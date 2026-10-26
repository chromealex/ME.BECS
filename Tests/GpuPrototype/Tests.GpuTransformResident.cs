#if FIXED_POINT
using System;
using System.Diagnostics;
using System.IO;
using ME.BECS.FixedPoint;
using ME.BECS.Transforms;
using ME.BECS.NativeCollections;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace ME.BECS.Tests.GpuPrototype {
    public partial class Tests_GpuTransformMatrices {
        [Serializable]
        private sealed class ResidentReport {
            public string device, unity;
            public string scope="Immutable branching trees of 8; clustered changed TRS; sfloat; completed batch includes final readback/scatter. Burst input mutation/reset included equally. No topology invalidation or GPU consumer integration.";
            public int count, percent, changedCount, steps=8;
            public double constructionMs;
            public double[] cpuMs=new double[3], syncGpuMs=new double[3], residentGpuMs=new double[3];
            public double[] initializationMs=new double[12];
        }
        [Test]
        public void ResidentHierarchyRetainsEarlierChangesAfterCleanStep() {
            var world=World.Create();
            try {
                var root=Create(world,1); var child=Create(world,2); child.SetParent(root);
                var entities=new[] {root,child};
                var positions=new[] {root.GetAspect<TransformAspect>().readLocalPosition,child.GetAspect<TransformAspect>().readLocalPosition};
                var expected=new TransformSnapshot[2];
                var stack=new NativeParallelList<Transform3DExt.HierarchyItem>(64,Allocator.Persistent);
                try {
                    using (var kernel=new GpuTransformHierarchyKernel(2)) {
                        for (var pass=0;pass<2;++pass) {
                            ResetFull(entities,positions); Batches.Apply(world);
                            kernel.InitializeResident(API.Query(world)); Batches.Apply(world);
                            for (var step=0;step<2;++step) {
                                var changed=step==0?1:0;
                                kernel.MutateResidentInputs(changed); Batches.Apply(world);
                                if (pass==0) { CpuFull(world,stack); }
                                else { kernel.StepResident(changed); }
                            }
                            if (pass==1) { kernel.DrainResident(); }
                            for (var i=0;i<2;++i) {
                                if (pass==0) { expected[i]=CaptureTransform(entities[i]); }
                                else { AssertTransform(expected[i],entities[i]); }
                            }
                        }
                    }
                } finally { stack.Dispose(); }
            } finally { world.Dispose(); }
        }
        [TestCase(65,100)]
        [TestCase(16384,1)]
        [TestCase(16384,10)]
        [TestCase(16384,100)]
        [TestCase(131072,1)]
        [TestCase(131072,10)]
        [TestCase(131072,100)]
        public void ResidentHierarchyMatchesBurst(int count,int percent) {
            var watch=Stopwatch.StartNew(); var world=World.Create();
            try {
                var entities=new Ent[count]; var positions=new float3[count];
                for (var i=0;i<count;++i) {
                    entities[i]=Create(world,i); positions[i]=entities[i].GetAspect<TransformAspect>().readLocalPosition;
                    if (i%8==0) { entities[i].SetTag<IsTransformStaticLocalComponent>(false); }
                    if (i%17==0 && i%8!=0) { entities[i].SetTag<IsTransformStaticComponent>(true); }
                    if (i%8!=0) { entities[i].SetParent(entities[i-i%8+(i%8-1)/2]); }
                }
                var stack=new NativeParallelList<Transform3DExt.HierarchyItem>(64,Allocator.Persistent);
                try {
                    using (var kernel=new GpuTransformHierarchyKernel(count)) {
                        var report=new ResidentReport {count=count,percent=percent,changedCount=System.Math.Max(1,count*percent/100),
                            device=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,constructionMs=watch.Elapsed.TotalMilliseconds};
                        // Snapshots outside timing allow parity after EVERY synchronous step and the final resident step.
                        var expected=new TransformSnapshot[report.steps,count];
                        for (var sample=-1;sample<3;++sample) {
                            // CPU reference first; alternate the two GPU modes to reduce order bias.
                            for (var pass=0;pass<3;++pass) {
                                var mode=pass==0?0:((sample+4)%2==0?pass:3-pass);
                                ResetFull(entities,positions); Batches.Apply(world);
                                var init=kernel.InitializeResident(API.Query(world));
                                report.initializationMs[(sample+1)*3+pass]=init.totalMs;
                                Batches.Apply(world);
                                watch.Restart();
                                for (var step=0;step<report.steps;++step) {
                                    kernel.MutateResidentInputs(report.changedCount);
                                    Batches.Apply(world);
                                    if (mode==0) { CpuFull(world,stack); }
                                    else {
                                        kernel.StepResident(report.changedCount);
                                        if (mode==1 || step==report.steps-1) { kernel.DrainResident(); }
                                    }

                                    if (sample<0 && mode==0) {
                                        for (var i=0;i<count;++i) { expected[step,i]=CaptureTransform(entities[i]); }
                                    } else if (sample<0 && (mode==1 || step==report.steps-1)) {
                                        for (var i=0;i<count;++i) { AssertTransform(expected[step,i],entities[i]); }
                                    }
                                }
                                var elapsed=watch.Elapsed.TotalMilliseconds;
                                if (sample>=0) {
                                    for (var i=0;i<count;++i) {
                                        if (mode==0) { expected[report.steps-1,i]=CaptureTransform(entities[i]); }
                                        else { AssertTransform(expected[report.steps-1,i],entities[i]); }
                                    }
                                    if (mode==0) { report.cpuMs[sample]=elapsed/report.steps; }
                                    else if (mode==1) { report.syncGpuMs[sample]=elapsed/report.steps; }
                                    else { report.residentGpuMs[sample]=elapsed/report.steps; }
                                }
                            }
                        }
                        File.WriteAllText($"Temp/becs-gpu-resident-hierarchy-{count}-{percent}.json",JsonUtility.ToJson(report,true));
                        TestContext.WriteLine($"count={count}; changed={percent}%; CPU={Median(report.cpuMs):F4}ms; GPU sync={Median(report.syncGpuMs):F4}ms; GPU resident={Median(report.residentGpuMs):F4}ms; init={Median(report.initializationMs):F4}ms");
                    }
                } finally { stack.Dispose(); }
            } finally { world.Dispose(); }
        }
    }
}
#endif
