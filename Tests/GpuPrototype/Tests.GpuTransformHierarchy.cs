#if FIXED_POINT
using System;
using System.Diagnostics;
using System.IO;
using ME.BECS.Transforms;
using ME.BECS.FixedPoint;
using ME.BECS.Jobs;
using ME.BECS.NativeCollections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace ME.BECS.Tests.GpuPrototype {
    public partial class Tests_GpuTransformMatrices {
        [Serializable]
        private sealed class HierarchyReport {
            public string device, unity;
            public string mode="GPU local + GPU hierarchy, one readback; closed forest, CPU topology included";
            public int count, treeSize;
            public double setupMs, cpuColdMs;
            public GpuHierarchyTimings gpuCold;
            public double[] cpuMs=new double[5];
            public GpuHierarchyTimings[] gpu=new GpuHierarchyTimings[5];
        }
        private struct TransformSnapshot {
            public float4x4 local, world;
            public ulong dirtyTick;
            public bool calculated, staticLocalCalculated, staticCalculated;
        }
        private static TransformSnapshot CaptureTransform(Ent e) {
            var a=e.GetAspect<TransformAspect>();
            return new TransformSnapshot {local=a.readLocalMatrix,world=a.readWorldMatrix,dirtyTick=e.Read<DirtyMoveComponent>().tick,
                calculated=a.IsWorldMatrixTickCalculated,staticLocalCalculated=e.Has<IsTransformStaticLocalCalculatedComponent>(),
                staticCalculated=e.Has<IsTransformStaticCalculatedComponent>()};
        }
        private static void AssertTransform(TransformSnapshot expected,Ent e) {
            var actual=CaptureTransform(e);
            EqualBits(expected.local,actual.local); EqualBits(expected.world,actual.world);
            if (expected.dirtyTick!=actual.dirtyTick || expected.calculated!=actual.calculated ||
                expected.staticLocalCalculated!=actual.staticLocalCalculated || expected.staticCalculated!=actual.staticCalculated) {
                Assert.Fail($"Transform flags differ for {e}");
            }
        }
        private static void ResetFull(Ent[] entities,float3[] positions) {
            Reset(entities);
            for (var i=0;i<entities.Length;++i) {
                entities[i].Get<LocalPositionComponent>().value=positions[i];
                entities[i].SetTag<IsTransformStaticCalculatedComponent>(false);
            }
        }
        private static void CpuFull(World world,NativeParallelList<Transform3DExt.HierarchyItem> stack) {
            CpuLocal(world);
            API.Query(world).Without<GpuExcluded>().Without<ParentComponent>().AsParallel().AsUnsafe()
                .Schedule<TransformWorldMatrixUpdateSystem.CalculateHierarchyJob,TransformAspect>(
                    new TransformWorldMatrixUpdateSystem.CalculateHierarchyJob {hierarchyStack=stack}).Complete();
        }
        [TestCase(0,8)]
        [TestCase(65,8)]
        [TestCase(65,65)]
        [TestCase(16384,8)]
        [TestCase(131072,8)]
        public void LocalAndHierarchyOnGpuMatchProduction(int count,int treeSize) {
            Assert.AreEqual(144,UnsafeUtility.SizeOf<GpuTransformNode>());
            Assert.AreNotEqual(IntPtr.Zero,JobReflectionData<TransformWorldMatrixUpdateSystem.CalculateLocalMatrixJob>.data.Data);
            Assert.AreNotEqual(IntPtr.Zero,JobReflectionData<TransformWorldMatrixUpdateSystem.CalculateLocalMatrixStaticJob>.data.Data);
            Assert.AreNotEqual(IntPtr.Zero,JobReflectionData<TransformWorldMatrixUpdateSystem.CalculateHierarchyJob>.data.Data);
            var watch=Stopwatch.StartNew(); var world=World.Create();
            try {
                var entities=new Ent[count]; var positions=new float3[count];
                for (var i=0;i<count;++i) {
                    entities[i]=Create(world,i); positions[i]=entities[i].GetAspect<TransformAspect>().readLocalPosition;
                    if (i%treeSize==0) { entities[i].SetTag<IsTransformStaticLocalComponent>(false); }
                    if ((i%17==0 && i%treeSize!=0) || (i>0 && i%treeSize==0 && (i/treeSize)%3==0)) {
                        entities[i].SetTag<IsTransformStaticComponent>(true);
                    }
                    if (i%treeSize!=0) {
                        // Small trees branch; the 65-node case is a deep chain.
                        var parent=treeSize==65?i-1:i-i%treeSize+(i%treeSize-1)/2;
                        entities[i].SetParent(entities[parent]);
                    }
                }
                var excluded=Create(world,100); excluded.Set(new GpuExcluded());
                var incomplete=Ent.New(world); incomplete.Set(new LocalPositionComponent());
                var stack=new NativeParallelList<Transform3DExt.HierarchyItem>(64,Allocator.Persistent);
                try {
                    using (var kernel=new GpuTransformHierarchyKernel(System.Math.Max(1,count))) {
                        var report=new HierarchyReport {count=count,treeSize=treeSize,device=SystemInfo.graphicsDeviceName,
                            unity=Application.unityVersion,setupMs=watch.Elapsed.TotalMilliseconds};
                        var expected=new TransformSnapshot[count];
                        for (var sample=-1;sample<5;++sample) {
                            double cpuMs=0; var gpu=default(GpuHierarchyTimings);
                            for (var pass=0;pass<2;++pass) {
                                ResetFull(entities,positions); Batches.Apply(world);
                                var cpu=(sample+pass)%2==0;
                                watch.Restart();
                                if (cpu==true) { CpuFull(world,stack); cpuMs=watch.Elapsed.TotalMilliseconds; }
                                else { gpu=API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel); gpu.totalMs=watch.Elapsed.TotalMilliseconds; }
                                for (var i=0;i<count;++i) {
                                    if (pass==0) { expected[i]=CaptureTransform(entities[i]); }
                                    else { AssertTransform(expected[i],entities[i]); }
                                }
                                EqualBits(default,excluded.GetAspect<TransformAspect>().readLocalMatrix);
                                EqualBits(default,excluded.GetAspect<TransformAspect>().readWorldMatrix);
                                Assert.IsFalse(incomplete.Has<LocalMatrixComponent>());
                            }
                            if (sample<0) { report.cpuColdMs=cpuMs; report.gpuCold=gpu; }
                            else { report.cpuMs[sample]=cpuMs; report.gpu[sample]=gpu; }
                        }
                        // Exercise unchanged frames, inherited dirty, static freezing and invalidated world matrices.
                        // Keep this semantic sequence bounded; large cases above verify every matrix and flag.
                        if (count<=65) {
                            var sequence=new TransformSnapshot[5,count];
                            for (var pass=0;pass<2;++pass) {
                                ResetFull(entities,positions);
                                for (var stage=0;stage<5;++stage) {
                                    if (stage>0) { foreach (var e in entities) { e.Get<DirtyMoveComponent>().tick=ulong.MaxValue; } }
                                    if (stage==2) {
                                        for (var i=0;i<count;i+=treeSize) { entities[i].GetAspect<TransformAspect>().localPosition.x+=(sfloat)1; }
                                    }
                                    if (stage==3) {
                                        for (var i=0;i<count;++i) {
                                            var a=entities[i].GetAspect<TransformAspect>();
                                            if (a.IsStaticLocal==true || a.IsStatic==true) { a.localPosition.y+=(sfloat)3; }
                                        }
                                    }
                                    if (stage==4) {
                                        for (var i=0;i<count;i+=3) { entities[i].GetAspect<TransformAspect>().IsWorldMatrixTickCalculated=false; }
                                    }
                                    Batches.Apply(world);
                                    if (pass==0) { CpuFull(world,stack); }
                                    else { API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel); }
                                    for (var i=0;i<count;++i) {
                                        if (pass==0) { sequence[stage,i]=CaptureTransform(entities[i]); }
                                        else { AssertTransform(sequence[stage,i],entities[i]); }
                                    }
                                }
                            }
                        }
                        File.WriteAllText($"Temp/becs-gpu-full-hierarchy-{count}-{treeSize}.json",JsonUtility.ToJson(report,true));
                        TestContext.WriteLine($"count={count}; treeSize={treeSize}; CPU full={Median(report.cpuMs):F4}ms; GPU full={Median(Array.ConvertAll(report.gpu,static t=>t.totalMs)):F4}ms");
                    }
                } finally { stack.Dispose(); }
            } finally { world.Dispose(); }
        }
        [TestCase("cycle")]
        [TestCase("duplicate")]
        [TestCase("mismatch")]
        public void GpuHierarchyRejectsInvalidLinksBeforeWriting(string kind) {
            var world=World.Create();
            try {
                var root=Create(world,0); var child=Create(world,1); child.SetParent(root);
                if (kind=="cycle") { root.Set(new ParentComponent {value=child}); }
                else if (kind=="duplicate") { root.Get<ChildrenComponent>().list.Add(child); }
                else { child.Get<ParentComponent>().value=child; }
                Batches.Apply(world);
                var rootBefore=CaptureTransform(root); var childBefore=CaptureTransform(child);
                using (var kernel=new GpuTransformHierarchyKernel(2)) {
                    Assert.Throws<InvalidOperationException>(()=>API.Query(world).OnGPU().Run(kernel));
                    AssertTransform(rootBefore,root); AssertTransform(childBefore,child);
                }
            } finally { world.Dispose(); }
        }
        [Test]
        public void GpuHierarchyRejectsPartialForestBeforeWriting() {
            var world=World.Create();
            try {
                var root=Create(world,0); var child=Create(world,1); child.SetParent(root); child.Set(new GpuExcluded());
                Batches.Apply(world);
                var rootBefore=CaptureTransform(root); var childBefore=CaptureTransform(child);
                using (var kernel=new GpuTransformHierarchyKernel(2)) {
                    Assert.Throws<InvalidOperationException>(()=>API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel));
                    AssertTransform(rootBefore,root); AssertTransform(childBefore,child);
                    root.Set(new GpuExcluded()); child.Remove<GpuExcluded>(); Batches.Apply(world);
                    Assert.Throws<InvalidOperationException>(()=>API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel));
                    AssertTransform(rootBefore,root); AssertTransform(childBefore,child);
                }
            } finally { world.Dispose(); }
        }
    }
}
#endif
