#if FIXED_POINT
using System;
using System.Diagnostics;
using System.IO;
using ME.BECS.Transforms;
using ME.BECS.Jobs;
using ME.BECS.FixedPoint;
using ME.BECS.NativeCollections;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using uint2 = Unity.Mathematics.uint2;
using uint4 = Unity.Mathematics.uint4;

namespace ME.BECS.Tests.GpuPrototype {
    public partial class Tests_GpuTransformMatrices {
        private bool enabled, synchronous;
        [SetUp]
        public void SetUp() {
            if (SystemInfo.supportsComputeShaders == false || SystemInfo.supportsAsyncGPUReadback == false) { Assert.Ignore("Compute/readback required."); }
            this.enabled = BurstCompiler.Options.EnableBurstCompilation;
            this.synchronous = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompilation = true;
            // Do not synchronously compile the entire installed bootstrap during setup.
            try {
                AllTests.Start();
                BurstCompiler.Options.EnableBurstCompileSynchronously = true;
            } catch {
                BurstCompiler.Options.EnableBurstCompileSynchronously = this.synchronous;
                BurstCompiler.Options.EnableBurstCompilation = this.enabled;
                throw;
            }
        }
        [TearDown]
        public void TearDown() {
            AllTests.Dispose();
            BurstCompiler.Options.EnableBurstCompileSynchronously = this.synchronous;
            BurstCompiler.Options.EnableBurstCompilation = this.enabled;
        }
        [Test]
        public void SfloatOperatorsMatchRawBits() {
            var special = new uint[] { 0,0x80000000,1,0x80000001,0x7fffff,0x807fffff,0x800000,0x80800000,
                0x3f800000,0xbf800000,0x3f800001,0xbf800001,0x7f7fffff,0xff7fffff,0x7f800000,0xff800000,0x7fc01234,0xffc00000 };
            var pairs = new uint2[special.Length*special.Length+65536];
            var n = 0;
            foreach (var a in special) { foreach (var b in special) { pairs[n++] = new uint2(a,b); } }
            uint state = 0x7439ab12;
            while (n < pairs.Length) { pairs[n++] = new uint2(Next(ref state),Next(ref state)); }
            using (var kernel = new GpuTransformLocalKernel(1))
            using (var input = new ComputeBuffer(pairs.Length,8))
            using (var output = new ComputeBuffer(pairs.Length,16)) {
                input.SetData(pairs);
                var shader = kernel.Shader; var k = shader.FindKernel("Arithmetic");
                shader.SetBuffer(k,"Pairs",input); shader.SetBuffer(k,"ArithmeticResults",output); shader.SetInt("Count",pairs.Length);
                shader.Dispatch(k,(pairs.Length+63)/64,1,1);
                var request = AsyncGPUReadback.Request(output); request.WaitForCompletion();
                Assert.IsFalse(request.hasError);
                var data = request.GetData<uint4>();
                for (var i = 0; i < pairs.Length; ++i) {
                    var a = sfloat.FromRaw(pairs[i].x); var b = sfloat.FromRaw(pairs[i].y);
                    Assert.AreEqual((a+b).RawValue,data[i].x,$"add {i}: {pairs[i].x:X8}/{pairs[i].y:X8}");
                    Assert.AreEqual((a-b).RawValue,data[i].y,$"sub {i}: {pairs[i].x:X8}/{pairs[i].y:X8}");
                    Assert.AreEqual((a*b).RawValue,data[i].z,$"mul {i}: {pairs[i].x:X8}/{pairs[i].y:X8}");
                }
            }
        }
        [Serializable]
        private sealed class Report {
            public int count;
            public string device, unity;
            public string mode = "sfloat raw-bit parity; local dynamic/static jobs on GPU; production hierarchy remains CPU; independent resets";
            public double setupMs, cpuColdMs, gpuColdMs;
            public double[] cpuLocalMs = new double[7];
            public double[] cpuSplitTagsMs = new double[7];
            public GpuTimings[] gpuLocal = new GpuTimings[7];
            public double[] cpuHierarchyMs = new double[7];
            public double[] gpuHierarchyMs = new double[7];
        }
        [TestCase(0)]
        [TestCase(65)]
        [TestCase(16384)]
        [TestCase(131072)]
        public void LocalJobsAndCpuHierarchyMatchProduction(int count) {
            Assert.AreNotEqual(IntPtr.Zero,JobReflectionData<TransformWorldMatrixUpdateSystem.CalculateLocalMatrixJob>.data.Data);
            Assert.AreNotEqual(IntPtr.Zero,JobReflectionData<TransformWorldMatrixUpdateSystem.CalculateLocalMatrixStaticJob>.data.Data);
            Assert.AreNotEqual(IntPtr.Zero,JobReflectionData<TransformWorldMatrixUpdateSystem.CalculateHierarchyJob>.data.Data);
            var watch = Stopwatch.StartNew(); var world = World.Create();
            try {
                var entities = new Ent[count];
                for (var i = 0; i < count; ++i) {
                    entities[i] = Create(world,i);
                    if (i%8 != 0) { entities[i].SetParent(entities[i-1]); }
                }
                var excluded = Create(world,100); excluded.Set(new GpuExcluded());
                var incomplete = Ent.New(world); incomplete.Set(new LocalPositionComponent());
                Batches.Apply(world);
                var expectedLocal = new float4x4[count]; var expectedWorld = new float4x4[count];
                var stack = new NativeParallelList<Transform3DExt.HierarchyItem>(64,Allocator.Persistent);
                try {
                using (var kernel = new GpuTransformLocalKernel(System.Math.Max(1,count))) {
                    var report = new Report { count=count,device=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,setupMs=watch.Elapsed.TotalMilliseconds };
                    for (var sample=-1; sample<7; ++sample) {
                        double cpuMs=0,cpuSplitMs=0,gpuHierarchy=0,cpuHierarchy=0; var gpuTime=default(GpuTimings);
                        for (var pass=0; pass<3; ++pass) {
                            Reset(entities); Batches.Apply(world);
                            var path = (sample+pass+3)%3;
                            var cpu = path==0;
                            watch.Restart();
                            if (cpu == true) { CpuLocal(world); cpuMs=watch.Elapsed.TotalMilliseconds; }
                            else if (path==2) { CpuSplitLocal(world); cpuSplitMs=watch.Elapsed.TotalMilliseconds; }
                            else { gpuTime=API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel); gpuTime.totalMs=watch.Elapsed.TotalMilliseconds; }
                            // Use the production hierarchy job unchanged, with the same parent-first traversal.
                            watch.Restart();
                            API.Query(world).Without<GpuExcluded>().Without<ParentComponent>().AsParallel().AsUnsafe()
                                .Schedule<TransformWorldMatrixUpdateSystem.CalculateHierarchyJob,TransformAspect>(new TransformWorldMatrixUpdateSystem.CalculateHierarchyJob { hierarchyStack=stack }).Complete();
                            if (cpu == true) { cpuHierarchy=watch.Elapsed.TotalMilliseconds; } else if (path==1) { gpuHierarchy=watch.Elapsed.TotalMilliseconds; }
                            for (var i=0;i<count;++i) {
                                var a=entities[i].GetAspect<TransformAspect>();
                                if (pass==0) { expectedLocal[i]=a.readLocalMatrix; expectedWorld[i]=a.readWorldMatrix; }
                                else { EqualBits(expectedLocal[i],a.readLocalMatrix); EqualBits(expectedWorld[i],a.readWorldMatrix); }
                                Assert.IsTrue(a.IsDirty);
                                Assert.AreEqual(i%5==0,entities[i].Has<IsTransformStaticLocalCalculatedComponent>());
                            }
                            EqualBits(default,excluded.GetAspect<TransformAspect>().readLocalMatrix);
                            Assert.IsFalse(incomplete.Has<LocalMatrixComponent>());
                        }
                        if (sample<0) { report.cpuColdMs=cpuMs; report.gpuColdMs=gpuTime.totalMs; }
                        else { report.cpuLocalMs[sample]=cpuMs; report.cpuSplitTagsMs[sample]=cpuSplitMs; report.gpuLocal[sample]=gpuTime; report.cpuHierarchyMs[sample]=cpuHierarchy; report.gpuHierarchyMs[sample]=gpuHierarchy; }
                    }
                    // An unchanged local matrix must not mark an otherwise clean dynamic entity dirty.
                    foreach (var e in entities) { e.Get<DirtyMoveComponent>().tick=ulong.MaxValue; }
                    Batches.Apply(world);
                    API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel);
                    foreach (var e in entities) { Assert.IsFalse(e.GetAspect<TransformAspect>().IsDirty); }
                    // Static-local calculated matrices must remain untouched on a later update.
                    if (count>0) {
                        var a=entities[0].GetAspect<TransformAspect>(); var before=a.readLocalMatrix;
                        a.localPosition.x=(sfloat)999f;
                        Batches.Apply(world);
                        API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel);
                        EqualBits(before,a.readLocalMatrix);
                    }
                    File.WriteAllText($"Temp/becs-gpu-transform-{count}.json",JsonUtility.ToJson(report,true));
                    TestContext.WriteLine($"count={count}; CPU local={Median(report.cpuLocalMs):F4}ms; CPU split tags={Median(report.cpuSplitTagsMs):F4}ms; GPU local full={Median(Array.ConvertAll(report.gpuLocal,static t=>t.totalMs)):F4}ms");
                }
                } finally { stack.Dispose(); }
            } finally { world.Dispose(); }
        }
        [Serializable]
        private sealed class ScatterReport {
            public int count;
            public string device;
            public ScatterSample[] samples;
        }
        [Serializable]
        private sealed class ScatterSample {
            public int stage, batch;
            public double[] ms = new double[5];
        }
        [TestCase(16384)]
        [TestCase(131072)]
        public void ScatterCostBreakdown(int count) {
            var world = World.Create();
            try {
                var entities = new Ent[count];
                for (var i=0;i<count;++i) { entities[i]=Create(world,i); }
                using (var nativeEntities = new NativeArray<Ent>(entities,Allocator.Persistent))
                using (var matrices = new NativeArray<float4x4>(count,Allocator.Persistent)) {
                    var writableMatrices=matrices;
                    for (var i=0;i<count;++i) { writableMatrices[i]=float4x4.identity; }
                    var report = new ScatterReport { count=count,device=SystemInfo.graphicsDeviceName,samples=new ScatterSample[12] };
                    var index=0;
                    foreach (var batch in new[] {64,1024,count}) {
                        foreach (var stage in new[] {1,2,0,3}) {
                            var sample=new ScatterSample {stage=stage,batch=batch};
                            report.samples[index++]=sample;
                            for (var repeat=-1;repeat<5;++repeat) {
                                Reset(entities); Batches.Apply(world);
                                var watch=Stopwatch.StartNew();
                                var scatter=new GpuTransformScatter {entities=nativeEntities,matrices=matrices,diagnosticStage=stage}.Schedule(count,batch);
                                try {
                                    if (stage==3) { scatter=new GpuTransformStaticTags {entities=nativeEntities}.Schedule(scatter); }
                                } finally { scatter.Complete(); }
                                watch.Stop();
                                if (repeat>=0) { sample.ms[repeat]=watch.Elapsed.TotalMilliseconds; }
                                for (var i=0;i<count;++i) {
                                    var a=entities[i].GetAspect<TransformAspect>();
                                    EqualBits(float4x4.identity,a.readLocalMatrix);
                                    Assert.AreEqual(stage!=1,a.IsDirty);
                                    Assert.AreEqual((stage==0 || stage==3) && i%5==0,entities[i].Has<IsTransformStaticLocalCalculatedComponent>());
                                }
                            }
                            TestContext.WriteLine($"stage={stage} batch={batch} median={Median(sample.ms):F4}ms");
                        }
                    }
                    File.WriteAllText($"Temp/becs-gpu-transform-scatter-{count}.json",JsonUtility.ToJson(report,true));
                }
            } finally { world.Dispose(); }
        }
        [Serializable]
        private sealed class TagCostReport {
            public string device, unity;
            public bool profilerEnabled;
            public int count, operations, workers;
            public TagCostSample[] samples;
        }
        [Serializable]
        private sealed class TagCostSample {
            public string operation;
            public int batch, stride;
            public double[] ms = new double[5];
        }
        [BurstCompile(CompileSynchronously = true)]
        private unsafe struct TagCostJob : IJobParallelFor {
            [ReadOnly] public NativeArray<Ent> entities;
            [NativeDisableParallelForRestriction] public NativeArray<int> locks;
            public NativeArray<int> visits;
            public BitArray bits;
            public World world;
            public int operation, stride;
            public ProfilerMarker cachedLock, cachedUnlock;
            public void Execute(int i) {
                if (i%5 != 0) { return; }
                if (this.operation<=1) {
                    this.entities[i].SetTag<IsTransformStaticLocalCalculatedComponent>(true);
                } else if (this.operation==2) {
                    this.bits.SetThreaded(in this.world.state.ptr->allocator,(uint)i,true);
                } else if (this.operation==3) {
                    var p=(LockSpinner*)this.locks.GetUnsafePtr()+i*this.stride;
                    p->Lock(); p->Unlock();
                } else if (this.operation==4) {
                    // Test-only control: the same CAS/barriers, without diagnostic markers.
                    ref var value=ref ((int*)this.locks.GetUnsafePtr())[i*this.stride];
                    while (System.Threading.Interlocked.CompareExchange(ref value,1,0)!=0) { Unity.Burst.Intrinsics.Common.Pause(); }
                    System.Threading.Interlocked.MemoryBarrier();
                    System.Threading.Interlocked.MemoryBarrier();
                    while (System.Threading.Interlocked.CompareExchange(ref value,0,1)!=1) { Unity.Burst.Intrinsics.Common.Pause(); }
                } else if (this.operation==5) {
                    var a=new ProfilerMarker("Lock"); a.Begin(); a.End();
                    var b=new ProfilerMarker("Unlock"); b.Begin(); b.End();
                } else {
                    this.cachedLock.Begin(); this.cachedLock.End();
                    this.cachedUnlock.Begin(); this.cachedUnlock.End();
                }
                this.visits[i]=1;
            }
        }
        [Test]
        public unsafe void TagSynchronizationCosts() {
            const int count=131072;
            Assert.AreEqual(sizeof(int),UnsafeUtility.SizeOf<LockSpinner>());
            var world=World.Create();
            try {
                var entities=new Ent[count];
                for (var i=0;i<count;++i) { entities[i]=Ent.New(world); }
                var bits=new BitArray(ref world.state.ptr->allocator,count,threadSafe:true);
                try {
                    using (var nativeEntities=new NativeArray<Ent>(entities,Allocator.Persistent))
                    using (var locks=new NativeArray<int>(count*32,Allocator.Persistent))
                    using (var visits=new NativeArray<int>(count,Allocator.Persistent)) {
                        var names=new[] {"SetTag absent","SetTag existing","BitArray.SetThreaded","LockSpinner pair","CAS pair without markers","Create marker pairs","Cached marker pairs"};
                        var samples=new System.Collections.Generic.List<TagCostSample>();
                        var cachedLock=new ProfilerMarker("Lock"); var cachedUnlock=new ProfilerMarker("Unlock");
                        foreach (var batch in new[] {64,count}) {
                            for (var operation=0;operation<names.Length;++operation) {
                                foreach (var stride in new[] {1,32}) {
                                    if (stride==32 && operation!=3 && operation!=4) { continue; }
                                    var sample=new TagCostSample {operation=names[operation],batch=batch,stride=stride};
                                    samples.Add(sample);
                                    for (var repeat=-1;repeat<5;++repeat) {
                                        for (var i=0;i<count;i+=5) {
                                            entities[i].SetTag<IsTransformStaticLocalCalculatedComponent>(operation==1);
                                            bits.SetThreaded(in world.state.ptr->allocator,(uint)i,false);
                                        }
                                        Batches.Apply(world);
                                        var writableVisits=visits;
                                        for (var i=0;i<count;++i) { writableVisits[i]=0; }
                                        var job=new TagCostJob {entities=nativeEntities,locks=locks,visits=visits,bits=bits,world=world,
                                            operation=operation,stride=stride,cachedLock=cachedLock,cachedUnlock=cachedUnlock};
                                        var watch=Stopwatch.StartNew(); job.Schedule(count,batch).Complete(); watch.Stop();
                                        if (repeat>=0) { sample.ms[repeat]=watch.Elapsed.TotalMilliseconds; }
                                        for (var i=0;i<count;++i) {
                                            var expected=i%5==0;
                                            if (visits[i]!=(expected?1:0) || locks[i*stride]!=0 ||
                                                entities[i].Has<IsTransformStaticLocalCalculatedComponent>()!=(expected && operation<=1) ||
                                                bits.IsSet(in world.state.ptr->allocator,i)!=(expected && operation==2)) {
                                                Assert.Fail($"Invalid {names[operation]} result at {i}");
                                            }
                                        }
                                    }
                                    TestContext.WriteLine($"{sample.operation}; batch={batch}; stride={stride}; median={Median(sample.ms):F4}ms");
                                }
                            }
                        }
                        var report=new TagCostReport {count=count,operations=(count+4)/5,device=SystemInfo.graphicsDeviceName,
                            unity=Application.unityVersion,profilerEnabled=UnityEngine.Profiling.Profiler.enabled,
                            workers=Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount,samples=samples.ToArray()};
                        File.WriteAllText("Temp/becs-tag-synchronization.json",JsonUtility.ToJson(report,true));
                    }
                } finally { bits.Dispose(ref world.state.ptr->allocator); }
            } finally { world.Dispose(); }
        }
        private static Ent Create(World world,int i) {
            var e=Ent.New(world);
            e.Set(new LocalPositionComponent {value=new float3((sfloat)(i%11-5),(sfloat)0.25f,(sfloat)(-0.5f))});
            e.Set(new LocalRotationComponent {value=new quaternion(math.normalize(new float4((sfloat)0.2f,(sfloat)(-0.3f),(sfloat)(i%2==0?0.6f:-0.6f),(sfloat)0.8f)))});
            e.Set(new LocalScaleComponent {value=new float3((sfloat)(i%3==0?-1f:1f),(sfloat)0.5f,(sfloat)2f)});
            e.Set(new LocalMatrixComponent()); e.Set(new WorldMatrixComponent()); e.Set(new DirtyMoveComponent {tick=ulong.MaxValue});
            if (i%5==0) { e.SetTag<IsTransformStaticLocalComponent>(true); }
            return e;
        }
        private static void Reset(Ent[] entities) {
            foreach (var e in entities) {
                e.Get<LocalMatrixComponent>().value=default; e.Get<WorldMatrixComponent>().value=default;
                e.Get<WorldMatrixComponent>().isTickCalculated=false;
                e.Get<DirtyMoveComponent>().tick=ulong.MaxValue;
                e.SetTag<IsTransformStaticLocalCalculatedComponent>(false);
            }
        }
        [BurstCompile(CompileSynchronously = true)]
        private struct CpuTransformLocal : IJobParallelFor {
            [ReadOnly] public NativeArray<Ent> entities;
            public void Execute(int i) {
                var a=this.entities[i].GetAspect<TransformAspect>();
                Transform3DExt.CalculateLocalMatrixAndMarkDirty(in a);
            }
        }
        private static void CpuSplitLocal(World world) {
            var results=API.Query(world).Without<GpuExcluded>().WithAspect<TransformAspect>()
                .Without<IsTransformStaticCalculatedComponent>().Without<IsTransformStaticLocalCalculatedComponent>().ToArrayOnDemand();
            try {
                var entities=results.GetResults();
                if (entities.Length>0) {
                    var handle=new CpuTransformLocal {entities=entities}.Schedule(entities.Length,64);
                    try { handle=new GpuTransformStaticTags {entities=entities}.Schedule(handle); }
                    finally { handle.Complete(); }
                }
            } finally { if (results.IsCreated==true) { results.Dispose(); } }
        }
        private static void CpuLocal(World world) {
            var a=API.Query(world).Without<GpuExcluded>().Without<IsTransformStaticCalculatedComponent>().Without<IsTransformStaticLocalCalculatedComponent>()
                .Without<IsTransformStaticLocalComponent>().AsParallel().AsUnsafe().Schedule<TransformWorldMatrixUpdateSystem.CalculateLocalMatrixJob,TransformAspect>();
            var b=API.Query(world).Without<GpuExcluded>().Without<IsTransformStaticCalculatedComponent>().Without<IsTransformStaticLocalCalculatedComponent>()
                .With<IsTransformStaticLocalComponent>().AsParallel().AsUnsafe().Schedule<TransformWorldMatrixUpdateSystem.CalculateLocalMatrixStaticJob,TransformAspect>();
            JobHandle.CombineDependencies(a,b).Complete();
        }
        private static void EqualBits(float4x4 a,float4x4 b) {
            for (var c=0;c<4;++c) {
                for (var r=0;r<4;++r) {
                    if (a[c][r].RawValue != b[c][r].RawValue) {
                        Assert.Fail($"matrix {c}/{r}: {a[c][r].RawValue:X8} != {b[c][r].RawValue:X8}");
                    }
                }
            }
        }
        private static uint Next(ref uint x) { x^=x<<13; x^=x>>17; x^=x<<5; return x; }
        private static double Median(double[] a) { var copy=(double[])a.Clone(); Array.Sort(copy); return copy[copy.Length/2]; }
    }
}
#endif
