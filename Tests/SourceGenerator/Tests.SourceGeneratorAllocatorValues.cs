using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures: no allocation, Dispose, pointer read or callback runs.
        public struct AllocatorValueProbe : AllocatorManager.IAllocator {
            public Ent ent;
            public AllocatorManager.TryFunction Function => null;
            public AllocatorManager.AllocatorHandle Handle { get; set; }
            public Allocator ToAllocator => this.Handle.ToAllocator;
            public bool IsCustomAllocator => true;
            public int Try(ref AllocatorManager.Block block) { this.ent.Set(new Test3Component()); return 0; }
            public void Dispose() { this.ent.Set(new Test3Component()); }
        }
        public static AllocatorHelper<AllocatorValueProbe> AllocatorValueReceiver(in Ent ent) {
            ent.Set(new TestComponent()); return default;
        }
        public static Allocator AllocatorValueArgument(in Ent ent) {
            ent.Set(new Test1Component()); return Allocator.Temp;
        }
        public static AllocatorManager.AllocatorHandle AllocatorHandleArgument(in Ent ent) {
            ent.Set(new Test2Component()); return default;
        }
        public static void AllocatorValueOperations(in Ent ent) {
            _ = AllocatorValueReceiver(in ent).Allocator;
            AllocatorManager.AllocatorHandle handle = AllocatorValueArgument(in ent);
            _ = AllocatorManager.ConvertToAllocatorHandle(AllocatorValueArgument(in ent));
            _ = AllocatorHandleArgument(in ent).Value;
            _ = AllocatorHandleArgument(in ent).ToAllocator;
            _ = AllocatorHandleArgument(in ent).IsCustomAllocator;
            _ = AllocatorHandleArgument(in ent).Handle;
            handle.Handle = AllocatorHandleArgument(in ent);
        }
        public partial struct AllocatorValueEffectsJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() => AllocatorValueOperations(in this.ent);
        }
        public static void AllocatorGenericValue<T>(AllocatorHelper<T> helper, in Ent ent) where T : unmanaged, AllocatorManager.IAllocator {
            _ = helper.Allocator;
            ent.Set(new Test1Component());
        }
        public partial struct AllocatorGenericValueJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() => AllocatorGenericValue(default(AllocatorHelper<AllocatorValueProbe>), in this.ent);
        }
        public static unsafe void AllocatorUnknownOperations(ref AllocatorValueProbe allocator, AllocatorManager.AllocatorHandle handle) {
            _ = AllocatorManager.Allocate(ref allocator, 8, 8, 1);
            _ = AllocatorManager.Allocate(handle, 8, 8, 1);
            AllocatorManager.Free(handle, null);
            var block = default(AllocatorManager.Block);
            _ = handle.Try(ref block);
            _ = new AllocatorHelper<AllocatorValueProbe>(handle);
            handle.Dispose();
        }
        public partial struct AllocatorUnknownJob : Unity.Jobs.IJob {
            public AllocatorValueProbe allocator;
            public AllocatorManager.AllocatorHandle handle;
            public void Execute() => AllocatorUnknownOperations(ref this.allocator, this.handle);
        }
        public struct UserAllocatorValues {
            public AllocatorValueProbe Allocator { get { default(Ent).Set(new Test3Component()); return default; } }
        }
        public static void UserAllocatorValueOperation() => _ = default(UserAllocatorValues).Allocator;
        public partial struct UserAllocatorValueJob : Unity.Jobs.IJob {
            public void Execute() => UserAllocatorValueOperation();
        }
        public partial struct AllocatorRefBeforeCompletionSystem : IUpdate {
            public AllocatorHelper<AllocatorValueProbe> helper;
            public void OnUpdate(ref SystemContext context) { _ = this.helper.Allocator; }
        }
        public partial struct AllocatorRefAfterCompletionSystem : IUpdate {
            public AllocatorHelper<AllocatorValueProbe> helper;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = this.helper.Allocator;
            }
        }

        [Test]
        public void AllocatorValueContractsRetainEveryReceiverAndArgument() {
            var rows = ExternalValueOperations(nameof(AllocatorValueOperations));
            var native = rows.Where(row => row[3].StartsWith("M:Unity.Collections.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(8, native.Length);
            foreach (var call in native) CollectionAssert.Contains(call, "!ecs-leaf", call[3]);
            var arguments = rows.Where(row => row[3].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.Allocator", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(8, arguments.Length);
            foreach (var call in arguments) Assert.IsFalse(call.Contains("!ecs-leaf"), call[3]);
        }

        [TestCase(typeof(AllocatorValueEffectsJob), 0)]
        [TestCase(typeof(AllocatorGenericValueJob), 1)]
        [TestCase(typeof(UserAllocatorValueJob), 2)]
        public void AllocatorValueSafetyIsSourceOwnedWithoutInvokingAllocatorCallbacks(Type job, int scenario) {
            var reader = CreateSafetyReader(() => Assert.Fail("Value-only allocator access must not read IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            var types = scenario == 0 ? new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component) } :
                scenario == 1 ? new[] { typeof(Test1Component) } : new[] { typeof(Test3Component) };
            var expected = types.Select(type => SafetyExceptionDependency(type, 2)).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [TestCase(nameof(AllocatorUnknownOperations))]
        [TestCase(nameof(UserAllocatorValueOperation))]
        public void AllocatorValueContractsDoNotCoverDispatchAllocationOrUserGetters(string method) {
            var rows = ExternalValueOperations(method);
            Assert.IsTrue(rows.Length > 0);
            foreach (var call in rows) Assert.IsFalse(call.Contains("!ecs-leaf"), call[3]);
        }

        [Test]
        public void UnknownAllocatorDispatchDoesNotCertifySafetyCountsOrWeights() {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(AllocatorUnknownJob), key);
                Assert.AreNotEqual("0", rows[2], key);
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tUnclosedAllocatorRegistry ", StringComparison.Ordinal)), key + "\n" + string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)), key);
            }
        }

        [TestCase(typeof(AllocatorRefBeforeCompletionSystem), "unproven")]
        [TestCase(typeof(AllocatorRefAfterCompletionSystem), "proven")]
        public void AllocatorRefAccessIsNotACompletionProof(Type system, string status) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + status, string.Join("\n", rows));
        }
    }
}
