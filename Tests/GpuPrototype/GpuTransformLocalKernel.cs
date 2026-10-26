#if FIXED_POINT
using System;
using System.Diagnostics;
using System.IO;
using ME.BECS.Transforms;
using ME.BECS.FixedPoint;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using uint4 = Unity.Mathematics.uint4;

namespace ME.BECS.Tests.GpuPrototype {
    internal struct GpuTransformInput { public uint4 p, r, s; }

    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuTransformPack : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        public NativeArray<GpuTransformInput> inputs;
        public void Execute(int i) {
            var a = this.entities[i].GetAspect<TransformAspect>();
            var p = a.readLocalPosition; var r = a.readLocalRotation.value; var s = a.readLocalScale;
            this.inputs[i] = new GpuTransformInput {
                p = new uint4(p.x.RawValue,p.y.RawValue,p.z.RawValue,0),
                r = new uint4(r.x.RawValue,r.y.RawValue,r.z.RawValue,r.w.RawValue),
                s = new uint4(s.x.RawValue,s.y.RawValue,s.z.RawValue,0),
            };
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuTransformScatter : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        [ReadOnly] public NativeArray<float4x4> matrices;
        // Diagnostic only: 0 = full contract, 1 = matrix, 2 = matrix + dirty.
        public int diagnosticStage;
        public void Execute(int i) {
            var a = this.entities[i].GetAspect<TransformAspect>();
            var current = this.matrices[i]; var previous = a.readLocalMatrix;
            if (math.all(previous.c0 == current.c0) == false || math.all(previous.c1 == current.c1) == false ||
                math.all(previous.c2 == current.c2) == false || math.all(previous.c3 == current.c3) == false) {
                a.localMatrix = current;
                if (this.diagnosticStage != 1) { a.SetDirty(); }
            }
            if (this.diagnosticStage == 0 && a.IsStaticLocal == true) { a.ent.SetTag<IsTransformStaticLocalCalculatedComponent>(true); }
        }
    }

    // Historical diagnostic comparator; the GPU adapter uses parallel tag writes.
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuTransformStaticTags : IJob {
        [ReadOnly] public NativeArray<Ent> entities;
        public void Execute() {
            for (var i=0;i<this.entities.Length;++i) {
                var e=this.entities[i];
                if (e.Has<IsTransformStaticLocalComponent>() == true) {
                    e.SetTag<IsTransformStaticLocalCalculatedComponent>(true);
                }
            }
        }
    }

    // Isolated-world synchronous prototype; hierarchy remains the production CPU job.
    internal sealed class GpuTransformLocalKernel : IDisposable {
        private NativeArray<GpuTransformInput> inputs;
        private ComputeBuffer inputBuffer, outputBuffer;
        private string assetPath;
        public ComputeShader Shader { get; private set; }
        public GpuTransformLocalKernel(int capacity) {
            if (capacity < 1) { throw new ArgumentOutOfRangeException(nameof(capacity)); }
            try {
                this.assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/__BecsGpuTransform.compute");
                File.WriteAllText(this.assetPath, GpuSfloatTransformShader.Source);
                AssetDatabase.ImportAsset(this.assetPath, ImportAssetOptions.ForceSynchronousImport);
                this.Shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(this.assetPath);
                if (this.Shader == null || this.Shader.HasKernel("Main") == false) { throw new InvalidOperationException("Transform shader import failed."); }
                this.inputs = new NativeArray<GpuTransformInput>(capacity, Allocator.Persistent);
                this.inputBuffer = new ComputeBuffer(capacity, 48);
                this.outputBuffer = new ComputeBuffer(capacity, 64);
            } catch { this.Dispose(); throw; }
        }
        public GpuTimings Run(QueryBuilder query) {
            var total = Stopwatch.StartNew(); var phase = Stopwatch.StartNew();
            var results = query.WithAspect<TransformAspect>().Without<IsTransformStaticCalculatedComponent>()
                .Without<IsTransformStaticLocalCalculatedComponent>().ToArrayOnDemand();
            try {
                var entities = results.GetResults(); var count = entities.Length;
                var timing = new GpuTimings { queryMs = phase.Elapsed.TotalMilliseconds };
                if (count > this.inputs.Length) { throw new InvalidOperationException("Transform capacity exceeded."); }
                if (count > 0) {
                    phase.Restart();
                    new GpuTransformPack { entities = entities, inputs = this.inputs }.Schedule(count,64).Complete();
                    timing.packMs = phase.Elapsed.TotalMilliseconds;
                    phase.Restart();
                    this.inputBuffer.SetData(this.inputs,0,0,count);
                    var k = this.Shader.FindKernel("Main");
                    this.Shader.SetBuffer(k,"Inputs",this.inputBuffer); this.Shader.SetBuffer(k,"Outputs",this.outputBuffer);
                    this.Shader.SetInt("Count",count);
                    timing.uploadMs = phase.Elapsed.TotalMilliseconds;
                    phase.Restart();
                    this.Shader.Dispatch(k,(count+63)/64,1,1);
                    var request = AsyncGPUReadback.Request(this.outputBuffer,count*64,0);
                    request.WaitForCompletion();
                    if (request.hasError == true) { throw new InvalidOperationException("Transform readback failed."); }
                    timing.dispatchReadbackMs = phase.Elapsed.TotalMilliseconds;
                    phase.Restart();
                    new GpuTransformScatter { entities = entities, matrices = request.GetData<float4x4>() }.Schedule(count,64).Complete();
                    timing.scatterMs = phase.Elapsed.TotalMilliseconds;
                }
                results.Dispose(); timing.totalMs = total.Elapsed.TotalMilliseconds; return timing;
            } finally { if (results.IsCreated == true) { results.Dispose(); } }
        }
        public void Dispose() {
            this.inputBuffer?.Dispose(); this.inputBuffer = null;
            this.outputBuffer?.Dispose(); this.outputBuffer = null;
            if (this.inputs.IsCreated == true) { this.inputs.Dispose(); }
            if (this.assetPath != null) { AssetDatabase.DeleteAsset(this.assetPath); this.assetPath = null; }
        }
    }
}
#endif
