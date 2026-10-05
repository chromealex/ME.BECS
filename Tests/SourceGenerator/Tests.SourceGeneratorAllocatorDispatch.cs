using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Only inspect generated metadata. Never call these allocators or schedule jobs.
        public struct ExplicitDispatchAllocator : AllocatorManager.IAllocator {
            public AllocatorManager.TryFunction Function => null;
            public Allocator ToAllocator => default;
            public bool IsCustomAllocator => true;
            AllocatorManager.AllocatorHandle AllocatorManager.IAllocator.Handle {
                get { default(Ent).Set(new TestComponent()); return default; }
                set { }
            }
            int AllocatorManager.IAllocator.Try(ref AllocatorManager.Block block) {
                default(Ent).Set(new Test1Component()); return 0;
            }
            // Same spelling is deliberately not the interface implementation.
            public AllocatorManager.AllocatorHandle Handle { get { default(Ent).Set(new Test3Component()); return default; } }
            public int Try(ref AllocatorManager.Block block) { default(Ent).Set(new Test3Component()); return 0; }
            public void Dispose() { default(Ent).Set(new Test3Component()); }
        }
        public struct DispatchAllocatorContainer<T> where T : unmanaged, IComponent {
            public struct Allocator<U> : AllocatorManager.IAllocator where U : unmanaged, IComponent {
                public AllocatorManager.TryFunction Function => null;
                public Unity.Collections.Allocator ToAllocator => default;
                public bool IsCustomAllocator => true;
                public AllocatorManager.AllocatorHandle Handle {
                    get { default(Ent).Set(new T()); return default; }
                    set { }
                }
                public int Try(ref AllocatorManager.Block block) { default(Ent).Set(new U()); return 0; }
                public void Dispose() { }
            }
        }
        public static unsafe void AllocateThroughGenericHelper<T>(ref T allocator) where T : unmanaged, AllocatorManager.IAllocator {
            _ = AllocatorManager.Allocate(ref allocator, 8, 8, 1);
        }
        public partial struct ExplicitAllocatorDispatchJob : IJob {
            public ExplicitDispatchAllocator allocator;
            public void Execute() => AllocateThroughGenericHelper(ref this.allocator);
        }
        public partial struct NestedAllocatorDispatchJob : IJob {
            public DispatchAllocatorContainer<TestComponent>.Allocator<Test1Component> allocator;
            public void Execute() => AllocateThroughGenericHelper(ref this.allocator);
        }
        public struct EntityCreatingAllocator : AllocatorManager.IAllocator {
            public AllocatorManager.TryFunction Function => null;
            public Allocator ToAllocator => default;
            public bool IsCustomAllocator => true;
            public AllocatorManager.AllocatorHandle Handle { get; set; }
            public int Try(ref AllocatorManager.Block block) { Ent.New(); return 0; }
            public void Dispose() { }
        }
        public partial struct RepeatedAllocatorCreationJob : IJob {
            public EntityCreatingAllocator allocator;
            public void Execute() {
                AllocateThroughGenericHelper(ref this.allocator);
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct LoopAllocatorCreationJob : IJob {
            public EntityCreatingAllocator allocator;
            public void Execute() {
                AllocateThroughGenericHelper(ref this.allocator);
                for (var i = 0; i < 2; ++i) AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        public struct EntityCreatingAllocatorGetter : AllocatorManager.IAllocator {
            public AllocatorManager.TryFunction Function => null;
            public Allocator ToAllocator => default;
            public bool IsCustomAllocator => true;
            public AllocatorManager.AllocatorHandle Handle {
                get { Ent.New(); return default; }
                set { }
            }
            public int Try(ref AllocatorManager.Block block) => 0;
            public void Dispose() { }
        }
        public partial struct AllocatorGetterCreationJob : IJob {
            public EntityCreatingAllocatorGetter allocator;
            public void Execute() => AllocateThroughGenericHelper(ref this.allocator);
        }
        public partial struct PrevisitedAllocatorGetterJob : IJob {
            public EntityCreatingAllocatorGetter allocator;
            public void Execute() {
                _ = this.allocator.Handle;
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        public static Action unknownAllocatorCallback;
        public struct UnknownDispatchAllocator : AllocatorManager.IAllocator {
            public AllocatorManager.TryFunction Function => null;
            public Allocator ToAllocator => default;
            public bool IsCustomAllocator => true;
            public AllocatorManager.AllocatorHandle Handle {
                get { unknownAllocatorCallback(); return default; }
                set { }
            }
            public int Try(ref AllocatorManager.Block block) { default(Ent).Set(new Test1Component()); return 0; }
            public void Dispose() { }
        }
        public partial struct UnknownAllocatorCallbackJob : IJob {
            public UnknownDispatchAllocator allocator;
            public void Execute() => AllocateThroughGenericHelper(ref this.allocator);
        }
        public partial struct RegistryAllocatorDispatchJob : IJob {
            public AllocatorManager.AllocatorHandle handle;
            public unsafe void Execute() => _ = AllocatorManager.Allocate(this.handle, 8, 8, 1);
        }
        public partial struct TypedRegistryAllocatorDispatchJob : IJob {
            public AllocatorManager.AllocatorHandle handle;
            public unsafe void Execute() => _ = AllocatorManager.Allocate<int>(this.handle, 1);
        }
        public struct SchedulingDispatchAllocator : AllocatorManager.IAllocator {
            public AllocatorManager.TryFunction Function => null;
            public Allocator ToAllocator => default;
            public bool IsCustomAllocator => true;
            public AllocatorManager.AllocatorHandle Handle {
                get { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return default; }
                set { }
            }
            public int Try(ref AllocatorManager.Block block) {
                _ = IJobExtensions.Schedule(default(ControlSecondJob), default); return 0;
            }
            public void Dispose() { }
        }
        public partial struct AllocatorSchedulingSystem : IUpdate {
            public SchedulingDispatchAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }

        [TestCase(typeof(ExplicitAllocatorDispatchJob))]
        [TestCase(typeof(NestedAllocatorDispatchJob))]
        public void AllocatorDispatchFollowsInterfaceTargetsAndGenericSubstitutionWithoutIL(Type job) {
            var reader = CreateSafetyReader(() => Assert.Fail("Resolved allocator dispatch must not read IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            var expected = new[] { typeof(TestComponent), typeof(Test1Component) }.Select(type => SafetyExceptionDependency(type, 2))
                .OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [TestCase(typeof(RepeatedAllocatorCreationJob), "2", "0")]
        [TestCase(typeof(LoopAllocatorCreationJob), "1", "1")]
        public void AllocatorTryEntityCountsRetainEachCallAndLoopContext(Type job, string inline, string loop) {
            var rows = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var count = rows.Select(row => row.Split('\t')).Single(row => row[0] == "C");
            Assert.AreEqual(inline, count[3]);
            Assert.AreEqual(loop, count[4]);
            Assert.IsTrue(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
        }

        [Test]
        public void AllocatorGetterCreationRequiresKnownPackageCheckMultiplicity() {
            var rows = ControlSummary(typeof(AllocatorGetterCreationJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tAllocatorGetterCreationMultiplicity ", StringComparison.Ordinal)), string.Join("\n", rows));
            Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
        }

        [TestCase(typeof(AllocatorGetterCreationJob))]
        [TestCase(typeof(PrevisitedAllocatorGetterJob))]
        public void AllocatorGetterWeightUncertaintySurvivesVisitedMethodCaching(Type job) {
            var rows = ControlSummary(job, "ME.BECS.JobWeights.v1");
            Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tAllocatorGetterWeightMultiplicity ", StringComparison.Ordinal)), string.Join("\n", rows));
            Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
        }

        [TestCase(typeof(RegistryAllocatorDispatchJob), "UnclosedAllocatorRegistry")]
        [TestCase(typeof(TypedRegistryAllocatorDispatchJob), "UnclosedAllocatorRegistry")]
        [TestCase(typeof(UnknownAllocatorCallbackJob), "DelegateInvoke")]
        public void AllocatorDispatchKeepsUnknownTargetsIncomplete(Type job, string gap) {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(job, key);
                Assert.AreNotEqual("0", rows[2], key + "\n" + string.Join("\n", rows));
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\t" + gap, StringComparison.Ordinal)), key + "\n" + string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)), key);
            }
        }

        [Test]
        public void AllocatorDiscoveryIncludesBothCallbacksAndRetainsPendingWork() {
            var system = typeof(AllocatorSchedulingSystem);
            var scheduled = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            Assert.AreEqual("0", scheduled[2], string.Join("\n", scheduled));
            var jobs = scheduled.Where(row => row.StartsWith("J\t", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(2, jobs.Length);
            var expected = new[] { typeof(ControlFirstJob), typeof(ControlSecondJob) }.Select(type => {
                var identity = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(type.Assembly.FullName + "\nT:" + type.FullName.Replace('+', '.')));
                return "J\tn" + identity.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + identity + "0:";
            }).ToArray();
            CollectionAssert.AreEquivalent(expected, jobs);
            var sync = SynchronizationSummary(system);
            Assert.AreEqual("0", sync[2], string.Join("\n", sync));
            CollectionAssert.Contains(sync, "S\tunproven");
        }
    }
}
