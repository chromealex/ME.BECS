using NUnit.Framework;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using static ME.BECS.Cuts;

namespace ME.BECS.Tests {
    public unsafe class Tests_JobEntityLimits {
        [UnityEngine.TestTools.UnitySetUp]
        public System.Collections.IEnumerator SetUp() {
            AllTests.Start();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDown]
        public System.Collections.IEnumerator TearDown() {
            AllTests.Dispose();
            yield return null;
        }

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
            Assert.AreEqual(2u, JobStaticInfo<RepeatedCreationJob>.inlineCount[EntityTypes<DefaultEntityType>.id],
                "Bootstrap must select source counts, not the legacy visited-method count");
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
            Assert.AreEqual(1u, JobStaticInfo<MixedLoopCreationJob>.loopCount);
            Assert.AreEqual(3u, JobStaticInfo<MixedLoopCreationJob>.entitiesMaxCount);
            Assert.AreEqual(3u, JobStaticInfo<MixedLoopCreationJob>.inlineCount[EntityTypes<DefaultEntityType>.id]);
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
            CollectionAssert.AreEqual(RunBoundedCreation(1u), RunBoundedCreation(16u));
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
