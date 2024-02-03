using System.Linq;
using NUnit.Framework;

namespace ME.BECS.Tests {

    public unsafe class Tests_Collections_BitArray {

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            AllTests.Start();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            AllTests.Dispose();
            yield return null;
        }

        [Test]
        public void NotContainsAll() {
            
            {
                var bits = new TempBitArray(10, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(4, true);
                bits.Set(5, true);
                bits.Set(6, true);

                using var w = World.Create();
                var bits2 = new BitArray(ref w.state->allocator, 100u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.NotContainsAll(in w.state->allocator, bits));
            }
            {
                var bits = new TempBitArray(10, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(3, true);
                bits.Set(5, true);
                bits.Set(6, true);

                using var w = World.Create();
                var bits2 = new BitArray(ref w.state->allocator, 100u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.NotContainsAll(in w.state->allocator, bits) == false);
            }
            {
                var bits = new TempBitArray(200, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(3, true);
                bits.Set(5, true);
                bits.Set(6, true);
                bits.Set(180, true);

                using var w = World.Create();
                var bits2 = new BitArray(ref w.state->allocator, 100u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.NotContainsAll(in w.state->allocator, bits) == false);
            }

        }

        [Test]
        public void ContainsAll() {

            {
                var bits = new TempBitArray(200, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(1, true);
                bits.Set(2, true);
                bits.Set(3, true);
                bits.Set(64, true);
                bits.Set(88, true);

                using var w = World.Create();
                var bits2 = new BitArray(ref w.state->allocator, 100u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.ContainsAll(in w.state->allocator, bits));
            }
            {
                var bits = new TempBitArray(200, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(1, true);
                bits.Set(2, true);

                using var w = World.Create();
                var bits2 = new BitArray(ref w.state->allocator, 100u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.ContainsAll(in w.state->allocator, bits));
            }
            {
                var bits = new TempBitArray(200, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(1, true);
                bits.Set(2, true);
                bits.Set(180, true);

                using var w = World.Create();
                var bits2 = new BitArray(ref w.state->allocator, 100u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.ContainsAll(in w.state->allocator, bits) == false);
            }
            {
                var bits = new TempBitArray(10, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(1, true);
                bits.Set(2, true);

                using var w = World.Create();
                var bits2 = new BitArray(ref w.state->allocator, 100u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.ContainsAll(in w.state->allocator, bits));
            }
            {
                var bits = new TempBitArray(10, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(1, true);
                bits.Set(2, true);

                using var w = World.Create();
                var bitsOther = new BitArray(ref w.state->allocator, 100u);
                bitsOther.Set(in w.state->allocator, 3, true);
                bitsOther.Set(in w.state->allocator, 88, true);
                
                var bits2 = new BitArray(ref w.state->allocator, 100u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.ContainsAll(in w.state->allocator, bitsOther, bits));
            }
            {
                var bits = new TempBitArray(200, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(1, true);
                bits.Set(2, true);
                bits.Set(180, true);

                using var w = World.Create();
                var bitsOther = new BitArray(ref w.state->allocator, 300u);
                bitsOther.Set(in w.state->allocator, 3, true);
                bitsOther.Set(in w.state->allocator, 88, true);
                bitsOther.Set(in w.state->allocator, 280, true);
                
                var bits2 = new BitArray(ref w.state->allocator, 90u);
                bits2.Set(in w.state->allocator, 1, true);
                bits2.Set(in w.state->allocator, 2, true);
                bits2.Set(in w.state->allocator, 3, true);
                bits2.Set(in w.state->allocator, 64, true);
                bits2.Set(in w.state->allocator, 88, true);
                Assert.IsTrue(bits2.ContainsAll(in w.state->allocator, bitsOther, bits) == false);
            }
            {
                var bits = new TempBitArray(100, allocator: Constants.ALLOCATOR_TEMP);
                bits.Set(24, true);
                bits.Set(25, true);
                bits.Set(27, true);
                bits.Set(31, true);

                using var w = World.Create();
                var bitsEmpty = new BitArray(ref w.state->allocator, 48);
                
                var bitsOther = new BitArray(ref w.state->allocator, 96);
                bitsOther.Set(in w.state->allocator, 24, true);
                bitsOther.Set(in w.state->allocator, 25, true);
                bitsOther.Set(in w.state->allocator, 27, true);
                bitsOther.Set(in w.state->allocator, 31, true);
                
                Assert.IsTrue(bitsOther.ContainsAll(in w.state->allocator, bitsEmpty, bits));
            }

        }

        [Test]
        public void Intersect() {

            var bits = new TempBitArray(100, allocator: Constants.ALLOCATOR_TEMP);
            bits.Set(1, true);
            bits.Set(2, true);
            bits.Set(3, true);
            bits.Set(64, true);
            bits.Set(88, true);

            using var w = World.Create();
            var bits2 = new BitArray(ref w.state->allocator, 45u);
            bits2.Set(in w.state->allocator, 1, true);
            bits2.Set(in w.state->allocator, 2, true);
            bits.Intersect(in w.state->allocator, bits2);

            Assert.IsTrue(bits.IsSet(1));
            Assert.IsTrue(bits.IsSet(2));
            Assert.IsTrue(bits.IsSet(3) == false);
            Assert.IsTrue(bits.IsSet(64) == false);
            Assert.IsTrue(bits.IsSet(88) == false);

        }

        [Test]
        public void Union() {

            var bits = new TempBitArray(100, allocator: Constants.ALLOCATOR_TEMP);
            bits.Set(1, true);
            bits.Set(2, true);
            bits.Set(3, true);
            bits.Set(64, true);
            bits.Set(88, true);

            using var w = World.Create();
            var bits2 = new BitArray(ref w.state->allocator, 45u);
            bits2.Set(in w.state->allocator, 2, true);
            bits2.Set(in w.state->allocator, 4, true);
            bits.Union(in w.state->allocator, bits2);

            Assert.IsTrue(bits.IsSet(1));
            Assert.IsTrue(bits.IsSet(2));
            Assert.IsTrue(bits.IsSet(3));
            Assert.IsTrue(bits.IsSet(4));
            Assert.IsTrue(bits.IsSet(64));
            Assert.IsTrue(bits.IsSet(88));

        }

        [Test]
        public void Remove() {

            var bits = new TempBitArray(100, allocator: Constants.ALLOCATOR_TEMP);
            bits.Set(1, true);
            bits.Set(2, true);
            bits.Set(3, true);
            bits.Set(64, true);
            bits.Set(88, true);

            using var w = World.Create();
            var bits2 = new BitArray(ref w.state->allocator, 45u);
            bits2.Set(in w.state->allocator, 2, true);
            bits2.Set(in w.state->allocator, 4, true);
            bits.Remove(in w.state->allocator, bits2);

            Assert.IsTrue(bits.IsSet(1));
            Assert.IsTrue(bits.IsSet(2) == false);
            Assert.IsTrue(bits.IsSet(3));
            Assert.IsTrue(bits.IsSet(4) == false);
            Assert.IsTrue(bits.IsSet(64));
            Assert.IsTrue(bits.IsSet(88));

        }

    }

    public unsafe class Tests_Collections_HashSet {

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            AllTests.Start();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            AllTests.Dispose();
            yield return null;
        }

        [Test]
        public void Add() {

            var world = World.Create();
            {
                var hs = new UIntHashSet(ref world.state->allocator, 1u);
                for (int i = 0; i < 100; ++i) {
                    hs.Add(ref world.state->allocator, (uint)i);
                }
                for (int i = 0; i < 100; ++i) {
                    hs.Remove(ref world.state->allocator, (uint)i);
                }
                hs.Add(ref world.state->allocator, 21);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 24);
                hs.Add(ref world.state->allocator, 25);
                hs.Add(ref world.state->allocator, 26);
                hs.Add(ref world.state->allocator, 28);
                hs.Add(ref world.state->allocator, 61);
                hs.Add(ref world.state->allocator, 63);
                hs.Add(ref world.state->allocator, 63);
                hs.Add(ref world.state->allocator, 70);
                hs.Add(ref world.state->allocator, 22);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 24);
                hs.Add(ref world.state->allocator, 25);
                hs.Add(ref world.state->allocator, 26);
                hs.Add(ref world.state->allocator, 28);
                hs.Add(ref world.state->allocator, 61);
                hs.Add(ref world.state->allocator, 63);
                hs.Add(ref world.state->allocator, 63);
                hs.Add(ref world.state->allocator, 70);
                hs.Add(ref world.state->allocator, 22);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 68);
                Assert.IsTrue(hs.Contains(in world.state->allocator, 21));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 23));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 24));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 25));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 26));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 28));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 69));
                Assert.IsTrue(hs.Remove(ref world.state->allocator, 69));
                Assert.IsFalse(hs.Contains(in world.state->allocator, 69));
            }
            world.Dispose();

        }

        [Test]
        public void Copy() {

            var world = World.Create();
            {
                var hs2 = new UIntHashSet(ref world.state->allocator, 1u);
                var hs = new UIntHashSet(ref world.state->allocator, 1u);
                for (int i = 0; i < 100; ++i) {
                    hs.Add(ref world.state->allocator, (uint)i);
                }
                for (int i = 0; i < 100; ++i) {
                    hs.Remove(ref world.state->allocator, (uint)i);
                }
                hs.Add(ref world.state->allocator, 21);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 24);
                hs.Add(ref world.state->allocator, 25);
                hs.Add(ref world.state->allocator, 26);
                hs.Add(ref world.state->allocator, 28);
                hs.Add(ref world.state->allocator, 61);
                hs.Add(ref world.state->allocator, 63);
                hs.Add(ref world.state->allocator, 63);
                hs.Add(ref world.state->allocator, 70);
                hs.Add(ref world.state->allocator, 22);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 23);
                hs.Add(ref world.state->allocator, 24);
                hs.Add(ref world.state->allocator, 25);
                hs.Add(ref world.state->allocator, 26);
                hs.Add(ref world.state->allocator, 28);
                hs.Add(ref world.state->allocator, 61);
                hs.Add(ref world.state->allocator, 63);
                hs.Add(ref world.state->allocator, 63);
                hs.Add(ref world.state->allocator, 70);
                hs.Add(ref world.state->allocator, 22);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 69);
                hs.Add(ref world.state->allocator, 68);
                Assert.IsTrue(hs.Contains(in world.state->allocator, 21));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 23));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 24));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 25));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 26));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 28));
                Assert.IsTrue(hs.Contains(in world.state->allocator, 69));
                Assert.IsTrue(hs.Remove(ref world.state->allocator, 69));
                Assert.IsFalse(hs.Contains(in world.state->allocator, 69));
                hs2.CopyFrom(ref world.state->allocator, hs);
                hs2.Add(ref world.state->allocator, 126);
                hs2.Add(ref world.state->allocator, 128);
                hs2.Add(ref world.state->allocator, 161);
                hs2.Add(ref world.state->allocator, 169);
                hs2.Add(ref world.state->allocator, 128);
                hs2.Add(ref world.state->allocator, 168);
                hs2.Add(ref world.state->allocator, 123);
                hs2.Add(ref world.state->allocator, 163);
                hs2.Add(ref world.state->allocator, 163);
                hs2.Add(ref world.state->allocator, 170);
                hs2.Add(ref world.state->allocator, 121);
                hs2.Add(ref world.state->allocator, 122);
                hs2.Add(ref world.state->allocator, 169);
                hs2.Add(ref world.state->allocator, 126);
                hs2.Add(ref world.state->allocator, 169);
                hs2.Add(ref world.state->allocator, 124);
                hs2.Add(ref world.state->allocator, 125);
                Assert.IsTrue(hs2.Contains(in world.state->allocator, 121));
                Assert.IsTrue(hs2.Contains(in world.state->allocator, 123));
                Assert.IsTrue(hs2.Contains(in world.state->allocator, 124));
                Assert.IsTrue(hs2.Contains(in world.state->allocator, 125));
                Assert.IsTrue(hs2.Contains(in world.state->allocator, 126));
                Assert.IsTrue(hs2.Contains(in world.state->allocator, 128));
                Assert.IsTrue(hs2.Contains(in world.state->allocator, 169));
                Assert.IsTrue(hs2.Remove(ref world.state->allocator, 169));
                Assert.IsFalse(hs2.Contains(in world.state->allocator, 169));
            }
            world.Dispose();

        }

        [Test]
        public void Equals() {

            var world = World.Create();
            {
                var seed = UnityEngine.Random.Range(0, 9999999);
                UnityEngine.Random.InitState(seed);
                var list = new System.Collections.Generic.List<uint>();
                var hs = new UIntHashSet(ref world.state->allocator, 1u);
                for (int i = 0; i < 100; ++i) {
                    list.Add((uint)UnityEngine.Random.Range(0, 100));
                }

                foreach (var item in list) {
                    hs.Add(ref world.state->allocator, item);
                }

                list = list.OrderBy(x => UnityEngine.Random.value).ToList();
                
                var hs2 = new UIntHashSet(ref world.state->allocator, 1u);
                foreach (var item in list) {
                    hs2.Add(ref world.state->allocator, item);
                }

                Assert.IsTrue(hs.Equals(in world.state->allocator, hs2));
                
            }
            world.Dispose();

        }

    }
    
}