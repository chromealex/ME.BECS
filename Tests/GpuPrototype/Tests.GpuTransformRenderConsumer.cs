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
        private sealed class RenderConsumerReport {
            public string device, unity;
            public string scope="Immutable forest; all input TRS change; sfloat; identical procedural triangle consumer. Eight draws per batch, final image readback included; no matrix readback/scatter during GPU updates. Not production provider or frame latency.";
            public int count, steps=8;
            public double constructionMs;
            public double[] cpuMs=new double[3], gpuMs=new double[3], initializationMs=new double[8];
        }
        private static void AssertRendered(byte[] expected,byte[] actual) {
            CollectionAssert.AreEqual(expected,actual,"CPU and GPU rendered images differ.");
            var visible=0;
            for (var i=0;i<actual.Length;i+=4) { if (actual[i]>0) { ++visible; } }
            Assert.Greater(visible,0,"Parity must not pass on two empty render targets.");
        }
        [TestCase(65)]
        [TestCase(16384)]
        [TestCase(131072)]
        public void GpuMatrixConsumerMatchesBurstImage(int count) {
            var watch=Stopwatch.StartNew(); var world=World.Create();
            try {
                var entities=new Ent[count]; var positions=new float3[count];
                var side=(int)System.Math.Ceiling(System.Math.Sqrt((count+7)/8));
                for (var i=0;i<count;++i) {
                    entities[i]=Create(world,i);
                    var a=entities[i].GetAspect<TransformAspect>();
                    a.localRotation=quaternion.identity; a.localScale=new float3((sfloat)1);
                    var root=i/8;
                    positions[i]=i%8==0?new float3((sfloat)(-0.8f+1.6f*(root%side)/side),(sfloat)(-0.8f+1.6f*(root/side)/side),(sfloat)0):
                        new float3((sfloat)0.03f,(sfloat)0.03f,(sfloat)0);
                    a.localPosition=positions[i];
                    if (i%8==0) { entities[i].SetTag<IsTransformStaticLocalComponent>(false); }
                    if (i%17==0 && i%8!=0) { entities[i].SetTag<IsTransformStaticComponent>(true); }
                    if (i%8!=0) { entities[i].SetParent(entities[i-i%8+(i%8-1)/2]); }
                }
                var stack=new NativeParallelList<Transform3DExt.HierarchyItem>(64,Allocator.Persistent);
                try {
                    using (var kernel=new GpuTransformHierarchyKernel(count))
                    using (var consumer=new GpuTransformRenderConsumer(count))
                    using (var staging=new NativeArray<float4x4>(count,Allocator.Persistent)) {
                        var report=new RenderConsumerReport {count=count,device=SystemInfo.graphicsDeviceName,
                            unity=Application.unityVersion,constructionMs=watch.Elapsed.TotalMilliseconds};
                        var expected=new byte[report.steps][];
                        for (var sample=-1;sample<3;++sample) {
                            for (var mode=0;mode<2;++mode) {
                                ResetFull(entities,positions); Batches.Apply(world);
                                report.initializationMs[(sample+1)*2+mode]=kernel.InitializeResident(API.Query(world)).totalMs;
                                Batches.Apply(world);
                                var cpuBefore=entities[0].GetAspect<TransformAspect>().readWorldMatrix;
                                byte[] initial=null;
                                if (sample<0) {
                                    kernel.UploadCpuRenderMatrices(consumer.Matrices,staging); consumer.Draw();
                                    initial=consumer.ReadCompletedImage();
                                }
                                byte[] image=null;
                                watch.Restart();
                                for (var step=0;step<report.steps;++step) {
                                    kernel.MutateResidentInputs(count); Batches.Apply(world);
                                    if (mode==0) {
                                        CpuFull(world,stack);
                                        kernel.UploadCpuRenderMatrices(consumer.Matrices,staging);
                                    } else {
                                        kernel.StepResident(count);
                                        kernel.ExportResidentMatrices(consumer.Matrices);
                                    }
                                    consumer.Draw();
                                    if (sample<0 || step==report.steps-1) {
                                        image=consumer.ReadCompletedImage();
                                        if (sample<0) {
                                            if (mode==0) { expected[step]=image; }
                                            else { AssertRendered(expected[step],image); }
                                        }
                                    }
                                }
                                var elapsed=watch.Elapsed.TotalMilliseconds/report.steps;
                                if (mode==1) { EqualBits(cpuBefore,entities[0].GetAspect<TransformAspect>().readWorldMatrix); }
                                if (sample<0) { CollectionAssert.AreNotEqual(initial,image,"Consumer must observe updated GPU matrices."); }
                                else if (mode==0) { expected[report.steps-1]=image; report.cpuMs[sample]=elapsed; }
                                else { AssertRendered(expected[report.steps-1],image); report.gpuMs[sample]=elapsed; }
                            }
                        }
                        File.WriteAllText($"Temp/becs-gpu-render-consumer-{count}.json",JsonUtility.ToJson(report,true));
                        TestContext.WriteLine($"count={count}; Burst+upload+draw={Median(report.cpuMs):F4}ms; GPU+export+draw={Median(report.gpuMs):F4}ms; initialize={Median(report.initializationMs):F4}ms");
                    }
                } finally { stack.Dispose(); }
            } finally { world.Dispose(); }
        }
    }
}
#endif
