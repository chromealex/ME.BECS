using System;
using System.Diagnostics;
using System.IO;
using ME.BECS.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ME.BECS.Tests.GpuPrototype {

    public partial struct GpuDampJob : IJobForComponents<GpuPosition, GpuVelocity> {
        public float retention;

        public void Execute(in JobInfo jobInfo, in Ent ent, ref GpuPosition position, ref GpuVelocity velocity) {
            position.value *= this.retention;
        }
    }

    // Fixed population/dense component storage only. This is not the production BECS World.
    // State remains in GPU buffers across Editor updates and across completed snapshots.
    internal sealed class GpuResidentWorldPrototype : IDisposable {
        private readonly ComputeBuffer positions;
        private readonly ComputeBuffer velocities;
        private readonly UnityEngine.Rendering.CommandBuffer graph;
        private readonly string[] assetPaths = new string[2];
        private readonly Action<AsyncGPUReadbackRequest> onReadback;
        private readonly Stopwatch elapsed = new Stopwatch();
        private AsyncGPUReadbackRequest request;
        private bool pending;
        private bool disposed;
        public readonly float[] snapshot;
        public bool ready { get; private set; }
        public bool failed { get; private set; }
        public double submitMs { get; private set; }
        public double readyMs { get; private set; }
        public double copyMs { get; private set; }

        public GpuResidentWorldPrototype(NativeArray<float> initial, NativeArray<float> velocity, int ticks, GpuMoveJob move, GpuDampJob damp) {
            if (initial.Length < 1 || initial.Length != velocity.Length || ticks < 1 || ticks > 256) {
                throw new ArgumentOutOfRangeException(nameof(initial));
            }
            this.snapshot = new float[initial.Length];
            this.onReadback = this.OnReadback;
            try {
                this.positions = new ComputeBuffer(initial.Length, sizeof(float));
                this.velocities = new ComputeBuffer(initial.Length, sizeof(float));
                this.positions.SetData(initial);
                this.velocities.SetData(velocity);
                var movement = this.Import<GpuMoveJob>(0);
                var damping = this.Import<GpuDampJob>(1);
                this.graph = new UnityEngine.Rendering.CommandBuffer { name = "BECS resident scalar graph (prototype)" };
                var moveKernel = this.Bind(movement, move, initial.Length);
                var dampKernel = this.Bind(damping, damp, initial.Length);
                for (var tick = 0; tick < ticks; ++tick) {
                    this.graph.DispatchCompute(movement, moveKernel, (initial.Length + 63) / 64, 1, 1);
                    this.graph.DispatchCompute(damping, dampKernel, (initial.Length + 63) / 64, 1, 1);
                }
            } catch {
                this.Dispose();
                throw;
            }
        }

        private ComputeShader Import<T>(int slot) where T : struct, IJobForComponents<GpuPosition, GpuVelocity> {
            this.assetPaths[slot] = AssetDatabase.GenerateUniqueAssetPath("Assets/__BecsGpuPrototypeResident.compute");
            File.WriteAllText(this.assetPaths[slot], ScalarJobShader.Compile<T>());
            AssetDatabase.ImportAsset(this.assetPaths[slot], ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(this.assetPaths[slot]);
            if (shader == null || shader.HasKernel("Main") == false) {
                throw new InvalidOperationException("Resident graph shader failed to import.");
            }
            return shader;
        }

        private int Bind<T>(ComputeShader shader, T job, int count) {
            var kernel = shader.FindKernel("Main");
            this.graph.SetComputeBufferParam(shader, kernel, "Positions", this.positions);
            this.graph.SetComputeBufferParam(shader, kernel, "Velocities", this.velocities);
            this.graph.SetComputeIntParam(shader, "Count", count);
            var fields = ScalarJobShader.Uniforms<T>();
            object boxed = job;
            for (var i = 0; i < fields.Length; ++i) {
                this.graph.SetComputeFloatParam(shader, "j" + i, (float)fields[i].GetValue(boxed));
            }
            return kernel;
        }

        // Blocking mode is a diagnostic control, not the normal resident-world update path.
        public void Submit(bool waitForCompletion = false) {
            if (this.disposed == true) {
                throw new ObjectDisposedException(nameof(GpuResidentWorldPrototype));
            }
            if (this.pending == true) {
                throw new InvalidOperationException("Consume the outstanding snapshot before submitting another batch.");
            }
            this.ready = false;
            this.failed = false;
            this.elapsed.Restart();
            Graphics.ExecuteCommandBuffer(this.graph);
            this.pending = true;
            try {
                this.request = waitForCompletion == true
                    ? AsyncGPUReadback.Request(this.positions)
                    : AsyncGPUReadback.Request(this.positions, this.onReadback);
            } catch {
                this.pending = false;
                throw;
            }
            this.submitMs = this.elapsed.Elapsed.TotalMilliseconds;
            if (waitForCompletion == true) {
                this.request.WaitForCompletion();
                // No callback was registered: consume the completed request exactly once.
                this.OnReadback(this.request);
            }
        }

        private void OnReadback(AsyncGPUReadbackRequest result) {
            this.readyMs = this.elapsed.Elapsed.TotalMilliseconds;
            this.failed = result.hasError;
            if (this.failed == false) {
                var copy = Stopwatch.StartNew();
                // Request data expires; copy while owned by this callback, never retain its NativeArray.
                result.GetData<float>().CopyTo(this.snapshot);
                this.copyMs = copy.Elapsed.TotalMilliseconds;
            }
            this.pending = false;
            this.ready = true;
        }

        public void Dispose() {
            if (this.disposed == true) {
                return;
            }
            // Failure/cancellation cleanup. Normal asynchronous samples never wait synchronously.
            if (this.pending == true) {
                this.request.WaitForCompletion();
                this.pending = false;
            }
            this.disposed = true;
            this.graph?.Dispose();
            this.positions?.Dispose();
            this.velocities?.Dispose();
            for (var i = 0; i < this.assetPaths.Length; ++i) {
                if (this.assetPaths[i] != null) {
                    AssetDatabase.DeleteAsset(this.assetPaths[i]);
                }
            }
        }
    }

    // One Burst job for the complete graph is valid here: entities have no cross-entity dependencies.
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    internal partial struct ResidentFusedCpuJob : IJobParallelFor {
        public NativeArray<float> positions;
        [ReadOnly] public NativeArray<float> velocities;
        public int ticks;
        public GpuMoveJob move;
        public GpuDampJob damp;

        public void Execute(int index) {
            var position = new GpuPosition { value = this.positions[index] };
            var velocity = new GpuVelocity { value = this.velocities[index] };
            for (var tick = 0; tick < this.ticks; ++tick) {
                this.move.Execute(default, default, ref position, ref velocity);
                this.damp.Execute(default, default, ref position, ref velocity);
            }
            this.positions[index] = position.value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    internal partial struct ResidentMoveCpuJob : IJobParallelFor {
        public NativeArray<float> positions;
        [ReadOnly] public NativeArray<float> velocities;
        public GpuMoveJob job;

        public void Execute(int index) {
            var position = new GpuPosition { value = this.positions[index] };
            var velocity = new GpuVelocity { value = this.velocities[index] };
            this.job.Execute(default, default, ref position, ref velocity);
            this.positions[index] = position.value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    internal partial struct ResidentDampCpuJob : IJobParallelFor {
        public NativeArray<float> positions;
        public GpuDampJob job;

        public void Execute(int index) {
            var position = new GpuPosition { value = this.positions[index] };
            var velocity = default(GpuVelocity);
            this.job.Execute(default, default, ref position, ref velocity);
            this.positions[index] = position.value;
        }
    }
}
