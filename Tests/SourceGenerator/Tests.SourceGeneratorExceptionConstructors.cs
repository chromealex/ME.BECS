using System;
using System.Linq;
using System.Runtime.Serialization;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata only: neither exception constructors nor these jobs are invoked.
        public static string DiagnosticMessage(in Ent ent) { ent.Set(new TestComponent()); Ent.New(); return "message"; }
        public static System.Exception DiagnosticInner(in Ent ent) { ent.Set(new Test1Component()); return new System.Exception("inner"); }
        public static void DiagnosticStandardConstructors() {
            _ = new System.Exception("message");
            _ = new System.SystemException("message");
            _ = new ArgumentException("message", "name", null);
            _ = new ArgumentNullException("name", "message");
            _ = new ArgumentOutOfRangeException("name", default(FormattingEffects), "message");
            _ = new InvalidOperationException("message");
            _ = new IndexOutOfRangeException("message");
            _ = new NotSupportedException("message");
            _ = new NotImplementedException("message");
            _ = new NullReferenceException("message");
            _ = new ArithmeticException("message");
            _ = new OverflowException("message");
            _ = new ObjectDisposedException("name", "message");
        }
        public partial struct DiagnosticStandardJob : IJob {
            public void Execute() { DiagnosticStandardConstructors(); }
        }
        public partial struct DiagnosticBecsNotCreatedJob : IJob {
            public void Execute() { E.NotCreatedException.Throw(0); }
        }
        public partial struct DiagnosticBecsQueryBuilderJob : IJob {
            public void Execute() { E.QueryBuilderException.Throw("message"); }
        }
        public partial struct DiagnosticBecsTypeNotFoundJob : IJob {
            public void Execute() { E.TypeNotFoundException.Throw("message"); }
        }
        public partial struct DiagnosticArgumentsJob : IJob {
            public Ent ent;
            public void Execute() { _ = new ArgumentException(DiagnosticMessage(in this.ent), "name", DiagnosticInner(in this.ent)); }
        }
        public class UserDiagnosticException : System.Exception {
            public Ent initialized = Ent.New();
            public UserDiagnosticException() : base("user") { Ent.New(); default(Ent).Set(new Test2Component()); }
            public override string Message { get { default(Ent).Set(new Test3Component()); return "user"; } }
        }
        public partial struct DiagnosticUserJob : IJob {
            public void Execute() { _ = new UserDiagnosticException(); }
        }
        public partial struct DiagnosticGenericStandardJob : IJob {
            public void Execute() { _ = ConstructGenericValue<InvalidOperationException>(); }
        }
        public partial struct DiagnosticGenericUserJob : IJob {
            public void Execute() { _ = ConstructGenericValue<UserDiagnosticException>(); }
        }
        public partial struct DiagnosticStoredInnerJob : IJob {
            public UserDiagnosticException inner;
            public void Execute() { _ = new System.Exception("outer", this.inner); }
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct DiagnosticJob : IJob {
                public Ent ent;
                public void Execute() { this.ent.Set(default(T)); _ = new System.Exception("message"); }
            }
        }
        public class SerializationDiagnosticException : System.Exception {
            public SerializationDiagnosticException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }
        public partial struct DiagnosticSerializationJob : IJob {
            public void Execute() { _ = new SerializationDiagnosticException(null, default); }
        }
        public partial struct DiagnosticFormattingJob : IJob {
            public void Execute() { _ = new System.Exception($"{unknownFormattingValue}"); }
        }
        public static string DiagnosticUnknownMessage() { inlineStringUnknown(); return "message"; }
        public partial struct DiagnosticUnknownArgumentJob : IJob {
            public void Execute() { _ = new System.Exception(DiagnosticUnknownMessage()); }
        }
        public partial struct DiagnosticVirtualMessageJob : IJob {
            public System.Exception exception;
            public void Execute() { _ = this.exception.Message; }
        }
        public static string DiagnosticScheduleMessage() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return "message"; }
        public static string DiagnosticCompleteMessage() {
            var handle = IJobExtensions.Schedule(default(ControlFirstJob), default);
            handle.Complete(); return "message";
        }
        public partial struct DiagnosticBeforeSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) { _ = new System.Exception(DiagnosticMessage(in this.ent)); context.dependsOn.Complete(); }
        }
        public partial struct DiagnosticAfterSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = new System.Exception(DiagnosticMessage(in this.ent)); }
        }
        public partial struct DiagnosticScheduleSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); _ = new System.Exception(DiagnosticScheduleMessage()); this.ent.Set(new TestComponent());
            }
        }
        public partial struct DiagnosticCompleteSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); _ = new System.Exception(DiagnosticCompleteMessage()); this.ent.Set(new TestComponent());
            }
        }

        [Test]
        public void StandardExceptionConstructorContractsAreExactNotAnInheritanceExemption() {
            var calls = ExternalValueOperations(nameof(DiagnosticStandardConstructors)).Where(row => row[0] == "new").ToArray();
            Assert.AreEqual(13, calls.Length);
            foreach (var row in calls) CollectionAssert.Contains(row, "!ecs-leaf");
            var user = ControlSummary(typeof(DiagnosticUserJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", user[2], string.Join("\n", user));
            Assert.IsTrue(user.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) && row.EndsWith("\t2\t0", StringComparison.Ordinal)),
                "Both the user constructor initializer and body must create entities.");
        }

        [TestCase(typeof(DiagnosticStandardJob))]
        [TestCase(typeof(DiagnosticGenericStandardJob))]
        [TestCase(typeof(DiagnosticStoredInnerJob))]
        [TestCase(typeof(DiagnosticBecsNotCreatedJob))]
        [TestCase(typeof(DiagnosticBecsQueryBuilderJob))]
        [TestCase(typeof(DiagnosticBecsTypeNotFoundJob))]
        public void StandardExceptionConstructionDoesNotReadStoredUserObjects(Type job) {
            var reader = CreateSafetyReader(() => Assert.Fail("Audited constructor must not need IL."));
            CollectionAssert.IsEmpty(SafetySelectionRecords(SelectJobSafety(reader, job)));
            foreach (var key in new[] { "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(job, key);
                Assert.AreEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("C\t", StringComparison.Ordinal)));
            }
        }

        [Test]
        public void ExceptionConstructorArgumentsRetainTheirEffects() {
            var reader = CreateSafetyReader(() => Assert.Fail("Known constructor arguments must use source."));
            CollectionAssert.AreEqual(new[] { typeof(TestComponent), typeof(Test1Component) }
                    .Select(type => SafetyExceptionDependency(type, 2)).OrderBy(row => row, StringComparer.Ordinal).ToArray(),
                SafetySelectionRecords(SelectJobSafety(reader, typeof(DiagnosticArgumentsJob))));
            var counts = ControlSummary(typeof(DiagnosticArgumentsJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsTrue(counts.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) && row.EndsWith("\t1\t0", StringComparison.Ordinal)));
            var weights = ControlSummary(typeof(DiagnosticArgumentsJob), "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            CollectionAssert.Contains(weights, "W\tME.BECS.Ent.NewEnt_INTERNAL\t10");
        }

        [TestCase(typeof(DiagnosticUserJob))]
        [TestCase(typeof(DiagnosticGenericUserJob))]
        public void UserExceptionInitializersAndBodiesAreNotExternalLeaves(Type job) {
            var reader = CreateSafetyReader(() => Assert.Fail("User exception body must use source."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(Test2Component), 2) }, SafetySelectionRecords(SelectJobSafety(reader, job)));
            var counts = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsTrue(counts.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) && row.EndsWith("\t2\t0", StringComparison.Ordinal)));
        }

        [TestCase(typeof(DiagnosticSerializationJob), "MissingSummary")]
        [TestCase(typeof(DiagnosticFormattingJob), "ImplicitFormatting")]
        [TestCase(typeof(DiagnosticUnknownArgumentJob), "DelegateInvoke")]
        [TestCase(typeof(DiagnosticVirtualMessageJob), "VirtualDispatch")]
        public void ExceptionConstructorContractsDoNotCoverSerializationFormattingOrUnknownCallbacks(Type job, string gap) {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(job, key);
                Assert.AreNotEqual("0", rows[2], key);
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\t" + gap, StringComparison.Ordinal)), string.Join("\n", rows));
            }
        }

        [TestCase(typeof(DiagnosticBeforeSystem), "unproven")]
        [TestCase(typeof(DiagnosticAfterSystem), "proven")]
        [TestCase(typeof(DiagnosticScheduleSystem), "unproven")]
        [TestCase(typeof(DiagnosticCompleteSystem), "proven")]
        public void ExceptionConstructionRetainsOrderedArgumentSynchronization(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + expected);
            if (system != typeof(DiagnosticScheduleSystem) && system != typeof(DiagnosticCompleteSystem)) return;
            var jobs = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            Assert.AreEqual("0", jobs[2], string.Join("\n", jobs));
            Assert.AreEqual(1, jobs.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
        }

        [Test]
        public void ExceptionConstructorContractsPreserveClosedGenericJobDependencies() {
            var reader = CreateSafetyReader(() => Assert.Fail("Closed generic job must use source."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(AotMarker), 2) },
                SafetySelectionRecords(SelectJobSafety(reader, typeof(GenericAotSystem<AotMarker>.DiagnosticJob))));
        }
    }
}
