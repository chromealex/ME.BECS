#if FIXED_POINT
using System;
using ME.BECS.Transforms;
using ME.BECS.FixedPoint;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Rendering;
using uint4 = Unity.Mathematics.uint4;

namespace ME.BECS.Tests.GpuPrototype {
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuResidentPack : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        public NativeArray<GpuTransformInput> inputs;
        public void Execute(int i) {
            var a=this.entities[i].GetAspect<TransformAspect>();
            var p=a.readLocalPosition; var r=a.readLocalRotation.value; var s=a.readLocalScale;
            this.inputs[i]=new GpuTransformInput {
                p=new uint4(p.x.RawValue,p.y.RawValue,p.z.RawValue,0),
                r=new uint4(r.x.RawValue,r.y.RawValue,r.z.RawValue,r.w.RawValue),
                s=new uint4(s.x.RawValue,s.y.RawValue,s.z.RawValue,0),
            };
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuRenderMatrixPack : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        public NativeArray<float4x4> matrices;
        public void Execute(int i) { this.matrices[i]=this.entities[i].GetAspect<TransformAspect>().readWorldMatrix; }
    }
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuResidentMutate : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        public int changedCount;
        public void Execute(int i) {
            var e=this.entities[i];
            e.Get<DirtyMoveComponent>().tick=ulong.MaxValue;
            if (i<this.changedCount) { e.Get<LocalPositionComponent>().value.x+=(sfloat)0.125f; }
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuResidentScatter : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        [ReadOnly] public NativeArray<GpuTransformNode> nodes;
        public void Execute(int i) {
            var n=this.nodes[i]; var e=this.entities[i]; var a=e.GetAspect<TransformAspect>();
            // Snapshot includes earlier steps even if the last step did not change this node.
            a.localMatrix=n.local; a.worldMatrix=n.world;
            if (n.info.z!=0u) { a.SetDirty(); }
            a.IsWorldMatrixTickCalculated=(n.info.x & 32u)!=0u || n.info.w!=0u;
            if ((n.info.x & 3u)==3u) { e.SetTag<IsTransformStaticLocalCalculatedComponent>(true); }
            if ((n.info.x & 4u)!=0u && n.info.w!=0u) { e.SetTag<IsTransformStaticCalculatedComponent>(true); }
        }
    }
    // Bounded experiment only: caller keeps this isolated forest alive and immutable until DrainResident.
    // Changed TRS occupy a contiguous prefix of cached preorder; no production change tracking is implied.
    internal sealed partial class GpuTransformHierarchyKernel {
        private int residentCount, residentRoots;
        public GpuHierarchyTimings InitializeResident(QueryBuilder query) {
            var timing=this.Run(query);
            this.residentCount=timing.count; this.residentRoots=timing.roots;
            return timing;
        }
        // Identical bounded input generation for the CPU and GPU benchmark paths, included in timings.
        public void MutateResidentInputs(int changedCount) {
            new GpuResidentMutate {entities=this.entities,changedCount=changedCount}.Schedule(this.residentCount,64).Complete();
        }
        public void StepResident(int changedCount) {
            if (this.residentCount==0 || changedCount<0 || changedCount>this.residentCount) {
                throw new InvalidOperationException("Initialize a nonempty immutable forest before stepping.");
            }
            if (changedCount>0) {
                new GpuResidentPack {entities=this.entities,inputs=this.inputs}.Schedule(changedCount,64).Complete();
                this.inputBuffer.SetData(this.inputs,0,0,changedCount);
            }
            var prepare=this.shader.FindKernel("PrepareResident");
            var local=this.shader.FindKernel("LocalHierarchy"); var world=this.shader.FindKernel("WorldHierarchy");
            this.shader.SetInt("Count",this.residentCount); this.shader.SetInt("RootCount",this.residentRoots);
            this.shader.SetBuffer(prepare,"Nodes",this.nodeBuffer);
            this.shader.Dispatch(prepare,(this.residentCount+63)/64,1,1);
            this.shader.Dispatch(local,(this.residentCount+63)/64,1,1);
            this.shader.Dispatch(world,(this.residentRoots+63)/64,1,1);
        }
        // The consumer owns its buffer and must keep it alive until submitted drawing has completed.
        public void ExportResidentMatrices(UnityEngine.ComputeBuffer target) {
            this.ValidateRenderBuffer(target);
            var export=this.shader.FindKernel("ExportRenderMatrices");
            this.shader.SetInt("Count",this.residentCount);
            this.shader.SetBuffer(export,"Nodes",this.nodeBuffer);
            this.shader.SetBuffer(export,"RenderMatrices",target);
            this.shader.Dispatch(export,(this.residentCount+63)/64,1,1);
        }
        public void UploadCpuRenderMatrices(UnityEngine.ComputeBuffer target,NativeArray<float4x4> staging) {
            this.ValidateRenderBuffer(target);
            if (staging.Length<this.residentCount) { throw new ArgumentException("Render staging capacity exceeded."); }
            new GpuRenderMatrixPack {entities=this.entities,matrices=staging}.Schedule(this.residentCount,64).Complete();
            target.SetData(staging,0,0,this.residentCount);
        }
        private void ValidateRenderBuffer(UnityEngine.ComputeBuffer target) {
            if (this.residentCount==0 || target==null || target.stride!=64 || target.count<this.residentCount) {
                throw new ArgumentException("Initialized forest and 64-byte matrix buffer of sufficient capacity required.");
            }
        }
        public void DrainResident() {
            if (this.residentCount==0) { throw new InvalidOperationException("Resident forest is not initialized."); }
            var request=AsyncGPUReadback.Request(this.nodeBuffer,this.residentCount*144,0);
            request.WaitForCompletion();
            if (request.hasError==true) { throw new InvalidOperationException("Resident readback failed."); }
            new GpuResidentScatter {entities=this.entities,nodes=request.GetData<GpuTransformNode>()}
                .Schedule(this.residentCount,64).Complete();
        }
    }
}
#endif
