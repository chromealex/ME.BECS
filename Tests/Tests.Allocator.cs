using System.Linq;
using NUnit.Framework;
using Unity.Jobs;
using static ME.BECS.Cuts;

namespace ME.BECS.Tests {

    public partial class Tests_Allocator {

        private ME.BECS.MemoryAllocator allocator;
        
        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            this.allocator.Initialize(10u, 512u, Unity.Collections.Allocator.Persistent, ignoreSizeRestrictions: true);
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            this.allocator.Dispose();
            yield return null;
        }

#if LEAK_DETECTION && LEAK_DETECTION_COUNTER
        [Unity.Burst.BurstCompile(CompileSynchronously = true)]
        private struct NestedAllocatorTagsJob : IJob {
            public void Execute() {
                for (var i = 0; i < 32; ++i) {
                    using (var root = new AllocatorTag(ALLOC_TAGS.COMPONENTS, 1u)) {
                        using (var child = new AllocatorTag(ALLOC_TAGS.COLLECTIONS, 2u)) {
                            using (var leaf = new AllocatorTag(ALLOC_TAGS.ENTITIES, 3u)) { }
                        }
                    }
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedAllocatorTagsReleaseTheirParentAllocations(bool burst) {
            AllocatorTag.tags.Data.Resize(JobUtils.ThreadsCount);
            var job = new NestedAllocatorTagsJob();
            if (burst == true) {
                Assert.AreEqual(true, Unity.Burst.BurstCompiler.Options.EnableBurstCompilation);
                job.Schedule().Complete();
            } else {
                job.Execute();
            }
            var before = LeakDetectorData.counter.Data.Get((int)ALLOCATOR);
            if (burst == true) {
                job.Schedule().Complete();
            } else {
                job.Execute();
            }
            Assert.AreEqual(before, LeakDetectorData.counter.Data.Get((int)ALLOCATOR));
        }

        [Test]
        public unsafe void NestedAllocatorTagsRestoreScopesAndKeepIndependentAllocationSnapshots() {
            var previous = AllocatorTag.Get();
            var allocation = default(MemPtr);
            try {
                using (var root = new AllocatorTag(ALLOC_TAGS.COMPONENTS, 1u)) {
                    using (var child = new AllocatorTag(ALLOC_TAGS.COLLECTIONS, 2u)) {
                        using (var leaf = new AllocatorTag(ALLOC_TAGS.ENTITIES, 3u)) {
                            allocation = this.allocator.Alloc(16u);
                        }
                        Assert.AreEqual(ALLOC_TAGS.COLLECTIONS.tag, AllocatorTag.Get().tagInfo.tag);
                        Assert.AreEqual(2u, AllocatorTag.Get().componentId);
                    }
                    Assert.AreEqual(ALLOC_TAGS.COMPONENTS.tag, AllocatorTag.Get().tagInfo.tag);
                    Assert.AreEqual(1u, AllocatorTag.Get().componentId);
                }
                Assert.AreEqual(previous.tagInfo.tag, AllocatorTag.Get().tagInfo.tag);
                Assert.AreEqual(previous.componentId, AllocatorTag.Get().componentId);
                var snapshot = LeakDetector.Find(this.allocator.GetUnsafePtr(allocation));
                Assert.AreEqual(ALLOC_TAGS.ENTITIES.tag, snapshot.tag.tagInfo.tag);
                Assert.AreEqual(3u, snapshot.tag.componentId);
                Assert.AreEqual(System.IntPtr.Zero, (System.IntPtr)snapshot.tag.parent.ptr);
            } finally {
                if (allocation != default) {
                    this.allocator.Free(allocation);
                }
            }
        }

        [Test]
        public unsafe void NullFreeDoesNotReportDoubleFreeOrChangeAllocationCounter() {
            var allocation = _make(16u, align: 16, allocator: Unity.Collections.Allocator.Persistent);
            var domainAllocation = _make(16u);
            try {
                var before = LeakDetectorData.counter.Data.Get((int)Unity.Collections.Allocator.Persistent);
                var domainBefore = LeakDetectorData.counter.Data.Get((int)ALLOCATOR);
                _free(default(safe_ptr), Unity.Collections.Allocator.Persistent);
                _free(default(safe_ptr<int>), Unity.Collections.Allocator.Persistent);
                _free(default(safe_ptr));
                _free(default(safe_ptr<int>));
                safe_ptr untyped = default;
                safe_ptr<int> typed = default;
                _free(ref untyped);
                _free(ref typed);
                Assert.AreEqual(System.IntPtr.Zero, (System.IntPtr)untyped.ptr);
                Assert.AreEqual(System.IntPtr.Zero, (System.IntPtr)typed.ptr);
                Assert.AreEqual(before, LeakDetectorData.counter.Data.Get((int)Unity.Collections.Allocator.Persistent));
                Assert.AreEqual(domainBefore, LeakDetectorData.counter.Data.Get((int)ALLOCATOR));
            } finally {
                _free(allocation, Unity.Collections.Allocator.Persistent);
                _free(domainAllocation);
            }
        }

        [Test]
        public unsafe void BootstrapReloadPreservesLiveAllocationTrackingAndCounters() {
            BootstrapRuntime.RequireInstalledPlan(editor: true);
            var persistent = _make(16u, align: 16, allocator: Unity.Collections.Allocator.Persistent);
            var domain = _make(16u, align: 16, allocator: Unity.Collections.Allocator.Domain);
            var persistentBeforeFree = 0;
            var domainBeforeFree = 0;
            try {
                for (var pass = 0; pass < 2; ++pass) {
                    BootstrapRuntime.LoadInstalled(editor: true);
                    using (var world = World.Create()) {
                        Assert.AreEqual(true, world.isCreated);
                    }
                    Assert.AreEqual((System.IntPtr)persistent.ptr, LeakDetector.Find(persistent).ptr);
                    Assert.AreEqual((System.IntPtr)domain.ptr, LeakDetector.Find(domain).ptr);
                }
            } finally {
                persistentBeforeFree = LeakDetectorData.counter.Data.Get((int)Unity.Collections.Allocator.Persistent);
                domainBeforeFree = LeakDetectorData.counter.Data.Get((int)Unity.Collections.Allocator.Domain);
                _free(persistent, Unity.Collections.Allocator.Persistent);
                _free(domain, Unity.Collections.Allocator.Domain);
            }
            Assert.AreEqual(persistentBeforeFree - 1, LeakDetectorData.counter.Data.Get((int)Unity.Collections.Allocator.Persistent));
            Assert.AreEqual(domainBeforeFree - 1, LeakDetectorData.counter.Data.Get((int)Unity.Collections.Allocator.Domain));
        }
#endif

        private void TestProxy(ME.BECS.AllocatorDebugProxy proxy, ME.BECS.AllocatorDebugProxy proxy2) {
            Assert.IsTrue(proxy.zones.Length == proxy2.zones.Length);
            for (var index = 0; index < proxy.zones.Length; ++index) {
                var zone = proxy.zones[index];
                var zone2 = proxy2.zones[index];
                for (int i = 0; i < zone.blocks.Length; ++i) {
                    var block = zone.blocks[i];
                    var block2 = zone2.blocks[i];
                    Assert.IsTrue(block == block2);
                }
            }
        }

        [Test]
        public void AllocArray() {
            var amount = 1000u;
            MemArray<uint> ptr1;
            MemArray<uint> ptr2;
            {
                var arr = ptr1 = new MemArray<uint>(ref this.allocator, amount);
                for (uint i = 0u; i < amount; ++i) {
                    arr[in this.allocator, i] = i;
                }

                for (uint i = 0u; i < amount; ++i) {
                    Assert.IsTrue(arr[in this.allocator, i] == i);
                }
            }
            {
                var arr = ptr2 = new MemArray<uint>(ref this.allocator, amount);
                for (uint i = 0u; i < amount; ++i) {
                    arr[in this.allocator, i] = i;
                }

                for (uint i = 0u; i < amount; ++i) {
                    Assert.IsTrue(arr[in this.allocator, i] == i);
                }
            }
            ptr1.Dispose(ref this.allocator);
            {
                for (uint i = 0u; i < amount; ++i) {
                    Assert.IsTrue(ptr2[in this.allocator, i] == i);
                }
            }
        }

        [Test]
        public void AllocList() {
            var amount = 10000u;
            {
                var arr = new List<uint>(ref this.allocator, 10);
                for (uint i = 0u; i < amount; ++i) {
                    arr.Add(ref this.allocator, i);
                }

                for (uint i = 0u; i < amount; ++i) {
                    Assert.IsTrue(arr[in this.allocator, i] == i);
                }
            }
        }

        [Test]
        public void Alloc() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void FreeBoth() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            var ptr2 = this.allocator.Alloc(100u);
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr2));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void FreeNext() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            var ptr2 = this.allocator.Alloc(100u);
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr2));
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void FreePrev() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            var ptr2 = this.allocator.Alloc(100u);
            this.allocator.CheckConsistency();
            var ptr3 = this.allocator.Alloc(100u);
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr2));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void Free() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(default));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void FillZone() {
            var amount = 18;
            for (int i = 0; i < amount; ++i) {
                this.allocator.Alloc(10u);
            }
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.freeBlocks[3].freeBlocks.Length == 0);
            this.allocator.CheckConsistency();
        }

        [Test]
        public void ReAlloc() {
            var ptr = this.allocator.Alloc(1000u);
            this.allocator.CheckConsistency();
            ptr = this.allocator.ReAlloc(ptr, 100u);
            this.allocator.CheckConsistency();
            ptr = this.allocator.ReAlloc(ptr, 2000u);
            this.allocator.CheckConsistency();
            this.allocator.Free(ptr);
            this.allocator.CheckConsistency();
        }

        [Test]
        public void ReAllocForward() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            var newPtr = this.allocator.ReAlloc(ptr, 20u);
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(newPtr));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(default));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void ReAllocFill() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            var newPtr = this.allocator.ReAlloc(ptr, 500u);
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(newPtr));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(default));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void LargeAlloc() {
            this.allocator.Alloc(100000u);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.freeBlocks[8].freeBlocks.Length == 1);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 2);
            this.allocator.CheckConsistency();
        }

        [Test]
        public void LargeAllocAndFree() {
            var ptr = this.allocator.Alloc(100000u);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.freeBlocks[8].freeBlocks.Length == 1);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 2);
            this.allocator.CheckConsistency();
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void AllocExact() {
            this.allocator.Alloc(512u);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.freeBlocks[8].freeBlocks.Length == 0);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 1);
            this.allocator.CheckConsistency();
        }

        [Test]
        public void FreeExactOneZone() {
            var ptr = this.allocator.Alloc(512u - 16u);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.freeBlocks[8].freeBlocks.Length == 0);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 1);
            this.allocator.CheckConsistency();
            this.allocator.Free(ptr);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.freeBlocks[8].freeBlocks.Length == 1);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 1);
            this.allocator.CheckConsistency();
        }

        [Test]
        public void StressTest() {
            var amount = 100_000;
            var items = new MemPtr[amount];
            for (int i = 0; i < amount; ++i) {
                var ptr = this.allocator.Alloc(10u);
                items[i] = ptr;
            }
            this.allocator.CheckConsistency();
            for (int i = 0; i < amount; ++i) {
                Assert.IsTrue(this.allocator.Free(items[i]), $"i = {i}");
            }
            this.allocator.CheckConsistency();
        }

        [Test]
        public void StressTestReverse() {
            var amount = 100_000;
            var items = new MemPtr[amount];
            for (int i = 0; i < amount; ++i) {
                var ptr = this.allocator.Alloc(10u);
                items[i] = ptr;
            }
            this.allocator.CheckConsistency();
            for (int i = amount - 1; i >= 0; --i) {
                Assert.IsTrue(this.allocator.Free(items[i]), $"i = {i}");
            }
            this.allocator.CheckConsistency();
        }

        [Test]
        public void StressTestRandom() {
            var amount = 100_000;
            var items = new MemPtr[amount];
            for (int i = 0; i < amount; ++i) {
                var ptr = this.allocator.Alloc(10u);
                items[i] = ptr;
            }
            this.allocator.CheckConsistency();
            items = items.OrderBy(x => UnityEngine.Random.value).ToArray();
            for (int i = 0; i < amount; ++i) {
                Assert.IsTrue(this.allocator.Free(items[i]), $"i = {i}");
            }
            this.allocator.CheckConsistency();
        }

        [Test]
        public void FreeMiddle() {
            var amount = 1000;
            var items = new MemPtr[amount];
            for (int i = 0; i < amount; ++i) {
                var ptr = this.allocator.Alloc(10u);
                items[i] = ptr;
            }
            this.allocator.CheckConsistency();
            UnityEngine.Random.InitState(1);
            items = items.OrderBy(x => UnityEngine.Random.value).ToArray();
            for (int i = 0; i < amount; ++i) {
                Assert.IsTrue(this.allocator.Free(items[i]), $"i = {i}");
            }
            this.allocator.CheckConsistency();
        }

        [Test]
        public void Serialize() {
            var amount = 1000;
            for (int i = 0; i < amount; ++i) {
                this.allocator.Alloc(10u);
            }
            this.allocator.CheckConsistency();
            var writer = new StreamBufferWriter(100u);
            this.allocator.Serialize(ref writer);
            this.allocator.CheckConsistency();
        }

        [Test]
        public void Deserialize() {
            var amount = 1000;
            for (int i = 0; i < amount; ++i) {
                this.allocator.Alloc(10u);
            }

            this.allocator.CheckConsistency();
            var writer = new StreamBufferWriter(100u);
            this.allocator.Serialize(ref writer);
            var prevAllocator = this.allocator;
            var proxy = new ME.BECS.AllocatorDebugProxy(this.allocator);
            this.allocator.CheckConsistency();
            
            var reader = new StreamBufferReader(writer.ToArray());
            this.allocator = new ME.BECS.MemoryAllocator(Unity.Collections.Allocator.Persistent);
            this.allocator.Deserialize(ref reader);
            var proxy2 = new ME.BECS.AllocatorDebugProxy(this.allocator);
            this.allocator.CheckConsistency();

            this.TestProxy(proxy, proxy2);
            
            prevAllocator.Dispose();
        }

        [Test]
        public void CopyFrom() {
            var amount = 1000;
            for (int i = 0; i < amount; ++i) {
                this.allocator.Alloc(10u);
            }
            this.allocator.CheckConsistency();
            
            var newAllocator = new ME.BECS.MemoryAllocator(Unity.Collections.Allocator.Persistent);
            newAllocator.CopyFrom(this.allocator);
            newAllocator.CheckConsistency();
            this.TestProxy(new ME.BECS.AllocatorDebugProxy(this.allocator), new ME.BECS.AllocatorDebugProxy(newAllocator));
            newAllocator.Dispose();
        }

        public unsafe partial struct JobAlloc : Unity.Jobs.IJobParallelFor {

            [Unity.Collections.NativeDisableParallelForRestrictionAttribute]
            public safe_ptr<ME.BECS.MemoryAllocator> allocator;
            [Unity.Collections.WriteOnlyAttribute]
            public Unity.Collections.NativeArray<MemPtr> results;

            public void Execute(int index) {
                this.results[index] = this.allocator.ptr->Alloc(10u);
            }

        }

        public unsafe partial struct JobFree : Unity.Jobs.IJobParallelFor {

            [Unity.Collections.NativeDisableParallelForRestrictionAttribute]
            public safe_ptr<ME.BECS.MemoryAllocator> allocator;
            [Unity.Collections.ReadOnlyAttribute]
            public Unity.Collections.NativeArray<MemPtr> results;

            public void Execute(int index) {
                this.allocator.ptr->Free(this.results[index]);
            }

        }

        [Test]
        public unsafe void ParallelAlloc() {
            var amount = 10_000;
            var r = _make(this.allocator);
            var results = new Unity.Collections.NativeArray<MemPtr>(amount, Unity.Collections.Allocator.TempJob);
            new JobAlloc() {
                allocator = r,
                results = results,
            }.Schedule(amount, 64).Complete();
            r.ptr->CheckConsistency();
            new JobFree() {
                allocator = r,
                results = results,
            }.Schedule(amount, 64).Complete();
            r.ptr->CheckConsistency();
            this.allocator = *r.ptr;
            _free(r);
            results.Dispose();
            
        }

    }

}
