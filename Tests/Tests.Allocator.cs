using System.Linq;
using NUnit.Framework;
using Unity.Jobs;
using static ME.BECS.Cuts;

namespace ME.BECS.Tests {

    public class Tests_Allocator {

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

        public unsafe struct JobAlloc : Unity.Jobs.IJobParallelFor {

            [Unity.Collections.NativeDisableParallelForRestrictionAttribute]
            public safe_ptr<ME.BECS.MemoryAllocator> allocator;
            [Unity.Collections.WriteOnlyAttribute]
            public Unity.Collections.NativeArray<MemPtr> results;

            public void Execute(int index) {
                this.results[index] = this.allocator.ptr->Alloc(10u);
            }

        }

        public unsafe struct JobFree : Unity.Jobs.IJobParallelFor {

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