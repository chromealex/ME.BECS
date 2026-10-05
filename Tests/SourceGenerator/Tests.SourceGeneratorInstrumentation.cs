using System;
using System.Linq;
using NUnit.Framework;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Profiling.LowLevel;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata only; never call Unity's native profiling API from these tests.
        public static string InstrumentationName(in Ent ent) { ent.Set(new Test1Component()); Ent.New(); return "marker"; }
        public static unsafe char* InstrumentationPointer(in Ent ent) { ent.Set(new Test2Component()); return null; }
        public static int InstrumentationLength(in Ent ent) { ent.Set(new Test3Component()); return 0; }
        public static ProfilerCategory InstrumentationCategory(in Ent ent) { ent.Set(new TestComponent()); return new ProfilerCategory("category"); }
        public static ProfilerMarker InstrumentationReceiver(in Ent ent) { ent.Set(new TestComponent()); return new ProfilerMarker("receiver"); }
        public static UnityEngine.Object InstrumentationObject(in Ent ent) { ent.Set(new Test2Component()); return null; }
        public static unsafe void InstrumentationConstructors(in Ent ent) {
            _ = new ProfilerMarker(InstrumentationName(in ent));
            _ = new ProfilerMarker(InstrumentationPointer(in ent), InstrumentationLength(in ent));
            _ = new ProfilerMarker(InstrumentationCategory(in ent), InstrumentationName(in ent));
            _ = new ProfilerMarker(InstrumentationCategory(in ent), InstrumentationPointer(in ent), InstrumentationLength(in ent));
            _ = new ProfilerMarker(InstrumentationCategory(in ent), InstrumentationName(in ent), MarkerFlags.Default);
            _ = new ProfilerMarker(InstrumentationCategory(in ent), InstrumentationPointer(in ent), InstrumentationLength(in ent), MarkerFlags.Default);
        }
        public static void InstrumentationOperations(in Ent ent) {
            InstrumentationReceiver(in ent).Begin();
            InstrumentationReceiver(in ent).Begin(InstrumentationObject(in ent));
            InstrumentationReceiver(in ent).End();
            var scope = InstrumentationReceiver(in ent).Auto();
            scope.Dispose();
            _ = InstrumentationReceiver(in ent).Handle;
            _ = JobsUtility.ThreadIndex;
            _ = JobsUtility.ThreadIndexCount;
            _ = JobsUtility.IsExecutingJob;
        }
        public static void InstrumentationBuiltinCategories() {
            _ = new ProfilerCategory("category");
            _ = new ProfilerCategory("category", default);
            ushort value = ProfilerCategory.Scripts;
            _ = ProfilerCategory.Render; _ = ProfilerCategory.Gui; _ = ProfilerCategory.Physics; _ = ProfilerCategory.Physics2D;
            _ = ProfilerCategory.Animation; _ = ProfilerCategory.Ai; _ = ProfilerCategory.Audio; _ = ProfilerCategory.Video;
            _ = ProfilerCategory.Particles; _ = ProfilerCategory.Lighting; _ = ProfilerCategory.Network; _ = ProfilerCategory.Loading;
            _ = ProfilerCategory.Vr; _ = ProfilerCategory.Input; _ = ProfilerCategory.Memory; _ = ProfilerCategory.VirtualTexturing;
            _ = ProfilerCategory.FileIO; _ = ProfilerCategory.Internal;
        }
        public static void InstrumentationDispose<T>(ref T scope) where T : struct, IDisposable => scope.Dispose();
        public struct UserInstrumentationScope : IDisposable {
            void IDisposable.Dispose() { Ent.New(); default(Ent).Set(new Test3Component()); }
        }
        public struct UserInstrumentationMarker {
            public UserInstrumentationMarker(string name) { Ent.New(); default(Ent).Set(new Test3Component()); }
            public void Begin() { default(Ent).Set(new TestComponent()); }
            public void End() { }
            public IntPtr Handle { get { default(Ent).Set(new Test2Component()); return default; } }
        }
        public static void InstrumentationLookalikes() {
            var marker = new UserInstrumentationMarker("user");
            marker.Begin(); marker.End(); _ = marker.Handle;
        }
        public static void InstrumentationOtherMembers() {
            _ = default(ProfilerCategory).Name;
            _ = JobsUtility.JobWorkerCount;
        }
        public partial struct InstrumentationEffectsJob : IJob {
            public Ent ent;
            public void Execute() { InstrumentationConstructors(in this.ent); InstrumentationOperations(in this.ent); }
        }
        public partial struct InstrumentationScopeJob : IJob {
            public void Execute() { var scope = default(ProfilerMarker.AutoScope); InstrumentationDispose(ref scope); }
        }
        public partial struct InstrumentationUserScopeJob : IJob {
            public void Execute() { var scope = default(UserInstrumentationScope); InstrumentationDispose(ref scope); }
        }
        public partial struct InstrumentationUsingJob : IJob {
            public Ent ent;
            public void Execute() { using (new ProfilerMarker(InstrumentationName(in this.ent)).Auto()) { this.ent.Set(new Test2Component()); } }
        }
        public partial struct InstrumentationUnknownJob : IJob {
            public void Execute() { _ = new ProfilerMarker(DiagnosticUnknownMessage()); }
        }
        public partial struct InstrumentationConditionalJob : IJob {
            public Ent ent;
            public void Execute() { new ProfilerMarker(InstrumentationName(in this.ent)).Begin(InstrumentationObject(in this.ent)); }
        }
        public partial struct InstrumentationLookalikeJob : IJob {
            public void Execute() => InstrumentationLookalikes();
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct InstrumentationJob : IJob {
                public void Execute() { _ = new ProfilerMarker("generic").Auto(); default(Ent).Set(default(T)); }
            }
        }
        public static string InstrumentationScheduleName() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return "scheduled"; }
        public static string InstrumentationCompleteName() { IJobExtensions.Schedule(default(ControlFirstJob), default).Complete(); return "completed"; }
        public partial struct InstrumentationBeforeSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) { InstrumentationOperations(in this.ent); context.dependsOn.Complete(); }
        }
        public partial struct InstrumentationAfterSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); InstrumentationOperations(in this.ent); }
        }
        public partial struct InstrumentationScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = new ProfilerMarker(InstrumentationScheduleName()).Auto(); _ = FormattingAccessText(); }
        }
        public partial struct InstrumentationCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = new ProfilerMarker(InstrumentationCompleteName()).Auto(); _ = FormattingAccessText(); }
        }
        public partial struct InstrumentationUsingSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); using (new ProfilerMarker("scope").Auto()) { _ = FormattingAccessText(); } }
        }
        public partial struct InstrumentationJobStateSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = JobsUtility.IsExecutingJob; _ = JobsUtility.ThreadIndex; _ = FormattingAccessText(); }
        }
        public partial struct InstrumentationConditionalSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                new ProfilerMarker(InstrumentationScheduleName()).Begin();
                _ = FormattingAccessText();
            }
        }

        [Test]
        public void InstrumentationContractsKeepAllConstructorArgumentsAndReceivers() {
            var constructors = ExternalValueOperations(nameof(InstrumentationConstructors));
            var markers = constructors.Where(row => row[3].StartsWith("M:Unity.Profiling.ProfilerMarker.#ctor", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(6, markers.Length);
            foreach (var marker in markers) CollectionAssert.Contains(marker, "!ecs-leaf");
            foreach (var entry in new[] { (nameof(InstrumentationName), 3), (nameof(InstrumentationPointer), 3), (nameof(InstrumentationLength), 3), (nameof(InstrumentationCategory), 4) })
                Assert.AreEqual(entry.Item2, constructors.Count(row => row[3].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + entry.Item1 + "(", StringComparison.Ordinal)));
            var operations = ExternalValueOperations(nameof(InstrumentationOperations));
            var unityOperations = operations.Where(row => row[3].StartsWith("M:Unity.", StringComparison.Ordinal)).ToArray();
            #if ENABLE_PROFILER
            Assert.AreEqual(9, unityOperations.Length);
            #else
            Assert.AreEqual(6, unityOperations.Length);
            #endif
            foreach (var operation in unityOperations)
                CollectionAssert.Contains(operation, "!ecs-leaf", operation[3]);
            #if ENABLE_PROFILER
            Assert.AreEqual(5, operations.Count(row => row[3].Contains(".InstrumentationReceiver(")));
            Assert.AreEqual(1, operations.Count(row => row[3].Contains(".InstrumentationObject(")));
            #else
            Assert.AreEqual(2, operations.Count(row => row[3].Contains(".InstrumentationReceiver(")));
            Assert.AreEqual(0, operations.Count(row => row[3].Contains(".InstrumentationObject(")));
            #endif
            var categories = ExternalValueOperations(nameof(InstrumentationBuiltinCategories));
            var values = categories.Where(row => row[3].StartsWith("M:Unity.Profiling.ProfilerCategory.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(22, values.Length);
            foreach (var value in values) CollectionAssert.Contains(value, "!ecs-leaf");
        }

        [TestCase(typeof(InstrumentationEffectsJob), 4, "3")]
        [TestCase(typeof(InstrumentationScopeJob), 0, "0")]
        [TestCase(typeof(InstrumentationUserScopeJob), 1, "1")]
        [TestCase(typeof(InstrumentationUsingJob), 2, "1")]
        [TestCase(typeof(GenericAotSystem<AotMarker>.InstrumentationJob), 1, "0")]
        [TestCase(typeof(InstrumentationLookalikeJob), 3, "1")]
        #if ENABLE_PROFILER
        [TestCase(typeof(InstrumentationConditionalJob), 2, "1")]
        #else
        [TestCase(typeof(InstrumentationConditionalJob), 0, "0")]
        #endif
        public void InstrumentationUsesSourceSafetyCountsAndWeightsWithoutIL(Type job, int components, string entities) {
            var reader = CreateSafetyReader(() => Assert.Fail("Audited instrumentation must not require IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            Assert.AreEqual(components, SafetySelectionRecords(SelectJobSafety(reader, job)).Length);
            var expectedTypes = job == typeof(InstrumentationEffectsJob) ? new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) } :
                job == typeof(InstrumentationUsingJob) || job == typeof(InstrumentationConditionalJob) && components != 0 ? new[] { typeof(Test1Component), typeof(Test2Component) } :
                job == typeof(InstrumentationUserScopeJob) ? new[] { typeof(Test3Component) } :
                job == typeof(InstrumentationLookalikeJob) ? new[] { typeof(TestComponent), typeof(Test2Component), typeof(Test3Component) } :
                job == typeof(GenericAotSystem<AotMarker>.InstrumentationJob) ? new[] { typeof(AotMarker) } : Array.Empty<Type>();
            CollectionAssert.AreEqual(expectedTypes.Select(type => SafetyExceptionDependency(type, 2)).OrderBy(row => row, StringComparer.Ordinal),
                SafetySelectionRecords(SelectJobSafety(reader, job)));
            var counts = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            var creation = counts.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray();
            if (entities == "0") CollectionAssert.IsEmpty(creation);
            else { Assert.AreEqual(1, creation.Length); Assert.IsTrue(creation[0].EndsWith("\t" + entities + "\t0", StringComparison.Ordinal), creation[0]); }
            var weights = ControlSummary(job, "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            if (entities != "0") CollectionAssert.Contains(weights, "W\tME.BECS.Ent.NewEnt_INTERNAL\t10");
        }

        [TestCase(typeof(InstrumentationBeforeSystem), "unproven")]
        [TestCase(typeof(InstrumentationAfterSystem), "proven")]
        [TestCase(typeof(InstrumentationScheduleSystem), "unproven")]
        [TestCase(typeof(InstrumentationCompleteSystem), "proven")]
        [TestCase(typeof(InstrumentationJobStateSystem), "unproven")]
        #if ENABLE_PROFILER
        [TestCase(typeof(InstrumentationConditionalSystem), "unproven")]
        #else
        [TestCase(typeof(InstrumentationConditionalSystem), "proven")]
        #endif
        public void InstrumentationIsNotCompletionAndRetainsOrderedArgumentEffects(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected);
        }

        [Test]
        public void InstrumentationDoesNotHideUnknownArgumentsOrFinallySynchronization() {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(InstrumentationUnknownJob), key);
                Assert.AreNotEqual("0", rows[2]);
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke", StringComparison.Ordinal)), string.Join("\n", rows));
            }
            var synchronization = SynchronizationSummary(typeof(InstrumentationUsingSystem));
            Assert.AreEqual("0", synchronization[2], string.Join("\n", synchronization));
            CollectionAssert.Contains(synchronization, "S\tproven");
        }

        [TestCase(nameof(InstrumentationLookalikes), 4)]
        [TestCase(nameof(InstrumentationOtherMembers), 2)]
        public void InstrumentationContractsDoNotWhitelistNamesOrOtherMembers(string method, int count) {
            var operations = ExternalValueOperations(method);
            Assert.AreEqual(count, operations.Length);
            foreach (var operation in operations) Assert.IsFalse(operation.Contains("!ecs-leaf"), operation[3]);
        }
    }
}
