using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Only inspect metadata. Never run these intentionally invalid buffer operations.
        private static FixedString32Bytes inlineStringStorage;
        public static ref FixedString32Bytes InlineStringReceiver(in Ent ent) { ent.Set(new TestComponent()); return ref inlineStringStorage; }
        public static int InlineStringIndex(in Ent ent) { ent.Set(new Test1Component()); Ent.New(); return 0; }
        public static byte InlineStringByte(in Ent ent) { ent.Set(new Test2Component()); return 0; }
        public static NativeArrayOptions InlineStringOptions(in Ent ent) { ent.Set(new Test3Component()); return NativeArrayOptions.ClearMemory; }
        public static unsafe void InlineStringOperations(in Ent ent) {
            _ = InlineStringReceiver(in ent).Length;
            InlineStringReceiver(in ent).Length = InlineStringIndex(in ent);
            _ = InlineStringReceiver(in ent).Capacity;
            InlineStringReceiver(in ent).Capacity = InlineStringIndex(in ent);
            _ = InlineStringReceiver(in ent).IsEmpty;
            _ = InlineStringReceiver(in ent)[InlineStringIndex(in ent)];
            InlineStringReceiver(in ent)[InlineStringIndex(in ent)] = InlineStringByte(in ent);
            _ = InlineStringReceiver(in ent).ElementAt(InlineStringIndex(in ent));
            InlineStringReceiver(in ent).Clear();
            InlineStringReceiver(in ent).Add(InlineStringByte(in ent));
            _ = InlineStringReceiver(in ent).TryResize(InlineStringIndex(in ent), InlineStringOptions(in ent));
            _ = InlineStringReceiver(in ent).GetUnsafePtr();
            _ = FixedString32Bytes.UTF8MaxLengthInBytes;
        }
        public static void InlineStringEverySize() {
            _ = default(FixedString32Bytes).IsEmpty;
            _ = default(FixedString64Bytes).IsEmpty;
            _ = default(FixedString128Bytes).IsEmpty;
            _ = default(FixedString512Bytes).IsEmpty;
            _ = default(FixedString4096Bytes).IsEmpty;
        }
        public partial struct InlineStringEffectsJob : IJob {
            public Ent ent;
            public void Execute() { InlineStringOperations(in this.ent); InlineStringEverySize(); }
        }
        public static unsafe void InlineStringGeneric<T>(ref T value) where T : unmanaged, IUTF8Bytes {
            _ = value.IsEmpty;
            _ = value.GetUnsafePtr();
            _ = value.TryResize(0, NativeArrayOptions.ClearMemory);
        }
        public partial struct InlineStringGenericJob : IJob {
            public FixedString128Bytes value;
            public void Execute() { InlineStringGeneric(ref this.value); }
        }
        public unsafe struct UserInlineString : IUTF8Bytes {
            bool IUTF8Bytes.IsEmpty { get { default(Ent).Set(new Test2Component()); return true; } }
            byte* IUTF8Bytes.GetUnsafePtr() { default(Ent).Set(new Test3Component()); return null; }
            bool IUTF8Bytes.TryResize(int length, NativeArrayOptions options) { default(Ent).Set(new Test1Component()); Ent.New(); return true; }
        }
        public partial struct UserInlineStringJob : IJob {
            public UserInlineString value;
            public void Execute() { InlineStringGeneric(ref this.value); }
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct InlineStringJob : IJob {
                public Ent ent;
                public FixedString64Bytes value;
                public void Execute() { this.ent.Set(default(T)); InlineStringGeneric(ref this.value); }
            }
        }
        private static Action inlineStringUnknown;
        public static int InlineStringUnknownIndex() { inlineStringUnknown(); return 0; }
        public partial struct InlineStringUnknownJob : IJob {
            public FixedString32Bytes value;
            public void Execute() { this.value.Length = InlineStringUnknownIndex(); }
        }
        public static int InlineStringScheduleIndex() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return 0; }
        public static int InlineStringCompleteIndex() {
            var handle = IJobExtensions.Schedule(default(ControlFirstJob), default);
            handle.Complete(); return 0;
        }
        public partial struct InlineStringBeforeSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) { InlineStringOperations(in this.ent); context.dependsOn.Complete(); }
        }
        public partial struct InlineStringAfterSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); InlineStringOperations(in this.ent); }
        }
        public partial struct InlineStringScheduleSystem : IUpdate {
            public Ent ent;
            public FixedString32Bytes value;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); this.value.Length = InlineStringScheduleIndex(); this.ent.Set(new TestComponent());
            }
        }
        public partial struct InlineStringCompleteSystem : IUpdate {
            public Ent ent;
            public FixedString32Bytes value;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); this.value.Length = InlineStringCompleteIndex(); this.ent.Set(new TestComponent());
            }
        }

        [Test]
        public void FixedStringBufferContractsRetainReceiverAndArgumentEffects() {
            var rows = ExternalValueOperations(nameof(InlineStringOperations));
            var inline = rows.Where(row => row[3].StartsWith("M:Unity.Collections.FixedString32Bytes.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(13, inline.Length);
            foreach (var row in inline) CollectionAssert.Contains(row, "!ecs-leaf");
            foreach (var helper in new (string name, int count)[] { (nameof(InlineStringReceiver), 12), (nameof(InlineStringIndex), 6),
                         (nameof(InlineStringByte), 2), (nameof(InlineStringOptions), 1) }) {
                var calls = rows.Where(row => row[3].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + helper.name + "(", StringComparison.Ordinal)).ToArray();
                Assert.AreEqual(helper.count, calls.Length, helper.name);
                Assert.IsFalse(calls.Any(row => row.Contains("!ecs-leaf")), helper.name);
            }
        }

        [Test]
        public void FixedStringInlineContractsCoverEveryBufferSize() {
            var rows = ExternalValueOperations(nameof(InlineStringEverySize));
            Assert.AreEqual(5, rows.Length);
            foreach (var row in rows) CollectionAssert.Contains(row, "!ecs-leaf");
        }

        [Test]
        public void FixedStringArgumentsRetainSafetyCountsAndWeightsWithoutIL() {
            var reader = CreateSafetyReader(() => Assert.Fail("Audited inline operations must use source."));
            var expected = new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) }
                .Select(type => SafetyExceptionDependency(type, 2)).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, typeof(InlineStringEffectsJob))));
            var counts = ControlSummary(typeof(InlineStringEffectsJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsTrue(counts.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) && row.EndsWith("\t6\t0", StringComparison.Ordinal)));
            var weights = ControlSummary(typeof(InlineStringEffectsJob), "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            CollectionAssert.Contains(weights, "W\tME.BECS.Ent.NewEnt_INTERNAL\t10");
        }

        [TestCase(typeof(InlineStringGenericJob), false)]
        [TestCase(typeof(UserInlineStringJob), true)]
        public void FixedStringInterfaceBindingUsesTheConcreteImplementation(Type job, bool user) {
            var reader = CreateSafetyReader(() => Assert.Fail("Concrete interface dispatch must use source."));
            var expected = user ? new[] { typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) }
                .Select(type => SafetyExceptionDependency(type, 2)).OrderBy(row => row, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
            var counts = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.AreEqual(user, counts.Any(row => row.StartsWith("C\t", StringComparison.Ordinal)));
            if (user) Assert.IsTrue(counts.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) && row.EndsWith("\t1\t0", StringComparison.Ordinal)));
        }

        [Test]
        public void FixedStringContractsDoNotEraseUnknownArgumentCallbacks() {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(InlineStringUnknownJob), key);
                Assert.AreNotEqual("0", rows[2], key);
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke", StringComparison.Ordinal)), key);
            }
        }

        [TestCase(typeof(InlineStringBeforeSystem), "unproven")]
        [TestCase(typeof(InlineStringAfterSystem), "proven")]
        [TestCase(typeof(InlineStringScheduleSystem), "unproven")]
        [TestCase(typeof(InlineStringCompleteSystem), "proven")]
        public void FixedStringOperationsDoNotErasePendingWork(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + expected);
            if (system != typeof(InlineStringScheduleSystem) && system != typeof(InlineStringCompleteSystem)) return;
            var jobs = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            Assert.AreEqual("0", jobs[2], string.Join("\n", jobs));
            Assert.AreEqual(1, jobs.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
        }

        [Test]
        public void FixedStringContractsPreserveClosedGenericJobDependencies() {
            var reader = CreateSafetyReader(() => Assert.Fail("Closed generic job must use source."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(AotMarker), 2) },
                SafetySelectionRecords(SelectJobSafety(reader, typeof(GenericAotSystem<AotMarker>.InlineStringJob))));
        }
    }
}
