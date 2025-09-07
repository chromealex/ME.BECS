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
        
        [Unity.Burst.BurstCompileAttribute]
        public static bool TestBurst(ulong a1, ulong a2, ulong a3, ulong a4, ulong b1, ulong b2, ulong b3, ulong b4) {
            var res = false;
            for (int i = 0; i < 100000; ++i) {
                if (a1 == b1 &&
                    a2 == b2 &&
                    a3 == b3 &&
                    a4 == b4) {
                    res = true;
                }
            }

            return res;
        }

        [Unity.Burst.BurstCompileAttribute]
        public static bool Test2Burst(ref Unity.Burst.Intrinsics.v256 a1, ref Unity.Burst.Intrinsics.v256 b1) {
            var res = false;
            for (int i = 0; i < 100000; ++i) {
                res = ME.BECS.Patch.Equals(ref a1, ref b1);
            }
            return res;
        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void PerformanceTest() {

            ulong a1 = 123UL;
            ulong a2 = 123UL;
            ulong a3 = 123UL;
            ulong a4 = 123UL;
            
            ulong b1 = 123UL;
            ulong b2 = 123UL;
            ulong b3 = 123UL;
            ulong b4 = 123UL;
            
            Unity.PerformanceTesting.Measure.Method(() => {

                TestBurst(a1, a2, a3, a4, b1, b2, b3, b4);

            }).MeasurementCount(10).IterationsPerMeasurement(50).WarmupCount(10).Run();

        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void PerformanceTestBurst() {

            Unity.Burst.Intrinsics.v256 a1 = default;
            Unity.Burst.Intrinsics.v256 b1 = default;
            
            Unity.PerformanceTesting.Measure.Method(() => {
                
                Test2Burst(ref a1, ref b1);
                
            }).MeasurementCount(10).IterationsPerMeasurement(50).WarmupCount(10).Run();

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

        [Test]
        public void ApplyPatch() {

            var prop = WorldProperties.Default;
            prop.stateProperties.entitiesCapacity = 10_000;
            var worldSource = World.Create(prop);
            {
                var ent = Ent.New(worldSource);
                ent.Set(new TestComponent() { data = 100200 });
            }

            var worldDest = World.Create(prop);
            Ent targetEnt = default;
            {
                var ent = Ent.New(worldDest);
                ent.Set(new TestComponent() { data = 100500 });
                targetEnt = ent;
            }

            var src = worldSource.Serialize();
            var dest = worldDest.Serialize();
            var diff = ME.BECS.Patch.GetDiff(new StreamBufferReader(src), new StreamBufferReader(dest));
            Assert.IsTrue(targetEnt.Read<TestComponent>().data == 100500);
            ME.BECS.Patch.Apply(in diff, worldDest.state);
            Assert.IsTrue(targetEnt.Read<TestComponent>().data == 100200);
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