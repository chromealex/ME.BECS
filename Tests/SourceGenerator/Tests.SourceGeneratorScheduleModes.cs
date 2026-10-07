using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public partial struct QueryModeJob : IJobForComponents<TestComponent> {
            // Query-mode fixtures need an actual read/write effect. A ref
            // parameter alone intentionally contributes no write dependency.
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent component) { component.data += 1; }
        }

        // Metadata fixtures only: default builders must never be executed.
        public static void IndependentQueryModes() {
            var read = default(QueryBuilder);
            var write = default(QueryBuilder);
            var before = read;
            read.AsReadonly();
            read.Schedule<QueryModeJob, TestComponent>();
            write.Schedule<QueryModeJob, TestComponent>();
            before.Schedule<QueryModeJob, TestComponent>();
            read.Schedule<QueryModeJob, TestComponent>();
        }

        public static void QueryCopiesAndReset() {
            var query = default(QueryBuilder).AsReadonly();
            var copy = query;
            query = default;
            copy.Schedule<QueryModeJob, TestComponent>();
            query.Schedule<QueryModeJob, TestComponent>();
        }

        public static void TemporaryQueryReceiverIsNotTheOriginal() {
            var query = default(QueryBuilder);
            query.With<TestComponent>().AsReadonly();
            query.Schedule<QueryModeJob, TestComponent>();
            var copy = query.With<TestComponent>().AsReadonly().AsParallel().Sort();
            copy.Schedule<QueryModeJob, TestComponent>();
            query.Schedule<QueryModeJob, TestComponent>();
        }

        public static void QueryModeBranch(bool condition) {
            var query = default(QueryBuilder);
            if (condition) query.AsReadonly();
            query.Schedule<QueryModeJob, TestComponent>();
        }

        public static void QueryModeBothBranches(bool condition) {
            var query = default(QueryBuilder);
            if (condition) query.AsReadonly();
            else query = default(QueryBuilder).AsReadonly();
            query.Schedule<QueryModeJob, TestComponent>();
        }

        public static void QueryModeConditionalValue(bool condition) {
            var query = condition ? default(QueryBuilder).AsReadonly() : default(QueryBuilder);
            query.Schedule<QueryModeJob, TestComponent>();
        }

        public static void QueryModeLoop(int count) {
            var query = default(QueryBuilder);
            for (var index = 0; index < count; ++index) {
                query.Schedule<QueryModeJob, TestComponent>();
                query.AsReadonly();
            }
            query.Schedule<QueryModeJob, TestComponent>();
        }

        public static void QueryModeInParameter(in QueryBuilder query) {
            query.AsReadonly(); // Defensive copy, does not change the caller's value.
            query.Schedule<QueryModeJob, TestComponent>();
            var copy = query.AsReadonly();
            copy.Schedule<QueryModeJob, TestComponent>();
        }

        public static void QueryModeParameterBranch(QueryBuilder query, bool readOnly) {
            if (readOnly) query.AsReadonly();
            query.Schedule<QueryModeJob, TestComponent>();
        }

        public static void ResetQueryMode(ref QueryBuilder query) => query = default;
        public static void QueryModeRefMutation() {
            var query = default(QueryBuilder).AsReadonly();
            ResetQueryMode(ref query);
            query.Schedule<QueryModeJob, TestComponent>();
        }

        [System.Diagnostics.Conditional("BECS_SOURCE_GENERATOR_NEVER_DEFINED_SCHEDULE_MODE_TEST")]
        public static void OmittedQueryModeMutation(ref QueryBuilder query) => query = default;
        public static void QueryModeOmittedMutation() {
            var query = default(QueryBuilder).AsReadonly();
            OmittedQueryModeMutation(ref query);
            query.Schedule<QueryModeJob, TestComponent>();
        }

        public static QueryBuilder UnknownQueryModeFactory() => default;
        public static void QueryModeUnknownFactory() => UnknownQueryModeFactory().Schedule<QueryModeJob, TestComponent>();

        public static void QueryModeFactory(ref SystemContext context) {
            context.Query().AsParallel().With<TestComponent>().Schedule<QueryModeJob, TestComponent>();
            context.Query().AsParallel().AsReadonly().With<TestComponent>().Schedule<QueryModeJob, TestComponent>();
        }

        public static void QueryModeGenericHelper<TJob>(QueryBuilder query) where TJob : struct, IJobForComponents<TestComponent> =>
            query.Schedule<TJob, TestComponent>();
        public static void QueryModeHelperArguments() {
            var query = default(QueryBuilder);
            QueryModeGenericHelper<QueryModeJob>(query.AsReadonly());
            QueryModeGenericHelper<QueryModeJob>(default);
        }

        public static unsafe void QueryModeCapturedParameter(bool isReadonly) {
            // QueryBuilder is a ref struct and cannot be captured, even through a copy.
            // Capture the scheduling flag instead: it belongs to the outer method,
            // not to parameter 0 of Local. This metadata fixture must never execute.
            void Local(bool unrelated) => JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(
                default, null, false, isReadonly, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);
            Local(true);
        }

        public static unsafe void QueryModeCapturedLocal(bool isReadonly) {
            var capturedReadonly = isReadonly;
            void Local(bool unrelated) => JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(
                default, null, false, capturedReadonly, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);
            Local(true);
        }

        public partial struct ReadOnlyScheduleModeSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryModeGenericHelper<QueryModeJob>(default(QueryBuilder).AsReadonly());
        }
        public partial struct MixedScheduleModeSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryModeHelperArguments();
        }
        public partial struct UnknownScheduleModeSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryModeUnknownFactory();
        }
        public partial struct GenericScheduleModeSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) => default(QueryBuilder).AsReadonly()
                .Schedule<GenericAotSystem<T>.UnannotatedSafetyJob, T>();
        }

        private static string[] ScheduleModeMethodSummary(string name) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return typeof(Tests_SourceGeneratorContracts).Assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null)
            .Select(attribute => attribute.Value.Split('\n'))
            .Where(rows => rows[0].IndexOf("~nested:", StringComparison.Ordinal) < 0)
            .Single(rows => rows[0] == "M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + name ||
                rows[0].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + name + "(", StringComparison.Ordinal) ||
                rows[0].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + name + "``", StringComparison.Ordinal));
        }

        [TestCase(nameof(IndependentQueryModes), "1,0,0,1")]
        [TestCase(nameof(QueryCopiesAndReset), "1,0")]
        [TestCase(nameof(TemporaryQueryReceiverIsNotTheOriginal), "0,1,0")]
        [TestCase(nameof(QueryModeBranch), "0|1")]
        [TestCase(nameof(QueryModeBothBranches), "1")]
        [TestCase(nameof(QueryModeConditionalValue), "0|1")]
        [TestCase(nameof(QueryModeLoop), "0|1,0|1")]
        [TestCase(nameof(QueryModeInParameter), "p0,1")]
        [TestCase(nameof(QueryModeParameterBranch), "1|p0")]
        [TestCase(nameof(QueryModeRefMutation), "?")]
        [TestCase(nameof(QueryModeOmittedMutation), "1")]
        [TestCase(nameof(QueryModeUnknownFactory), "?")]
        [TestCase(nameof(QueryModeFactory), "0,1")]
        [TestCase(nameof(QueryModeGenericHelper), "p0")]
        public void SourceScheduleModesFollowQueryValues(string method, string expected) {
            var rows = ScheduleModeMethodSummary(method);
            CollectionAssert.Contains(rows[1].Split(','), "schedule-mode-schema=1");
            var modes = rows.Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Any(token => token.StartsWith("!scheduled-job=", StringComparison.Ordinal)))
                .Select(row => row.Single(token => token.StartsWith("!schedule-readonly=", StringComparison.Ordinal))
                    .Substring("!schedule-readonly=".Length)).ToArray();
            CollectionAssert.AreEqual(expected.Split(','), modes, string.Join("\n", rows));
        }

        [Test]
        public void ScheduleModeHelperArgumentsRemainCallSiteSpecific() {
            var rows = ScheduleModeMethodSummary(nameof(QueryModeHelperArguments));
            var calls = rows.Skip(4).Select(row => row.Split('\t')).Where(row => row.Length >= 5 &&
                row[3].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.QueryModeGenericHelper``", StringComparison.Ordinal)).ToArray();
            CollectionAssert.AreEqual(new[] { "!schedule-value-0=1", "!schedule-value-0=0" },
                calls.Select(row => row.Single(token => token.StartsWith("!schedule-value-0=", StringComparison.Ordinal))).ToArray());
            Assert.IsTrue(calls.All(row => !row.Any(token => token.StartsWith("!scheduled-job=", StringComparison.Ordinal))),
                "A user helper is still traversed; passing a job does not itself schedule it.");
        }

        [TestCase(nameof(QueryModeCapturedParameter))]
        [TestCase(nameof(QueryModeCapturedLocal))]
        public void CapturedReadonlyFlagDoesNotUseTheLocalFunctionParameterOrdinal(string method) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var outer = ScheduleModeMethodSummary(method);
            var nested = typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\n')).Single(rows => rows[0] == outer[0] + "~nested:0");
            var call = nested.Skip(4).Select(row => row.Split('\t')).Single(row => row.Any(token => token.StartsWith("!scheduled-job=", StringComparison.Ordinal)));
            CollectionAssert.Contains(call, "!schedule-readonly=?", "The captured readonly flag is not parameter 0 of the local function.");
        }

        [TestCase(typeof(ReadOnlyScheduleModeSystem), typeof(QueryModeJob), "1", "0")]
        [TestCase(typeof(MixedScheduleModeSystem), typeof(QueryModeJob), "0|1", "0")]
        [TestCase(typeof(UnknownScheduleModeSystem), typeof(QueryModeJob), "?", "1")]
        [TestCase(typeof(GenericScheduleModeSystem<AotMarker>), typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob), "1", "0")]
        public void SystemScheduleModesBindHelpersAndGenericJobs(Type system, Type job, string mode, string gaps) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var rows = system.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.SystemScheduleModes.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\n'))
                .Single(entry => entry[0] == (system.IsGenericType ? system.AssemblyQualifiedName : system.FullName));
            Assert.AreEqual(gaps, rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { "S\t" + mode + "\t" + job.AssemblyQualifiedName },
                rows.Where(row => row.StartsWith("S\t", StringComparison.Ordinal)).ToArray());
            Assert.IsTrue(ValidateScheduleModes(rows), string.Join("\n", rows));
        }

        private static bool ValidateScheduleModes(string[] rows) {
            var method = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobsValidation", true)
                .GetMethod("ValidateScheduleModeSummary", BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)method.Invoke(null, new object[] { rows, 0u });
        }

        [Test]
        public void ScheduleModeParserRejectsUnknownCompleteAndDuplicateJobs() {
            var job = typeof(QueryModeJob).AssemblyQualifiedName;
            Assert.IsTrue(ValidateScheduleModes(new[] { "System", "M:System.OnUpdate", "0", "S\t0|1\t" + job }));
            Assert.IsFalse(ValidateScheduleModes(new[] { "System", "M:System.OnUpdate", "0", "S\t?\t" + job }));
            Assert.IsFalse(ValidateScheduleModes(new[] { "System", "M:System.OnUpdate", "0", "S\tp0\t" + job }));
            Assert.IsFalse(ValidateScheduleModes(new[] { "System", "M:System.OnUpdate", "0", "S\t1\t" + job, "S\t0\t" + job }));
            Assert.IsFalse(ValidateScheduleModes(new[] { "System", "M:System.OnUpdate", "0", "S\t1\t" + typeof(TestComponent).AssemblyQualifiedName }));
            Assert.IsFalse(ValidateScheduleModes(new[] { "System", "M:System.OnUpdate", "0", "G\tunreported gap" }));
            Assert.IsTrue(ValidateScheduleModes(new[] { "System", "M:System.OnUpdate", "1", "S\t?\t" + job, "G\tunresolved" }));
        }
    }
}
