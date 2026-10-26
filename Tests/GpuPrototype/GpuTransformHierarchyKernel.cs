#if FIXED_POINT
using System;
using System.Diagnostics;
using System.IO;
using ME.BECS.FixedPoint;
using ME.BECS.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using uint2 = Unity.Mathematics.uint2;
using uint4 = Unity.Mathematics.uint4;

namespace ME.BECS.Tests.GpuPrototype {
    internal struct GpuTransformNode {
        public float4x4 local, world;
        // eligible local, static-local, static, static-calculated, dirty, world-calculated.
        public uint4 info;
    }
    [Serializable]
    internal struct GpuHierarchyTimings {
        public int count, roots;
        public double queryMs, topologyMs, packMs, uploadMs, dispatchReadbackMs, scatterMs, totalMs;
    }
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuHierarchyPack : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        [ReadOnly] public NativeArray<int> parents;
        public NativeArray<GpuTransformInput> inputs;
        public NativeArray<GpuTransformNode> nodes;
        public void Execute(int i) {
            var e=this.entities[i]; var a=e.GetAspect<TransformAspect>();
            var p=a.readLocalPosition; var r=a.readLocalRotation.value; var s=a.readLocalScale;
            this.inputs[i]=new GpuTransformInput {
                p=new uint4(p.x.RawValue,p.y.RawValue,p.z.RawValue,0),
                r=new uint4(r.x.RawValue,r.y.RawValue,r.z.RawValue,r.w.RawValue),
                s=new uint4(s.x.RawValue,s.y.RawValue,s.z.RawValue,0),
            };
            var staticCalculated=e.Has<IsTransformStaticCalculatedComponent>();
            var localEligible=staticCalculated==false && e.Has<IsTransformStaticLocalCalculatedComponent>()==false;
            var flags=(localEligible?1u:0u) | (a.IsStaticLocal?2u:0u) | (a.IsStatic?4u:0u) |
                (staticCalculated?8u:0u) | (a.IsDirty?16u:0u) | (a.IsWorldMatrixTickCalculated?32u:0u);
            this.nodes[i]=new GpuTransformNode {local=a.readLocalMatrix,world=a.readWorldMatrix,
                info=new uint4(flags,unchecked((uint)this.parents[i]),0,0)};
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuHierarchyScatter : IJobParallelFor {
        [ReadOnly] public NativeArray<Ent> entities;
        [ReadOnly] public NativeArray<GpuTransformNode> nodes;
        public void Execute(int i) {
            var n=this.nodes[i]; var e=this.entities[i]; var a=e.GetAspect<TransformAspect>();
            if (n.info.z!=0u) { a.localMatrix=n.local; a.SetDirty(); }
            if ((n.info.x & 3u)==3u) { e.SetTag<IsTransformStaticLocalCalculatedComponent>(true); }
            if (n.info.w!=0u) {
                a.worldMatrix=n.world;
                a.IsWorldMatrixTickCalculated=true;
                if ((n.info.x & 4u)!=0u) { e.SetTag<IsTransformStaticCalculatedComponent>(true); }
            }
        }
    }

    internal struct GpuHierarchyPending { public int sourceIndex, parent; }
    [BurstCompile(CompileSynchronously = true)]
    internal struct GpuHierarchyTopology : IJob {
        [ReadOnly] public NativeArray<Ent> source;
        public NativeParallelHashMap<Ent,int> selected;
        public NativeArray<byte> visited;
        public NativeList<GpuHierarchyPending> stack;
        public NativeArray<Ent> entities;
        public NativeArray<int> parents;
        public NativeArray<uint2> roots;
        public NativeArray<int> result;
        public void Execute() {
            this.selected.Clear(); this.stack.Clear(); this.result[0]=0; this.result[1]=0;
            for (var i=0;i<this.source.Length;++i) {
                this.selected.TryAdd(this.source[i],i); this.visited[i]=0;
            }
            var count=0; var rootCount=0;
            for (var root=0;root<this.source.Length;++root) {
                if (this.source[root].Has<ParentComponent>()==true) { continue; }
                var start=count;
                this.stack.Add(new GpuHierarchyPending {sourceIndex=root,parent=-1});
                while (this.stack.Length>0) {
                    var last=this.stack.Length-1; var item=this.stack[last]; this.stack.RemoveAt(last);
                    if (this.visited[item.sourceIndex]!=0) { this.result[1]=1; return; }
                    this.visited[item.sourceIndex]=1;
                    var e=this.source[item.sourceIndex]; var index=count++;
                    this.entities[index]=e; this.parents[index]=item.parent;
                    var a=e.GetAspect<TransformAspect>();
                    if (e.Has<ChildrenComponent>()==false) { continue; }
                    ref readonly var children=ref a.children;
                    for (var c=(int)children.Count-1;c>=0;--c) {
                        var child=children[(uint)c];
                        if (child.IsAlive()==false || child.worldId!=e.worldId ||
                            this.selected.TryGetValue(child,out var childIndex)==false ||
                            child.Has<ParentComponent>()==false || child.GetAspect<TransformAspect>().parent!=e) {
                            this.result[1]=2; return;
                        }
                        this.stack.Add(new GpuHierarchyPending {sourceIndex=childIndex,parent=index});
                    }
                }
                this.roots[rootCount++]=new uint2((uint)start,(uint)(count-start));
            }
            if (count!=this.source.Length) { this.result[1]=3; return; }
            this.result[0]=rootCount;
        }
    }

    // Test-only closed-forest snapshot. Caller owns an isolated world until Run returns.
    // Two GPU dispatches share Nodes; only their final result is read back to CPU.
    internal sealed partial class GpuTransformHierarchyKernel : IDisposable {
        private NativeArray<Ent> entities;
        private NativeArray<int> parents;
        private NativeArray<GpuTransformInput> inputs;
        private NativeArray<GpuTransformNode> nodes;
        private NativeArray<uint2> roots;
        private ComputeBuffer inputBuffer, nodeBuffer, rootBuffer;
        private ComputeShader shader;
        private string assetPath;
        private NativeParallelHashMap<Ent,int> selected;
        private NativeArray<byte> visited;
        private NativeList<GpuHierarchyPending> stack;
        private NativeArray<int> topologyResult;

        public GpuTransformHierarchyKernel(int capacity) {
            if (capacity<1) { throw new ArgumentOutOfRangeException(nameof(capacity)); }
            try {
                this.assetPath=AssetDatabase.GenerateUniqueAssetPath("Assets/__BecsGpuHierarchy.compute");
                File.WriteAllText(this.assetPath,GpuSfloatTransformShader.Source);
                AssetDatabase.ImportAsset(this.assetPath,ImportAssetOptions.ForceSynchronousImport);
                this.shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(this.assetPath);
                if (this.shader==null || this.shader.HasKernel("LocalHierarchy")==false || this.shader.HasKernel("WorldHierarchy")==false) {
                    throw new InvalidOperationException("Hierarchy shader import failed.");
                }
                this.entities=new NativeArray<Ent>(capacity,Allocator.Persistent);
                this.parents=new NativeArray<int>(capacity,Allocator.Persistent);
                this.inputs=new NativeArray<GpuTransformInput>(capacity,Allocator.Persistent);
                this.nodes=new NativeArray<GpuTransformNode>(capacity,Allocator.Persistent);
                this.roots=new NativeArray<uint2>(capacity,Allocator.Persistent);
                this.selected=new NativeParallelHashMap<Ent,int>(capacity,Allocator.Persistent);
                this.visited=new NativeArray<byte>(capacity,Allocator.Persistent);
                this.stack=new NativeList<GpuHierarchyPending>(capacity,Allocator.Persistent);
                this.topologyResult=new NativeArray<int>(2,Allocator.Persistent);
                this.inputBuffer=new ComputeBuffer(capacity,48);
                this.nodeBuffer=new ComputeBuffer(capacity,144);
                this.rootBuffer=new ComputeBuffer(capacity,8);
            } catch { this.Dispose(); throw; }
        }
        private int BuildTopology(NativeArray<Ent> source) {
            new GpuHierarchyTopology {source=source,selected=this.selected,visited=this.visited,stack=this.stack,
                entities=this.entities,parents=this.parents,roots=this.roots,result=this.topologyResult}.Schedule().Complete();
            if (this.topologyResult[1]!=0) {
                throw new InvalidOperationException($"GPU hierarchy requires a closed, live same-world forest with matching parent/children links (error {this.topologyResult[1]}).");
            }
            return this.topologyResult[0];
        }
        public GpuHierarchyTimings Run(QueryBuilder query) {
            this.residentCount=0;
            var total=Stopwatch.StartNew(); var phase=Stopwatch.StartNew();
            var results=query.WithAspect<TransformAspect>().ToArrayOnDemand();
            try {
                var source=results.GetResults(); var count=source.Length;
                var timing=new GpuHierarchyTimings {count=count,queryMs=phase.Elapsed.TotalMilliseconds};
                if (count>this.entities.Length) { throw new InvalidOperationException("Hierarchy capacity exceeded."); }
                if (count>0) {
                    phase.Restart(); timing.roots=this.BuildTopology(source); timing.topologyMs=phase.Elapsed.TotalMilliseconds;
                    phase.Restart();
                    new GpuHierarchyPack {entities=this.entities,parents=this.parents,inputs=this.inputs,nodes=this.nodes}.Schedule(count,64).Complete();
                    timing.packMs=phase.Elapsed.TotalMilliseconds;
                    phase.Restart();
                    this.inputBuffer.SetData(this.inputs,0,0,count); this.nodeBuffer.SetData(this.nodes,0,0,count);
                    this.rootBuffer.SetData(this.roots,0,0,timing.roots);
                    var local=this.shader.FindKernel("LocalHierarchy"); var world=this.shader.FindKernel("WorldHierarchy");
                    this.shader.SetInt("Count",count); this.shader.SetInt("RootCount",timing.roots);
                    this.shader.SetBuffer(local,"Inputs",this.inputBuffer); this.shader.SetBuffer(local,"Nodes",this.nodeBuffer);
                    this.shader.SetBuffer(world,"Nodes",this.nodeBuffer); this.shader.SetBuffer(world,"Roots",this.rootBuffer);
                    timing.uploadMs=phase.Elapsed.TotalMilliseconds;
                    phase.Restart();
                    this.shader.Dispatch(local,(count+63)/64,1,1);
                    this.shader.Dispatch(world,(timing.roots+63)/64,1,1);
                    var request=AsyncGPUReadback.Request(this.nodeBuffer,count*144,0); request.WaitForCompletion();
                    if (request.hasError==true) { throw new InvalidOperationException("Hierarchy GPU readback failed."); }
                    timing.dispatchReadbackMs=phase.Elapsed.TotalMilliseconds;
                    phase.Restart();
                    new GpuHierarchyScatter {entities=this.entities,nodes=request.GetData<GpuTransformNode>()}.Schedule(count,64).Complete();
                    timing.scatterMs=phase.Elapsed.TotalMilliseconds;
                }
                results.Dispose(); timing.totalMs=total.Elapsed.TotalMilliseconds; return timing;
            } finally { if (results.IsCreated==true) { results.Dispose(); } }
        }
        public void Dispose() {
            this.inputBuffer?.Dispose(); this.inputBuffer=null;
            this.nodeBuffer?.Dispose(); this.nodeBuffer=null;
            this.rootBuffer?.Dispose(); this.rootBuffer=null;
            if (this.entities.IsCreated==true) { this.entities.Dispose(); }
            if (this.parents.IsCreated==true) { this.parents.Dispose(); }
            if (this.inputs.IsCreated==true) { this.inputs.Dispose(); }
            if (this.nodes.IsCreated==true) { this.nodes.Dispose(); }
            if (this.roots.IsCreated==true) { this.roots.Dispose(); }
            if (this.selected.IsCreated==true) { this.selected.Dispose(); }
            if (this.visited.IsCreated==true) { this.visited.Dispose(); }
            if (this.stack.IsCreated==true) { this.stack.Dispose(); }
            if (this.topologyResult.IsCreated==true) { this.topologyResult.Dispose(); }
            if (this.assetPath!=null) { AssetDatabase.DeleteAsset(this.assetPath); this.assetPath=null; }
        }
    }
}
#endif
