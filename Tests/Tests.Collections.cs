using NUnit.Framework;

namespace ME.BECS.Tests {

    public unsafe class Tests_Collections_BitArray {

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

}