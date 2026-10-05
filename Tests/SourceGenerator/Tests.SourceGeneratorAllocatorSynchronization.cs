using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Source/metadata fixtures only. Never allocate, execute callbacks or jobs.
        public partial struct AllocatorBeforeCompletionSystem : IUpdate {
            public ExplicitDispatchAllocator allocator;
            public void OnUpdate(ref SystemContext context) => AllocateThroughGenericHelper(ref this.allocator);
        }
        public partial struct AllocatorAfterCompletionSystem : IUpdate {
            public ExplicitDispatchAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        public partial struct AllocatorLateCompletionSystem : IUpdate {
            public ExplicitDispatchAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                AllocateThroughGenericHelper(ref this.allocator);
                context.dependsOn.Complete();
            }
        }
        public partial struct NestedAllocatorAfterCompletionSystem : IUpdate {
            public DispatchAllocatorContainer<TestComponent>.Allocator<Test1Component> allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        public struct CompletedGetterAllocator : AllocatorManager.IAllocator {
            public AllocatorManager.TryFunction Function => null;
            public Allocator ToAllocator => default;
            public bool IsCustomAllocator => true;
            public AllocatorManager.AllocatorHandle Handle {
                get {
                    var scheduled = IJobExtensions.Schedule(default(ControlFirstJob), default);
                    scheduled.Complete();
                    return default;
                }
                set { }
            }
            public int Try(ref AllocatorManager.Block block) { default(Ent).Set(new TestComponent()); return 0; }
            public void Dispose() { }
        }
        public struct PendingTryAllocator : AllocatorManager.IAllocator {
            public AllocatorManager.TryFunction Function => null;
            public Allocator ToAllocator => default;
            public bool IsCustomAllocator => true;
            public AllocatorManager.AllocatorHandle Handle { get; set; }
            public int Try(ref AllocatorManager.Block block) {
                _ = IJobExtensions.Schedule(default(ControlFirstJob), default);
                default(Ent).Set(new TestComponent());
                return 0;
            }
            public void Dispose() { }
        }
        public struct CompletedTryAllocator : AllocatorManager.IAllocator {
            public AllocatorManager.TryFunction Function => null;
            public Allocator ToAllocator => default;
            public bool IsCustomAllocator => true;
            public AllocatorManager.AllocatorHandle Handle { get; set; }
            public int Try(ref AllocatorManager.Block block) {
                var scheduled = IJobExtensions.Schedule(default(ControlFirstJob), default);
                scheduled.Complete();
                default(Ent).Set(new TestComponent());
                return 0;
            }
            public void Dispose() { }
        }
        public partial struct CompletedGetterAllocatorSystem : IUpdate {
            public CompletedGetterAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        public partial struct PendingTryAllocatorSystem : IUpdate {
            public PendingTryAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        public partial struct CompletedTryAllocatorSystem : IUpdate {
            public CompletedTryAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        public partial struct UnknownAllocatorSynchronizationSystem : IUpdate {
            public UnknownDispatchAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                AllocateThroughGenericHelper(ref this.allocator);
            }
        }
        public partial struct RegistryAllocatorSynchronizationSystem : IUpdate {
            public AllocatorManager.AllocatorHandle handle;
            public unsafe void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = AllocatorManager.Allocate<int>(this.handle, 1);
            }
        }
        public static unsafe void FreeAllocatorOperations(AllocatorManager.AllocatorHandle handle, void* pointer) {
            AllocatorManager.Free(handle, pointer);
            AllocatorManager.Free(handle, pointer, 8, 8, 1);
            AllocatorManager.Free(handle, (int*)pointer, 1);
        }
        public partial struct RegistryFreeAllocatorJob : IJob {
            public AllocatorManager.AllocatorHandle handle;
            public unsafe void Execute() => FreeAllocatorOperations(this.handle, null);
        }
        public partial struct RegistryFreeAllocatorSystem : IUpdate {
            public AllocatorManager.AllocatorHandle handle;
            public unsafe void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                FreeAllocatorOperations(this.handle, null);
            }
        }

        [TestCase(typeof(AllocatorBeforeCompletionSystem), "unproven", 0)]
        [TestCase(typeof(AllocatorAfterCompletionSystem), "proven", 0)]
        [TestCase(typeof(AllocatorLateCompletionSystem), "unproven", 0)]
        [TestCase(typeof(NestedAllocatorAfterCompletionSystem), "proven", 0)]
        [TestCase(typeof(CompletedGetterAllocatorSystem), "proven", 1)]
        [TestCase(typeof(PendingTryAllocatorSystem), "unproven", 1)]
        [TestCase(typeof(CompletedTryAllocatorSystem), "proven", 1)]
        public void AllocatorSynchronizationTraversesActualCallbacksWithoutLegacy(Type system, string expected, int jobs) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            var discovery = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            Assert.AreEqual("0", discovery[2], string.Join("\n", discovery));
            Assert.AreEqual(jobs, discovery.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
            // The production selector must consume both complete source contracts
            // without consulting the legacy IL dependency/synchronization analysis.
            var selected = DependencySelector(system, () => Assert.Fail("Known allocator callbacks must use source synchronization."), out _);
            Assert.IsNotNull(selected);
        }

        [TestCase(typeof(UnknownAllocatorSynchronizationSystem), "DelegateInvoke")]
        [TestCase(typeof(RegistryAllocatorSynchronizationSystem), "UnclosedAllocatorRegistry")]
        [TestCase(typeof(RegistryFreeAllocatorSystem), "UnclosedAllocatorRegistry")]
        public void AllocatorSynchronizationCannotCertifyUnknownDispatch(Type system, string gap) {
            var rows = SynchronizationSummary(system);
            Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\tincomplete");
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\t" + gap, StringComparison.Ordinal)), string.Join("\n", rows));
        }

        [Test]
        public void FreeAllocatorContractsRetainRegistryDispatchWithoutInvokingIt() {
            var calls = ExternalValueOperations(nameof(FreeAllocatorOperations));
            Assert.AreEqual(3, calls.Length);
            foreach (var call in calls) Assert.IsFalse(call.Contains("!ecs-leaf"));
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(RegistryFreeAllocatorJob), key);
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tUnclosedAllocatorRegistry ", StringComparison.Ordinal)), string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("G\tMissingSummary ", StringComparison.Ordinal) || row.StartsWith("I\t", StringComparison.Ordinal)), string.Join("\n", rows));
            }
        }
    }
}
