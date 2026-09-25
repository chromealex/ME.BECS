using NUnit.Framework;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using static ME.BECS.Cuts;

namespace ME.BECS.Tests {
    public unsafe class Tests_JobEntityLimits {
        public struct RepeatedCreationJob : IJobForComponents<TestComponent> {
            private static void Create(in JobInfo info) => Ent.New(in info);

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TestComponent data) {
                Create(in jobInfo);
                Create(in jobInfo);
            }
        }

        [Test]
        public void SourceCountsIncludeEachCallToSharedHelper() {
            var rows = typeof(RepeatedCreationJob).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobEntityCounts.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\n'))
                .Single(summary => summary[0] == typeof(RepeatedCreationJob).FullName);
            Assert.IsTrue(rows.Skip(3).Select(row => row.Split('\t'))
                .Any(row => row.Length == 5 && row[0] == "C" && row[3] == "2" && row[4] == "0"),
                "Two calls to a helper creating one entity require two reservations");
            Assert.AreEqual("0", rows[2], "The initializer requires complete analysis");
            Assert.IsTrue(rows.Any(row => row.StartsWith("I\t") && row.EndsWith("\tApply\tv3")));
            AssertSourceInitializer<RepeatedCreationJob>(rows, 2u, 0u, 0u);
        }

        [EntitiesJobMaxCount(3)]
        public struct MixedLoopCreationJob : IJobForComponents<TestComponent> {
            private static void Create(in JobInfo info) => Ent.New(in info);

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TestComponent data) {
                Create(in jobInfo);
                for (var i = 0; i < data.data; ++i) Create(in jobInfo);
            }
        }

        [Test]
        public void SourceCountsRetainBothLoopContextsOfSharedHelper() {
            var rows = typeof(MixedLoopCreationJob).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobEntityCounts.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\n'))
                .Single(summary => summary[0] == typeof(MixedLoopCreationJob).FullName);
            Assert.IsTrue(rows.Skip(3).Select(row => row.Split('\t'))
                .Any(row => row.Length == 5 && row[0] == "C" && row[3] == "1" && row[4] == "1"),
                "The same creation helper must contribute both an inline and a repeating allocation site");
            Assert.AreEqual("0", rows[2], "The initializer requires complete analysis");
            Assert.IsTrue(rows.Any(row => row.StartsWith("I\t") && row.EndsWith("\tApply\tv3")));
            AssertSourceInitializer<MixedLoopCreationJob>(rows, 3u, 1u, 3u);
        }

        [Test]
        public void BootstrapInitializesSharedHelperJobs() {
            AllTests.Start();
            try {
                AssertBootstrapInitializesSharedHelperJobs();
            } finally {
                AllTests.Dispose();
            }
        }

        private static void AssertBootstrapInitializesSharedHelperJobs() {
            const string message = "Loaded Editor bootstrap has not initialized the current source entity counts. Regenerate the Editor bootstrap and wait for compilation; metadata comparison alone does not execute or regenerate it.";
            Assert.IsTrue(JobStaticInfo<RepeatedCreationJob>.inlineCount.ptr != null, message);
            Assert.AreEqual(2u, JobStaticInfo<RepeatedCreationJob>.inlineCount[EntityTypes<DefaultEntityType>.id], message);
            Assert.IsTrue(JobStaticInfo<MixedLoopCreationJob>.inlineCount.ptr != null, message);
            Assert.AreEqual(1u, JobStaticInfo<MixedLoopCreationJob>.loopCount, message);
            Assert.AreEqual(3u, JobStaticInfo<MixedLoopCreationJob>.entitiesMaxCount, message);
            Assert.AreEqual(3u, JobStaticInfo<MixedLoopCreationJob>.inlineCount[EntityTypes<DefaultEntityType>.id], message);
        }

        private static void AssertSourceInitializer<TJob>(string[] rows, uint inline, uint loops, uint maximum) where TJob : struct {
            // Unit-test the exported body independently of the currently loaded bootstrap.
            // Use a private one-group layout and restore every shared static afterwards.
            var records = rows.Where(row => row.StartsWith("I\t")).Select(row => row.Split('\t')).ToArray();
            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(5, records[0].Length);
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && assembly.FullName == records[0][1]).ToArray();
            Assert.AreEqual(1, assemblies.Length);
            var owner = assemblies[0].GetType(records[0][2], true);
            var apply = owner.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(apply);
            Assert.AreEqual(2, apply.GetParameters().Length, "These fixtures reserve exactly one entity group");
            var previousInline = JobStaticInfo<TJob>.inlineCount;
            var previousLoops = JobStaticInfo<TJob>.loopCount;
            var previousMaximum = JobStaticInfo<TJob>.entitiesMaxCount;
            JobStaticInfo<TJob>.inlineCount = default;
            try {
                apply.MakeGenericMethod(typeof(TJob)).Invoke(null, new object[] { 1u, 0u });
                Assert.IsTrue(JobStaticInfo<TJob>.inlineCount.ptr != null, "Source initializer must allocate reservation counts");
                Assert.AreEqual(inline, JobStaticInfo<TJob>.inlineCount[0u]);
                Assert.AreEqual(loops, JobStaticInfo<TJob>.loopCount);
                Assert.AreEqual(maximum, JobStaticInfo<TJob>.entitiesMaxCount);
            } finally {
                var allocated = JobStaticInfo<TJob>.inlineCount;
                JobStaticInfo<TJob>.inlineCount = previousInline;
                JobStaticInfo<TJob>.loopCount = previousLoops;
                JobStaticInfo<TJob>.entitiesMaxCount = previousMaximum;
                if (allocated.ptr != null) _free(allocated, Unity.Collections.Allocator.Domain);
            }
        }

        [EntitiesJobMaxCount(3)]
        [Unity.Burst.BurstCompile]
        public struct BoundedCreationJob : IJobForComponents<TestComponent> {
            [Unity.Collections.NativeDisableParallelForRestrictionAttribute]
            public Unity.Collections.NativeArray<uint> ids;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TestComponent data) {
                this.ids[data.data * 3] = Ent.New(in jobInfo).id;
                for (var i = 0; i < data.data % 3; ++i)
                    this.ids[data.data * 3 + i + 1] = Ent.New(in jobInfo).id;
            }
        }

        [Test]
        public void BoundedLoopsHaveDeterministicIdsAcrossBatchSizes() {
            AllTests.Start();
            try {
                CollectionAssert.AreEqual(RunBoundedCreation(1u), RunBoundedCreation(16u));
            } finally {
                AllTests.Dispose();
            }
        }

        private static uint[] RunBoundedCreation(uint batch) {
            const int count = 48;
            var world = World.Create();
            var ids = new Unity.Collections.NativeArray<uint>(count * 3, Unity.Collections.Allocator.TempJob);
            try {
                for (var i = 0; i < count; ++i) Ent.New().Set(new TestComponent { data = i });
                Batches.Apply(world);
                API.Query(world).AsParallel(batch).Schedule<BoundedCreationJob, TestComponent>(new BoundedCreationJob { ids = ids }).Complete();
                Assert.AreEqual(3u, JobStaticInfo<BoundedCreationJob>.entitiesMaxCount);
                Assert.AreEqual(3u, JobStaticInfo<BoundedCreationJob>.inlineCount[EntityTypes<DefaultEntityType>.id]);
                for (var i = 0; i < count; ++i)
                    for (var offset = 0; offset < 3; ++offset)
                        if (offset <= i % 3) Assert.AreNotEqual(0u, ids[i * 3 + offset]);
                        else Assert.AreEqual(0u, ids[i * 3 + offset]);
                return ids.ToArray();
            } finally {
                ids.Dispose();
                world.Dispose();
            }
        }

        [Test]
        public void MaximumMustBePositive() {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new EntitiesJobMaxCountAttribute(0));
            Assert.AreEqual(3u, new EntitiesJobMaxCountAttribute(3).count);
        }

        [Test]
        public void LimitIsExactAndResetsForEachInvocation() {
            var info = new JobInfo { entitiesMaxCount = 2u };
            info.CreateLocalCounter();
            try {
                info.CheckEntityLimit();
                // A copy passed through in JobInfo must share the current invocation's counter.
                var copy = info;
                copy.CheckEntityLimit();
                var exception = Assert.Throws<System.Exception>(() => info.CheckEntityLimit());
                StringAssert.Contains("EntitiesJobMaxCount exceeded", exception.Message);
                info.ResetLocalCounter();
                info.CheckEntityLimit();
                info.CheckEntityLimit();
                Assert.Throws<System.Exception>(() => info.CheckEntityLimit());
            } finally {
                _free(info.localOffsets, Constants.ALLOCATOR_TEMP);
            }
        }

        [Test]
        public void WorkerCountersAreIndependent() {
            var first = new JobInfo { entitiesMaxCount = 1u };
            var second = first;
            first.CreateLocalCounter();
            second.CreateLocalCounter();
            try {
                first.CheckEntityLimit();
                Assert.Throws<System.Exception>(() => first.CheckEntityLimit());
                Assert.DoesNotThrow(() => second.CheckEntityLimit());
            } finally {
                _free(first.localOffsets, Constants.ALLOCATOR_TEMP);
                _free(second.localOffsets, Constants.ALLOCATOR_TEMP);
            }
        }

        [Test]
        public void MissingAttributeMessageExplainsBound() {
            var exception = Assert.Throws<System.Exception>(() => E.THROW_ENT_NEW());
            StringAssert.Contains("EntitiesJobMaxCount(number)", exception.Message);
        }
    }
}
