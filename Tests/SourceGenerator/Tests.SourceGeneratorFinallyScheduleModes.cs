using System;
using System.Linq;
using NUnit.Framework;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures: default QueryBuilder must never execute.
        public static void QueryFinallyMayThrow() { }
        public static void QueryFinallyReadOnlyEntry() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); }
            finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFinallyResetBeforeCleanup() {
            var query = default(QueryBuilder).AsReadonly();
            try { query = default; }
            finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFinallyExceptionalValue() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); query = default; }
            finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFinallyNormalContinuation() {
            var query = default(QueryBuilder);
            try { QueryFinallyMayThrow(); query.AsReadonly(); }
            finally { QueryFinallyMayThrow(); }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryFinallyChangesContinuation() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); }
            finally { query = default; }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryFinallyNested() {
            var query = default(QueryBuilder).AsReadonly();
            try {
                try { QueryFinallyMayThrow(); }
                finally { query = default; }
            } finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFinallyReturn(bool leave) {
            var query = default(QueryBuilder).AsReadonly();
            try { if (leave) return; }
            finally { query = default; query.Schedule<QueryModeJob, TestComponent>(); }
            query.AsReadonly();
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryFinallyThrowsDuringCleanup() {
            var query = default(QueryBuilder).AsReadonly();
            try {
                try { QueryFinallyMayThrow(); }
                finally { query = default; throw new InvalidOperationException(); }
            } finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFinallyTryInsideCleanup() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); }
            finally {
                try { QueryFinallyMayThrow(); }
                finally { query = default; }
                query.Schedule<QueryModeJob, TestComponent>();
            }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryFinallyLoop(int count, bool skip) {
            var query = default(QueryBuilder).AsReadonly();
            for (var i = 0; i < count; ++i) {
                try { if (skip) continue; break; }
                finally { query.Schedule<QueryModeJob, TestComponent>(); query = default; }
            }
        }
        public static void QueryFinallyRefMutation() {
            var query = default(QueryBuilder).AsReadonly();
            try { ResetQueryMode(ref query); }
            finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFinallyOmittedMutation() {
            var query = default(QueryBuilder).AsReadonly();
            try { OmittedQueryModeMutation(ref query); }
            finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public struct QueryModeScope : IDisposable { public void Dispose() { } }
        public static void QueryFinallyUsing() {
            var query = default(QueryBuilder).AsReadonly();
            using (var scope = new QueryModeScope()) { query = default; }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryFinallySymbolic(QueryBuilder query, bool isReadonly) {
            try { QueryFinallyMayThrow(); }
            finally {
                query.Schedule<QueryModeJob, TestComponent>();
                if (isReadonly) query.AsReadonly();
                QueryModeGenericHelper<QueryModeJob>(query);
            }
        }
        public static void QueryFinallyArgumentBeforeThrow() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryModeGenericHelper<QueryModeJob>(query = default); }
            finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFinallyNewValueBeforeCleanup() {
            var query = default(QueryBuilder).AsReadonly();
            try { query = new QueryBuilder(); }
            finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFinallyNestedFunction() {
            try {
                void Local() => default(QueryBuilder).AsReadonly().Schedule<QueryModeJob, TestComponent>();
                Local();
            } finally { QueryFinallyMayThrow(); }
        }
        public static void QueryFinallyCatch() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); } catch (System.Exception) { query = default; }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryFinallyFilter(bool accept) {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); } catch (System.Exception) when (accept) { query = default; }
            finally { query.Schedule<QueryModeJob, TestComponent>(); }
        }

        [TestCase(nameof(QueryFinallyReadOnlyEntry), "1")]
        [TestCase(nameof(QueryFinallyResetBeforeCleanup), "0")]
        [TestCase(nameof(QueryFinallyExceptionalValue), "0|1")]
        [TestCase(nameof(QueryFinallyNormalContinuation), "1")]
        [TestCase(nameof(QueryFinallyChangesContinuation), "0")]
        [TestCase(nameof(QueryFinallyNested), "0")]
        [TestCase(nameof(QueryFinallyReturn), "0,1")]
        [TestCase(nameof(QueryFinallyThrowsDuringCleanup), "0")]
        [TestCase(nameof(QueryFinallyTryInsideCleanup), "0,0")]
        [TestCase(nameof(QueryFinallyLoop), "0|1")]
        [TestCase(nameof(QueryFinallyRefMutation), "?")]
        [TestCase(nameof(QueryFinallyOmittedMutation), "1")]
        [TestCase(nameof(QueryFinallyUsing), "0")]
        [TestCase(nameof(QueryFinallySymbolic), "p0")]
        [TestCase(nameof(QueryFinallyArgumentBeforeThrow), "0")]
        [TestCase(nameof(QueryFinallyNewValueBeforeCleanup), "0")]
        [TestCase(nameof(QueryFinallyCatch), "0|1")]
        [TestCase(nameof(QueryFinallyFilter), "0|1")]
        public void FinallyScheduleModesFollowCleanupAndExceptionalValues(string method, string expected) {
            SourceScheduleModesFollowQueryValues(method, expected);
            if (method == nameof(QueryFinallySymbolic)) {
                var rows = ScheduleModeMethodSummary(method);
                var helper = rows.Skip(4).Select(row => row.Split('\t')).Single(row => row.Length >= 5 &&
                    row[3].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.QueryModeGenericHelper``", StringComparison.Ordinal));
                CollectionAssert.Contains(helper, "!schedule-value-0=1|p0",
                    "An unchanged parameter branch keeps its incoming symbolic value, not unknown.");
            }
        }

        public partial struct FinallyReadonlyScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryFinallyReadOnlyEntry();
        }
        public partial struct FinallyMixedScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryFinallyExceptionalValue();
        }
        public partial struct FinallyUnknownScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryFinallyRefMutation();
        }
        public partial struct FinallyNestedFunctionScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryFinallyNestedFunction();
        }
        public partial struct FinallyGenericScheduleSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) {
                var query = default(QueryBuilder).AsReadonly();
                try { QueryFinallyMayThrow(); }
                finally { query.Schedule<GenericAotSystem<T>.UnannotatedSafetyJob, T>(); }
            }
        }
        public partial struct FinallyExplicitQueryJob : IJobForComponents<TestComponent> {
            void IJobForComponents<TestComponent>.Execute(in JobInfo info, in Ent ent, ref TestComponent component) { }
            // Same-named, non-contract overload must not become the dependency root.
            public void Execute() => default(Ent).Set(new Test1Component());
        }
        public partial struct FinallyExplicitScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { QueryFinallyMayThrow(); }
                finally { default(QueryBuilder).AsReadonly().Schedule<FinallyExplicitQueryJob, TestComponent>(); }
            }
        }
        public partial struct FinallyGenericExplicitScheduleSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public partial struct Job : IJobForComponents<T> {
                void IJobForComponents<T>.Execute(in JobInfo info, in Ent ent, ref T component) { }
                public void Execute() => default(Ent).Set(new Test1Component());
            }
            public void OnUpdate(ref SystemContext context) {
                try { QueryFinallyMayThrow(); }
                finally { default(QueryBuilder).AsReadonly().Schedule<Job, T>(); }
            }
        }
        public partial struct FinallyAmbiguousQueryJob : IJobForComponents<TestComponent>, Unity.Jobs.IJob {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent component) { }
            public void Execute() => default(Ent).Set(new Test1Component());
        }
        public partial struct FinallyAmbiguousScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { QueryFinallyMayThrow(); }
                finally { default(QueryBuilder).AsReadonly().Schedule<FinallyAmbiguousQueryJob, TestComponent>(); }
            }
        }

        [TestCase(typeof(FinallyReadonlyScheduleSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(FinallyMixedScheduleSystem), typeof(TestComponent), 2)]
        [TestCase(typeof(FinallyNestedFunctionScheduleSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(FinallyGenericScheduleSystem<AotMarker>), typeof(AotMarker), 0)]
        [TestCase(typeof(FinallyExplicitScheduleSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(FinallyGenericExplicitScheduleSystem<AotMarker>), typeof(AotMarker), 0)]
        public void FinallyScheduleModesProduceExactTypedUnion(Type system, Type component, int mode) {
            var rows = SystemDependencyRows(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { "C\t" + mode + "\t" + component.AssemblyQualifiedName },
                rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray());
            Assert.IsNotNull(DependencySelector(system, () => Assert.Fail("Complete query/finally contracts must not fall back to IL."), out _));
        }

        [Test]
        public void FinallyUnknownMutationDoesNotNarrowJobAccess() {
            var rows = ControlSummary(typeof(FinallyUnknownScheduleSystem), "ME.BECS.SystemScheduleModes.v1");
            CollectionAssert.Contains(rows, "S\t?\t" + typeof(QueryModeJob).AssemblyQualifiedName);
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tUnknownScheduleMode", StringComparison.Ordinal)), string.Join("\n", rows));
            Assert.AreNotEqual("0", SystemDependencyRows(typeof(FinallyUnknownScheduleSystem))[2]);
        }

        [Test]
        public void FinallyNestedFunctionHasIndependentHandlerOrdinals() {
            var rows = SynchronizationSummary(typeof(FinallyNestedFunctionScheduleSystem));
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\tproven");
        }

        [Test]
        public void FinallyAmbiguousJobExecuteDoesNotSelectAnArbitraryBody() {
            var rows = SystemDependencyRows(typeof(FinallyAmbiguousScheduleSystem));
            Assert.AreNotEqual("0", rows[2]);
            Assert.IsTrue(rows.Any(row => row.Contains("MissingOrAmbiguousJobExecute")), string.Join("\n", rows));
        }
    }
}
