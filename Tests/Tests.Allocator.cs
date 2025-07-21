using NUnit.Framework;

namespace ME.BECS.Tests {

    public class Tests_Allocator {

        private ME.BECS.Memory.Allocator allocator;
        
        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            this.allocator.Initialize(10u, 512u, Unity.Collections.Allocator.Persistent);
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            this.allocator.Dispose();
            yield return null;
        }

        private void TestProxy(ME.BECS.Memory.AllocatorDebugProxy proxy, ME.BECS.Memory.AllocatorDebugProxy proxy2) {
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
        public void Alloc() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(ptr));
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
        public void Realloc() {
            var ptr = this.allocator.Alloc(10u);
            this.allocator.CheckConsistency();
            var newPtr = this.allocator.ReAlloc(ptr, 20u);
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(ptr));
            this.allocator.CheckConsistency();
            Assert.IsTrue(this.allocator.Free(newPtr));
            this.allocator.CheckConsistency();
            Assert.IsFalse(this.allocator.Free(default));
            this.allocator.CheckConsistency();
        }

        [Test]
        public void FillZone() {
            var amount = 20;
            for (int i = 0; i < amount; ++i) {
                this.allocator.Alloc(10u);
            }
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.Length == 0);
        }

        [Test]
        public void LargeAlloc() {
            this.allocator.Alloc(1000u);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.Length == 1);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 2);
        }

        [Test]
        public void AllocExact() {
            this.allocator.Alloc(512u);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.Length == 0);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 1);
        }

        [Test]
        public void FreeExactOneZone() {
            var ptr = this.allocator.Alloc(512u - 16u);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.Length == 0);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 1);
            this.allocator.Free(ptr);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.freeBlocks.Length == 1);
            UnityEngine.Assertions.Assert.IsTrue(this.allocator.zonesCount == 1);
        }

        [Test]
        public void StressTest() {
            var amount = 100_000;
            var items = new MemPtr[amount];
            for (int i = 0; i < amount; ++i) {
                var ptr = this.allocator.Alloc(10u);
                items[i] = ptr;
            }
            for (int i = 0; i < amount; ++i) {
                Assert.IsTrue(this.allocator.Free(items[i]), $"i = {i}");
            }
        }

        [Test]
        public void Serialize() {
            var amount = 1000;
            for (int i = 0; i < amount; ++i) {
                this.allocator.Alloc(10u);
            }

            var writer = new StreamBufferWriter(100u);
            this.allocator.Serialize(ref writer);
        }

        [Test]
        public void Deserialize() {
            var amount = 1000;
            for (int i = 0; i < amount; ++i) {
                this.allocator.Alloc(10u);
            }

            var writer = new StreamBufferWriter(100u);
            this.allocator.Serialize(ref writer);
            var prevAllocator = this.allocator;
            var proxy = new ME.BECS.Memory.AllocatorDebugProxy(this.allocator);
            
            var reader = new StreamBufferReader(writer.ToArray());
            this.allocator = new ME.BECS.Memory.Allocator(Unity.Collections.Allocator.Persistent);
            this.allocator.Deserialize(ref reader);
            var proxy2 = new ME.BECS.Memory.AllocatorDebugProxy(this.allocator);

            this.TestProxy(proxy, proxy2);
            
            prevAllocator.Dispose();
        }

        [Test]
        public void CopyFrom() {
            var amount = 1000;
            for (int i = 0; i < amount; ++i) {
                this.allocator.Alloc(10u);
            }

            var newAllocator = new ME.BECS.Memory.Allocator(Unity.Collections.Allocator.Persistent);
            newAllocator.CopyFrom(this.allocator);
            this.TestProxy(new ME.BECS.Memory.AllocatorDebugProxy(this.allocator), new ME.BECS.Memory.AllocatorDebugProxy(newAllocator));
            newAllocator.Dispose();
        }

    }

}