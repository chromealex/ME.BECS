using System;
using System.Linq;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Inspect catalogs only: these constructors and systems must never execute.
        public static T ConstructGenericValue<T>() where T : new() => new T();
        public sealed class GenericConstructorWrites {
            public GenericConstructorWrites() { default(Ent).Set(new Test1Component()); }
        }
        public static int GenericConstructorInitializer() { default(Ent).Set(new Test2Component()); return 0; }
        public sealed class ImplicitGenericConstructorWrites {
            public int value = GenericConstructorInitializer();
        }
        public sealed class GenericConstructorCreates {
            public GenericConstructorCreates() { Ent.New(); }
        }
        public sealed class GenericConstructorSchedules {
            public GenericConstructorSchedules() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); }
        }
        public sealed class GenericConstructorUnknown {
            public GenericConstructorUnknown() { unknownAllocatorCallback(); }
        }
        public partial struct GenericConstructorWritesJob : IJob {
            public void Execute() => _ = ConstructGenericValue<GenericConstructorWrites>();
        }
        public partial struct ImplicitGenericConstructorJob : IJob {
            public void Execute() => _ = ConstructGenericValue<ImplicitGenericConstructorWrites>();
        }
        public partial struct RepeatedGenericConstructorJob : IJob {
            public void Execute() {
                _ = ConstructGenericValue<GenericConstructorCreates>();
                _ = ConstructGenericValue<GenericConstructorCreates>();
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct LoopGenericConstructorJob : IJob {
            public void Execute() {
                _ = ConstructGenericValue<GenericConstructorCreates>();
                for (var index = 0; index < 2; ++index) _ = ConstructGenericValue<GenericConstructorCreates>();
            }
        }
        public partial struct UnknownGenericConstructorJob : IJob {
            public void Execute() => _ = ConstructGenericValue<GenericConstructorUnknown>();
        }
        public partial struct GenericConstructorSchedulingSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = ConstructGenericValue<GenericConstructorSchedules>();
                default(Ent).Set(new TestComponent());
            }
        }
        public struct GenericAutoProperty<T> { public T Value { get; set; } }
        public partial struct GenericAutoPropertyPlainSystem : IUpdate {
            public GenericAutoProperty<int> data;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                this.data.Value = 1;
                _ = this.data.Value;
                default(Ent).Set(new TestComponent());
            }
        }
        public partial struct GenericAutoPropertyHandleSystem : IUpdate {
            public GenericAutoProperty<JobHandle> data;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                this.data.Value = IJobExtensions.Schedule(default(ControlFirstJob), default);
                this.data.Value.Complete();
                default(Ent).Set(new TestComponent());
            }
        }
        public partial struct DefaultGenericHandleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                ConstructGenericValue<JobHandle>().Complete();
                default(Ent).Set(new TestComponent());
            }
        }

        [TestCase(typeof(GenericConstructorWritesJob), typeof(Test1Component))]
        [TestCase(typeof(ImplicitGenericConstructorJob), typeof(Test2Component))]
        public void GenericConstructionSafetyIncludesTheActualConstructorAndInitializers(Type job, Type component) {
            var reader = CreateSafetyReader(() => Assert.Fail("Closed generic constructor must use source summaries."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(component, 2) }, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [TestCase(typeof(RepeatedGenericConstructorJob), "2", "0")]
        [TestCase(typeof(LoopGenericConstructorJob), "1", "1")]
        public void GenericConstructionEntityCountsRetainRepeatedAndLoopCalls(Type job, string inline, string loop) {
            var rows = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var count = rows.Select(row => row.Split('\t')).Single(row => row[0] == "C");
            Assert.AreEqual(inline, count[3]);
            Assert.AreEqual(loop, count[4]);
            Assert.IsTrue(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
        }

        [Test]
        public void GenericConstructionRetainsScheduledJobsForSubsequentAccess() {
            var system = typeof(GenericConstructorSchedulingSystem);
            var jobs = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            Assert.AreEqual("0", jobs[2], string.Join("\n", jobs));
            Assert.AreEqual(1, jobs.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
            var sync = SynchronizationSummary(system);
            Assert.AreEqual("0", sync[2], string.Join("\n", sync));
            CollectionAssert.Contains(sync, "S\tunproven");
        }

        [Test]
        public void GenericConstructionCannotEraseUnknownConstructorCallbacks() {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(UnknownGenericConstructorJob), key);
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke ", StringComparison.Ordinal)), string.Join("\n", rows));
            }
        }

        [TestCase(typeof(GenericAutoPropertyPlainSystem), "proven")]
        [TestCase(typeof(GenericAutoPropertyHandleSystem), "incomplete")]
        [TestCase(typeof(DefaultGenericHandleSystem), "proven")]
        public void AutoPropertyStorageAndDefaultConstructionAreClassifiedAfterSubstitution(Type system, string status) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + status, string.Join("\n", rows));
            Assert.AreEqual(status == "incomplete", rows[2] != "0", string.Join("\n", rows));
            if (status == "incomplete")
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tFieldStorage:", StringComparison.Ordinal)), string.Join("\n", rows));
        }
    }
}
