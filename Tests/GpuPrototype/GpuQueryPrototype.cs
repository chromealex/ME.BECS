using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using ME.BECS.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ME.BECS.Tests.GpuPrototype {

    [Serializable]
    internal struct GpuTimings {
        public double queryMs;
        public double packMs;
        public double uploadMs;
        public double dispatchReadbackMs;
        public double scatterMs;
        public double totalMs;
    }

    // Editor test only. Owns its temporary imported shader and reusable buffers.
    internal sealed class GpuKernel<T> : IDisposable where T : struct, IJobForComponents<GpuPosition, GpuVelocity> {
        private readonly FieldInfo[] uniforms;
        private NativeArray<float> positions;
        private NativeArray<float> velocities;
        private string assetPath;
        private ComputeShader shader;
        private ComputeBuffer positionBuffer;
        private ComputeBuffer velocityBuffer;
        private int kernel;
        private bool disposed;

        public GpuKernel(int capacity, int repetitions = 1, bool precise = false) {
            if (capacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            if (SystemInfo.supportsComputeShaders == false || SystemInfo.supportsAsyncGPUReadback == false) {
                throw new NotSupportedException("This prototype requires compute shaders and native GPU readback.");
            }
            var source = ScalarJobShader.Compile<T>(repetitions, precise: precise);
            this.uniforms = ScalarJobShader.Uniforms<T>();
            try {
                this.positions = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                this.velocities = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                this.assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/__BecsGpuPrototype.compute");
                File.WriteAllText(this.assetPath, source);
                AssetDatabase.ImportAsset(this.assetPath, ImportAssetOptions.ForceSynchronousImport);
                this.shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(this.assetPath);
                if (this.shader == null || this.shader.HasKernel("Main") == false) {
                    throw new InvalidOperationException("Generated compute shader did not import with the Main kernel.");
                }
                this.kernel = this.shader.FindKernel("Main");
                this.positionBuffer = new ComputeBuffer(capacity, sizeof(float));
                this.velocityBuffer = new ComputeBuffer(capacity, sizeof(float));
                this.shader.SetBuffer(this.kernel, "Positions", this.positionBuffer);
                this.shader.SetBuffer(this.kernel, "Velocities", this.velocityBuffer);
            } catch {
                this.Dispose();
                throw;
            }
        }

        // Caller owns an isolated world and completes all other users before calling.
        // Readback blocks deliberately: CPU components are current when Run returns.
        // steps is a bounded chain of dispatches, with one upload/readback. No cross-frame ownership.
        public GpuTimings Run(QueryBuilder query, in T job, int steps = 1) {
            var total = Stopwatch.StartNew();
            var phase = Stopwatch.StartNew();
            var results = query.With<GpuPosition>().With<GpuVelocity>().ToArrayOnDemand();
            try {
                if (this.disposed == true) {
                    throw new ObjectDisposedException(nameof(GpuKernel<T>));
                }
                if (steps < 1 || steps > 32) {
                    throw new ArgumentOutOfRangeException(nameof(steps), "Prototype supports 1..32 resident steps.");
                }
                var entities = results.GetResults();
                var count = entities.Length;
                var timing = new GpuTimings { queryMs = phase.Elapsed.TotalMilliseconds };
                if (count > this.positions.Length) {
                    throw new InvalidOperationException("Query exceeds the prototype kernel capacity.");
                }
                if (count == 0) {
                    results.Dispose();
                    timing.totalMs = total.Elapsed.TotalMilliseconds;
                    return timing;
                }
                phase.Restart();
                new GpuPackJob {
                    entities = entities,
                    positions = this.positions,
                    velocities = this.velocities,
                }.Schedule(count, 64).Complete();
                timing.packMs = phase.Elapsed.TotalMilliseconds;
                phase.Restart();
                this.positionBuffer.SetData(this.positions, 0, 0, count);
                this.velocityBuffer.SetData(this.velocities, 0, 0, count);
                this.shader.SetInt("Count", count);
                object boxed = job;
                for (var i = 0; i < this.uniforms.Length; ++i) {
                    this.shader.SetFloat("j" + i, (float)this.uniforms[i].GetValue(boxed));
                }
                timing.uploadMs = phase.Elapsed.TotalMilliseconds;
                phase.Restart();
                for (var step = 0; step < steps; ++step) {
                    this.shader.Dispatch(this.kernel, (count + 63) / 64, 1, 1);
                }
                // Request owns its native result. Do not recycle a job's safety handle as a
                // RequestIntoNativeArray destination; Unity rejects that transition here.
                var readback = AsyncGPUReadback.Request(this.positionBuffer, count * sizeof(float), 0);
                readback.WaitForCompletion();
                if (readback.hasError == true) {
                    throw new InvalidOperationException("GPU readback failed; components were not updated.");
                }
                timing.dispatchReadbackMs = phase.Elapsed.TotalMilliseconds;
                phase.Restart();
                new GpuScatterJob {
                    entities = entities,
                    // Borrowed until the next frame; Complete below consumes it in this call.
                    positions = readback.GetData<float>(),
                }.Schedule(count, 64).Complete();
                timing.scatterMs = phase.Elapsed.TotalMilliseconds;
                // Include query cleanup in the returned total, including on empty queries.
                results.Dispose();
                timing.totalMs = total.Elapsed.TotalMilliseconds;
                return timing;
            } finally {
                if (results.IsCreated == true) {
                    results.Dispose();
                }
            }
        }

        public void Dispose() {
            if (this.disposed == true) {
                return;
            }
            this.disposed = true;
            this.positionBuffer?.Dispose();
            this.velocityBuffer?.Dispose();
            if (this.positions.IsCreated == true) {
                this.positions.Dispose();
            }
            if (this.velocities.IsCreated == true) {
                this.velocities.Dispose();
            }
            if (this.assetPath != null) {
                AssetDatabase.DeleteAsset(this.assetPath);
            }
        }
    }

    // Query results contain each entity once. Existing components only; no structural changes.
    // Complete() in Run keeps the borrowed entity array alive until both jobs finish.
    [BurstCompile(CompileSynchronously = true)]
    internal partial struct GpuPackJob : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        [WriteOnly] public NativeArray<float> positions;
        [WriteOnly] public NativeArray<float> velocities;

        public void Execute(int index) {
            this.positions[index] = this.entities[index].Read<GpuPosition>().value;
            this.velocities[index] = this.entities[index].Read<GpuVelocity>().value;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal partial struct GpuScatterJob : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        [ReadOnly] public NativeArray<float> positions;

        public void Execute(int index) {
            this.entities[index].Get<GpuPosition>().value = this.positions[index];
        }
    }

    internal static class GpuQueryExtensions {
        // Namespace and assembly keep this experiment out of the production API.
        public static GpuQuery OnGPU(this QueryBuilder query) => new GpuQuery(query);
    }

    internal ref struct GpuQuery {
        private QueryBuilder query;

        public GpuQuery(QueryBuilder query) {
            this.query = query;
        }

        #if FIXED_POINT
        public GpuTimings Run(GpuTransformLocalKernel kernel) => kernel.Run(this.query);
        public GpuHierarchyTimings Run(GpuTransformHierarchyKernel kernel) => kernel.Run(this.query);
        #endif

        public GpuTimings Run<T>(GpuKernel<T> kernel, in T job, int steps = 1) where T : struct, IJobForComponents<GpuPosition, GpuVelocity> {
            return kernel.Run(this.query, in job, steps);
        }
    }
}
