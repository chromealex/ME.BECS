using NUnit.Framework;

namespace ME.BECS.Tests {

    [Unity.Burst.BurstCompileAttribute]
    public class Tests_FlatQueries {

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            FullFillBits.Initialize();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            FullFillBits.Dispose();
            yield return null;
        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void TestFullFills1000Pages() {

            UnityEngine.Random.InitState(1);
            var amount = 64u * 1000u;
            var test = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>((int)amount, Unity.Collections.Allocator.Temp);
            var data = new TempBitArray(amount, ClearOptions.ClearMemory, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < amount; ++i) {
                var r = true;
                data.Set(i, r);
                test.Add((uint)i);
            }
            
            var res = data.GetTrueBitsTempFast();
            for (int i = 0; i < res.Length; ++i) {
                Assert.IsTrue(test[i] == res[i]);
            }

            Unity.PerformanceTesting.Measure.Method(() => {

                TestsBurst(ref data);
                
            }).MeasurementCount(10).IterationsPerMeasurement(50).Run();

        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void TestZeroFills1000Pages() {

            UnityEngine.Random.InitState(1);
            var amount = 64u * 1000u;
            var test = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>((int)amount, Unity.Collections.Allocator.Temp);
            var data = new TempBitArray(amount, ClearOptions.ClearMemory, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < amount; ++i) {
                var r = false;
                data.Set(i, r);
            }
            
            var res = data.GetTrueBitsTempFast();
            for (int i = 0; i < res.Length; ++i) {
                Assert.IsTrue(test[i] == res[i]);
            }

            Unity.PerformanceTesting.Measure.Method(() => {

                TestsBurst(ref data);
                
            }).MeasurementCount(10).IterationsPerMeasurement(50).Run();

        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void TestOnePage() {

            UnityEngine.Random.InitState(1);
            var amount = 64u;
            var test = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>((int)amount, Unity.Collections.Allocator.Temp);
            var data = new TempBitArray(amount, ClearOptions.ClearMemory, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < amount; ++i) {
                var r = UnityEngine.Random.Range(0, 2) == 0;
                data.Set(i, r);
                if (r == true) test.Add((uint)i);
            }
            
            var res = data.GetTrueBitsTempFast();
            for (int i = 0; i < res.Length; ++i) {
                Assert.IsTrue(test[i] == res[i]);
            }

            Unity.PerformanceTesting.Measure.Method(() => {

                TestsBurst(ref data);
                
            }).MeasurementCount(10).IterationsPerMeasurement(50).Run();

        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void TestRandomHalfZero1000Pages() {

            UnityEngine.Random.InitState(1);
            var amount = 64u * 1000u;
            var test = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>((int)amount, Unity.Collections.Allocator.Temp);
            var data = new TempBitArray(amount, ClearOptions.ClearMemory, Unity.Collections.Allocator.Temp);
            var k = 0;
            for (int i = 0; i < amount; ++i) {
                if (i % 64 == 0) {
                    ++k;
                }
                if (i % 64 == 0 && k % 2 == 0) {
                    // fill page with zeros
                    i += 64;
                } else {
                    var r = UnityEngine.Random.Range(0, 2);
                    data.Set(i, r == 0);
                    if (r == 0) test.Add((uint)i);
                }
            }
            
            var res = data.GetTrueBitsTempFast();
            for (int i = 0; i < res.Length; ++i) {
                Assert.IsTrue(test[i] == res[i]);
            }

            Unity.PerformanceTesting.Measure.Method(() => {

                TestsBurst(ref data);
                
            }).MeasurementCount(10).IterationsPerMeasurement(50).Run();

        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void TestRandom1000Pages() {

            UnityEngine.Random.InitState(1);
            var amount = 64u * 1000u;
            var test = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>((int)amount, Unity.Collections.Allocator.Temp);
            var data = new TempBitArray(amount, ClearOptions.ClearMemory, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < amount; ++i) {
                var r = UnityEngine.Random.Range(0, 2) == 0;
                data.Set(i, r);
                if (r == true) test.Add((uint)i);
            }
            
            var res = data.GetTrueBitsTempFast();
            for (int i = 0; i < res.Length; ++i) {
                Assert.IsTrue(test[i] == res[i]);
            }

            Unity.PerformanceTesting.Measure.Method(() => {

                TestsBurst(ref data);
                
            }).MeasurementCount(10).IterationsPerMeasurement(50).Run();

        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void TestRandom4Pages() {

            UnityEngine.Random.InitState(1);
            var amount = 64u * 4u;
            var test = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>((int)amount, Unity.Collections.Allocator.Temp);
            var data = new TempBitArray(amount, ClearOptions.ClearMemory, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < amount; ++i) {
                var r = UnityEngine.Random.Range(0, 2) == 0;
                data.Set(i, r);
                if (r == true) test.Add((uint)i);
            }
            
            var res = data.GetTrueBitsTempFast();
            for (int i = 0; i < res.Length; ++i) {
                Assert.IsTrue(test[i] == res[i]);
            }

            Unity.PerformanceTesting.Measure.Method(() => {

                TestsBurst(ref data);
                
            }).MeasurementCount(10).IterationsPerMeasurement(50).Run();

        }

        [Test, Unity.PerformanceTesting.PerformanceAttribute]
        public void TestRandom8Pages() {

            UnityEngine.Random.InitState(1);
            var amount = 64u * 8u;
            var test = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>((int)amount, Unity.Collections.Allocator.Temp);
            var data = new TempBitArray(amount, ClearOptions.ClearMemory, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < amount; ++i) {
                var r = UnityEngine.Random.Range(0, 2) == 0;
                data.Set(i, r);
                if (r == true) test.Add((uint)i);
            }
            
            var res = data.GetTrueBitsTempFast();
            for (int i = 0; i < res.Length; ++i) {
                Assert.IsTrue(test[i] == res[i]);
            }

            Unity.PerformanceTesting.Measure.Method(() => {

                TestsBurst(ref data);
                
            }).MeasurementCount(10).IterationsPerMeasurement(50).Run();

        }

        [Unity.Burst.BurstCompileAttribute]
        private static void TestsBurst(ref TempBitArray data) {
            
            var bits = data.GetTrueBitsTempFast();

        }

    }

}