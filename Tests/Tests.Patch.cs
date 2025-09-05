using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using static Cuts;

    [Unity.Burst.BurstCompileAttribute]
    public unsafe class Tests_Patch {

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
        public void Serialization() {
            
            var prop = WorldProperties.Default;
            prop.stateProperties.entitiesCapacity = 10_000;
            var worldSource = World.Create(prop);
            {
                var ent = Ent.New(worldSource);
                ent.Set(new TestComponent() { data = 100200 });
            }

            var worldDest = World.Create(prop);
            {
                var ent = Ent.New(worldDest);
                ent.Set(new TestComponent() { data = 100500 });
            }

            var src = worldSource.Serialize();
            var dest = worldDest.Serialize();
            var diff = ME.BECS.Patch.GetDiff(new StreamBufferReader(src), new StreamBufferReader(dest));
            var bytes = diff.Serialize();
            var patch = new Patch(bytes);
            patch.data.MoveTo(0u);
            var ptr = patch.data.GetPointer();
            for (uint i = 0; i < patch.newLength; ++i) {
                Assert.IsTrue(ptr[i] == bytes[i]);
            }

        }

        [Test]
        public void Patch() {

            var prop = WorldProperties.Default;
            prop.stateProperties.entitiesCapacity = 10_000;
            var worldSource = World.Create(prop);
            {
                var ent = Ent.New(worldSource);
                ent.Set(new TestComponent() { data = 100200 });
            }

            var worldDest = World.Create(prop);
            {
                var ent = Ent.New(worldDest);
                ent.Set(new TestComponent() { data = 100200 });
            }

            var src = worldSource.Serialize();
            var dest = worldDest.Serialize();
            var diff = ME.BECS.Patch.GetDiff(new StreamBufferReader(src), new StreamBufferReader(dest));
            {
                Assert.IsTrue(diff.deltaCount == 0);
            }
            diff.Dispose();
            
            worldSource.Dispose();
            worldDest.Dispose();

        }

        [Unity.Burst.BurstCompileAttribute]
        public static void PatchBurstDiff(in StreamBufferReader src, in StreamBufferReader dest, ref Patch patch) {
            patch = ME.BECS.Patch.GetDiff(src, dest);
        }
        
        [Test]
        public void PatchBurst() {

            var prop = WorldProperties.Default;
            prop.stateProperties.entitiesCapacity = 10_000;
            var worldSource = World.Create(prop);
            {
                var ent = Ent.New(worldSource);
                ent.Set(new TestComponent() { data = 100200 });
            }

            var worldDest = World.Create(prop);
            {
                var ent = Ent.New(worldDest);
                ent.Set(new TestComponent() { data = 100200 });
            }

            var src = worldSource.Serialize();
            var dest = worldDest.Serialize();
            var diff = new Patch();
            PatchBurstDiff(new StreamBufferReader(src), new StreamBufferReader(dest), ref diff);
            {
                Assert.IsTrue(diff.deltaCount == 0);
            }
            diff.Dispose();
            
            worldSource.Dispose();
            worldDest.Dispose();

        }

        [Test]
        public void PatchArr() {

            {
                var srcArr  = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, };
                var destArr = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, };

                var src = new StreamBufferReader(srcArr);
                var dest = new StreamBufferReader(destArr);
                var diff = ME.BECS.Patch.GetDiff(src, dest);
                {
                    Assert.IsTrue(diff.deltaCount == 0u);
                    Assert.IsTrue(diff.tailLength == 0u);
                }
                diff.Dispose();
            }
            {
                var srcArr  = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                var destArr = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

                var src = new StreamBufferReader(srcArr);
                var dest = new StreamBufferReader(destArr);
                var diff = ME.BECS.Patch.GetDiff(src, dest);
                {
                    Assert.IsTrue(diff.deltaCount == 0u);
                    Assert.IsTrue(diff.tailLength == 2u);
                }
                diff.Dispose();
            }
            {
                var srcArr  = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var destArr = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                var src = new StreamBufferReader(srcArr);
                var dest = new StreamBufferReader(destArr);
                var diff = ME.BECS.Patch.GetDiff(src, dest);
                {
                    Assert.IsTrue(diff.deltaCount == 0u);
                    Assert.IsTrue(diff.tailLength == 1u);
                }
                diff.Dispose();
            }
            {
                var srcArr  = new byte[] { 2, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var destArr = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                var src = new StreamBufferReader(srcArr);
                var dest = new StreamBufferReader(destArr);
                var diff = ME.BECS.Patch.GetDiff(src, dest);
                {
                    Assert.IsTrue(diff.deltaCount == 1u);
                    Assert.IsTrue(diff.tailLength == 1u);
                }
                diff.Dispose();
            }
            {
                var srcArr  = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, };
                var destArr = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                var src = new StreamBufferReader(srcArr);
                var dest = new StreamBufferReader(destArr);
                var diff = ME.BECS.Patch.GetDiff(src, dest);
                {
                    Assert.IsTrue(diff.deltaCount == 0u);
                    Assert.IsTrue(diff.newLength == 25u);
                    Assert.IsTrue(diff.tailLength == 25u);
                }
                diff.Dispose();
            }
            {
                var srcArr  = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var destArr = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1 };

                var src = new StreamBufferReader(srcArr);
                var dest = new StreamBufferReader(destArr);
                var diff = ME.BECS.Patch.GetDiff(src, dest);
                {
                    Assert.IsTrue(diff.deltaCount == 1u);
                    Assert.IsTrue(diff.tailLength == 1u);
                    Assert.IsTrue(diff.newLength == 33u);
                }
                diff.Dispose();
            }

        }

    }

}