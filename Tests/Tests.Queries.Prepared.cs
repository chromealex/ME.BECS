#pragma warning disable CS0618
using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {

    public unsafe partial class Tests_Queries {

        public struct PreparedRequired : IQueryDefinition {
            public void Build(ref FlatQueries.QueryCompose compose) {
                compose.With<TestComponent>();
                compose.With<TestComponent>();
            }
        }

        public struct PreparedOther : IQueryDefinition {
            public void Build(ref FlatQueries.QueryCompose compose) {
                compose.With<Test2Component>();
            }
        }

        public struct PreparedComplex : IQueryDefinition {
            public void Build(ref FlatQueries.QueryCompose compose) {
                compose.WithAspect<Aspect1>();
                compose.With<Test1Component>();
                compose.WithAny<Test3Component, Test4Component>();
                compose.Without<TestComponent>();
            }
        }

        private static void AssertPreparedResultsEqual(Unity.Collections.NativeArray<Ent> expected, Unity.Collections.NativeArray<Ent> actual) {
            Assert.AreEqual(expected.Length, actual.Length, "Query result count");
            for (int i = 0; i < expected.Length; ++i) {
                Assert.AreEqual(expected[i], actual[i], $"Entity at result index {i}");
            }
        }

        [Test]
        public void PreparedQueryObservesMembershipChangesAndStorageGrowth() {
            using var world = World.Create();
            var prepared = default(PreparedQuery<PreparedRequired>);
            try {
                var first = Ent.New();
                first.Set(new TestComponent());
                using (var result = prepared.Query(world, Batches.Apply(default, world)).ToArray()) {
                    CollectionAssert.AreEqual(new[] { first }, result);
                }
                first.Disable<TestComponent>();
                using (var result = prepared.Query(world, Batches.Apply(default, world)).ToArray()) {
                    Assert.AreEqual(0, result.Length);
                }
                first.Enable<TestComponent>();
                first.Set(new IsInactive());
                using (var result = prepared.Query(world, Batches.Apply(default, world)).ToArray()) {
                    Assert.AreEqual(0, result.Length);
                }
                first.Remove<IsInactive>();
                // Force entity/component storage to grow beyond the initial capacity.
                for (int i = 0; i < 4096; ++i) {
                    Ent.New().Set(new TestComponent());
                }
                first.Remove<TestComponent>();
                using (var expected = API.Query(world, Batches.Apply(default, world)).With<TestComponent>().ToArray())
                using (var actual = prepared.Query(world).ToArray()) {
                    AssertPreparedResultsEqual(expected, actual);
                    Assert.AreEqual(4096, actual.Length);
                }
                first.Destroy();
                var replacement = Ent.New();
                replacement.Set(new TestComponent());
                using (var expected = API.Query(world, Batches.Apply(default, world)).With<TestComponent>().ToArray())
                using (var actual = prepared.Query(world).ToArray()) {
                    AssertPreparedResultsEqual(expected, actual);
                }
            } finally {
                prepared.Dispose();
            }
        }

        [Test]
        public void PreparedQueryExtensionsDoNotMutateSharedFilter() {
            using var world = World.Create();
            var prepared = default(PreparedQuery<PreparedRequired>);
            try {
                var first = Ent.New();
                first.Set(new TestComponent());
                var second = Ent.New();
                second.Set(new TestComponent());
                second.Set(new Test2Component());
                Batches.Apply(default, world).Complete();
                var all = prepared.Query(world).ToArrayOnDemand();
                var excluded = prepared.Query(world).Without<Test2Component>().ToArrayOnDemand();
                var required = prepared.Query(world).With<Test2Component>().ToArrayOnDemand();
                var any = prepared.Query(world).WithAny<Test2Component, Test3Component>().ToArrayOnDemand();
                try {
                    CollectionAssert.AreEqual(new[] { first, second }, all.GetResults());
                    CollectionAssert.AreEqual(new[] { first }, excluded.GetResults());
                    CollectionAssert.AreEqual(new[] { second }, required.GetResults());
                    CollectionAssert.AreEqual(new[] { second }, any.GetResults());
                } finally {
                    all.Dispose();
                    excluded.Dispose();
                    required.Dispose();
                    any.Dispose();
                }
                using (var result = prepared.Query(world).ToArray()) {
                    CollectionAssert.AreEqual(new[] { first, second }, result);
                }
            } finally {
                prepared.Dispose();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreparedQueryGeneratedScheduleSupportsBothModes(bool parallel) {
            using var world = World.Create();
            var prepared = default(PreparedQuery<PreparedRequired>);
            try {
                var ent = Ent.New();
                ent.Set(new TestComponent());
                Assert.AreNotEqual(System.IntPtr.Zero, JobReflectionData<JobComponents>.data.Data, "Bootstrap must register the existing job before scheduling.");
                for (int i = 0; i < 2; ++i) {
                    var query = prepared.Query(world, Batches.Apply(default, world));
                    if (parallel == true) {
                        query.AsParallel();
                    }
                    query.Schedule<JobComponents, TestComponent>().Complete();
                }
                Assert.AreEqual(2, ent.Read<TestComponent>().data);
                // Reuse the registered production scheduling path to add a
                // component absent from another plan, without mutating that plan.
                var other = default(PreparedQuery<PreparedOther>);
                try {
                    ent.Set(new Test2Component());
                    var withoutJobComponent = Ent.New();
                    withoutJobComponent.Set(new Test2Component());
                    var query = other.Query(world, Batches.Apply(default, world));
                    if (parallel == true) {
                        query.AsParallel();
                    }
                    query.Schedule<JobComponents, TestComponent>().Complete();
                    Assert.AreEqual(3, ent.Read<TestComponent>().data);
                    using (var result = other.Query(world).ToArray()) {
                        CollectionAssert.AreEqual(new[] { ent, withoutJobComponent }, result);
                    }
                } finally {
                    other.Dispose();
                }
            } finally {
                prepared.Dispose();
            }
        }

        [Test]
        public void PreparedQueryComplexFilterStepAndInactiveMatchDynamic() {
            using var world = World.Create();
            var prepared = new PreparedQuery<PreparedComplex>(withInactive: true);
            try {
                for (int i = 0; i < 100; ++i) {
                    var ent = Ent.New();
                    ent.Set(new Test1Component());
                    ent.Set(new Test2Component());
                    if (i % 2 == 0) {
                        ent.Set(new Test3Component());
                    } else {
                        ent.Set(new Test4Component());
                    }
                    if (i % 3 == 0) {
                        ent.Set(new TestComponent());
                    }
                    if (i % 5 == 0) {
                        ent.Set(new IsInactive());
                    }
                }
                using (var expected = API.Query(world, Batches.Apply(default, world), withInactive: true)
                    .WithAspect<Aspect1>().WithAny<Test3Component, Test4Component>().Without<TestComponent>().Step(3u, 1u).ToArray())
                using (var actual = prepared.Query(world).WithAspect<Aspect1>().Step(3u, 1u).ToArray()) {
                    Assert.Greater(actual.Length, 0);
                    AssertPreparedResultsEqual(expected, actual);
                }
                var expectedCount = 0;
                foreach (var ent in API.Query(world, withInactive: true)
                    .WithAspect<Aspect1>().WithAny<Test3Component, Test4Component>().Without<TestComponent>()) {
                    ++expectedCount;
                }
                var count = 0;
                foreach (var ent in prepared.Query(world)) {
                    ++count;
                }
                Assert.AreEqual(66, expectedCount);
                Assert.AreEqual(expectedCount, count);
                using (var result = prepared.Query(world).ToArray()) {
                    Assert.AreEqual(66, result.Length);
                }
            } finally {
                prepared.Dispose();
            }
        }

        [TestCase(64)]
        [TestCase(4096)]
        [TestCase(32768)]
        public void PreparedQueryBenchmark(int entityCount) {
            using var world = World.Create();
            var prepared = default(PreparedQuery<PreparedComplex>);
            try {
                for (int i = 0; i < entityCount; ++i) {
                    var ent = Ent.New();
                    ent.Set(new Test1Component());
                    ent.Set(new Test2Component());
                    if (i % 4 == 0) {
                        ent.Set(new Test3Component());
                    }
                }
                Batches.Apply(default, world).Complete();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                prepared.Query(world).Dispose();
                var setupMs = timer.Elapsed.TotalMilliseconds;
                // Warm both paths before interleaved median measurements.
                PreparedBenchmarkLoop(in world, ref prepared, false, false, 100);
                PreparedBenchmarkLoop(in world, ref prepared, true, false, 100);
                PreparedBenchmarkLoop(in world, ref prepared, false, true, 5);
                PreparedBenchmarkLoop(in world, ref prepared, true, true, 5);
                foreach (var evaluate in new[] { false, true }) {
                    var baseline = new double[7];
                    var cached = new double[7];
                    var iterations = evaluate == true ? 20 : 500;
                    for (int sample = 0; sample < baseline.Length; ++sample) {
                        for (int pass = 0; pass < 2; ++pass) {
                            var usePrepared = (sample + pass) % 2 == 0;
                            timer.Restart();
                            PreparedBenchmarkLoop(in world, ref prepared, usePrepared, evaluate, iterations);
                            var ms = timer.Elapsed.TotalMilliseconds / iterations;
                            if (usePrepared == true) {
                                cached[sample] = ms;
                            } else {
                                baseline[sample] = ms;
                            }
                        }
                    }
                    System.Array.Sort(baseline);
                    System.Array.Sort(cached);
                    var measurement = $"PreparedQuery entities={entityCount} evaluate={evaluate} setup_ms={setupMs:F4} dynamic_us={baseline[3] * 1000:F3} prepared_us={cached[3] * 1000:F3} ratio={baseline[3] / cached[3]:F3}";
                    TestContext.WriteLine(measurement);
                    UnityEngine.Debug.Log(measurement);
                }
            } finally {
                prepared.Dispose();
            }
        }

        private static void PreparedBenchmarkLoop(in World world, ref PreparedQuery<PreparedComplex> prepared, bool usePrepared, bool evaluate, int iterations) {
            if (evaluate == false) {
                PreparedBenchmarkBuildLoop(in world, ref prepared, usePrepared, iterations);
                return;
            }
            // Schedule through the managed Unity entrypoint; the query evaluation job
            // itself is Burst compiled. Do not lazily initialize Unity job reflection
            // data from a Burst direct call in this isolated Editor benchmark.
            for (int i = 0; i < iterations; ++i) {
                var query = usePrepared == true ? prepared.Query(world) : API.Query(world)
                    .WithAspect<Aspect1>().With<Test1Component>().WithAny<Test3Component, Test4Component>().Without<TestComponent>();
                var result = query.ToArrayOnDemand();
                result.Dispose();
            }
        }

        [Unity.Burst.BurstCompile(CompileSynchronously = true)]
        private static void PreparedBenchmarkBuildLoop(in World world, ref PreparedQuery<PreparedComplex> prepared, bool usePrepared, int iterations) {
            for (int i = 0; i < iterations; ++i) {
                var query = usePrepared == true ? prepared.Query(world) : API.Query(world)
                    .WithAspect<Aspect1>().With<Test1Component>().WithAny<Test3Component, Test4Component>().Without<TestComponent>();
                query.Dispose();
            }
        }

    }
}
