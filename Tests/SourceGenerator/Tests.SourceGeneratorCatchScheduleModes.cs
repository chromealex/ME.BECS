using System;
using System.Linq;
using NUnit.Framework;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata only: none of these default builders/jobs is executed.
        public static void QueryCatchReadonly() {
            var query = default(QueryBuilder);
            try { query.AsReadonly(); }
            catch (System.Exception) { query.AsReadonly(); }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryCatchValueAtThrow() {
            var query = default(QueryBuilder);
            try { query.AsReadonly(); QueryFinallyMayThrow(); }
            catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryCatchResetBeforeThrow() {
            var query = default(QueryBuilder).AsReadonly();
            try { query = default; throw null; }
            catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryCatchRefMutation() {
            var query = default(QueryBuilder).AsReadonly();
            try { ResetQueryMode(ref query); }
            catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryCatchAfterCleanup() {
            var query = default(QueryBuilder).AsReadonly();
            try {
                try { QueryFinallyMayThrow(); }
                finally { query = default; }
            } catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryCatchDoesNotRunOuterCleanupEarly() {
            var query = default(QueryBuilder).AsReadonly();
            try {
                try { QueryFinallyMayThrow(); }
                catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
            } finally { query = default; }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryCatchInsideCleanupResumesLeave() {
            var query = default(QueryBuilder);
            try { QueryFinallyMayThrow(); }
            finally {
                try { QueryFinallyMayThrow(); }
                catch (System.Exception) { query.AsReadonly(); }
                query.AsReadonly();
            }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryCatchInsideCleanupResumesReturn() {
            var query = default(QueryBuilder).AsReadonly();
            try { return; }
            finally {
                try { query = default; throw null; }
                catch (System.Exception) { query.AsReadonly(); }
                query.Schedule<QueryModeJob, TestComponent>();
            }
        }
        public static void QueryCatchKeepsParentCleanup() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); }
            finally {
                try {
                    try { QueryFinallyMayThrow(); }
                    finally { query = default; throw null; }
                } catch (System.Exception) { query.AsReadonly(); }
                query.Schedule<QueryModeJob, TestComponent>();
            }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryCatchRethrow() {
            var query = default(QueryBuilder);
            try {
                try { throw null; }
                catch (System.Exception) { query.AsReadonly(); throw; }
            } catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryCatchSiblings() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); }
            catch (ArgumentException) { query = default; }
            catch (InvalidOperationException) { query.AsReadonly(); }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryCatchLoop(int count, bool skip) {
            var query = default(QueryBuilder).AsReadonly();
            for (var i = 0; i < count; ++i) {
                try { query.Schedule<QueryModeJob, TestComponent>(); QueryFinallyMayThrow(); }
                catch (System.Exception) { query = default; if (skip) continue; break; }
            }
        }
        public static void QueryCatchSymbolic(QueryBuilder query) {
            try { QueryFinallyMayThrow(); }
            catch (System.Exception) { query.AsReadonly(); }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryCatchArgumentBeforeThrow() {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryModeGenericHelper<QueryModeJob>(query = default); }
            catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryCatchReturnSkipsFollowingCode(bool leave) {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); }
            catch (System.Exception) { if (leave) return; query = default; }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryCatchNestedFunction() {
            try {
                void Local() {
                    var query = default(QueryBuilder).AsReadonly();
                    try { QueryFinallyMayThrow(); }
                    catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
                }
                Local();
            } catch (System.Exception) { QueryFinallyMayThrow(); }
        }

        [TestCase(nameof(QueryCatchReadonly), "1")]
        [TestCase(nameof(QueryCatchValueAtThrow), "0|1")]
        [TestCase(nameof(QueryCatchResetBeforeThrow), "0")]
        [TestCase(nameof(QueryCatchRefMutation), "?")]
        [TestCase(nameof(QueryCatchAfterCleanup), "0")]
        [TestCase(nameof(QueryCatchDoesNotRunOuterCleanupEarly), "1,0")]
        [TestCase(nameof(QueryCatchInsideCleanupResumesLeave), "1")]
        [TestCase(nameof(QueryCatchInsideCleanupResumesReturn), "1")]
        [TestCase(nameof(QueryCatchKeepsParentCleanup), "1,1")]
        [TestCase(nameof(QueryCatchRethrow), "0|1")]
        [TestCase(nameof(QueryCatchSiblings), "0|1")]
        [TestCase(nameof(QueryCatchLoop), "0|1")]
        [TestCase(nameof(QueryCatchSymbolic), "1|p0")]
        [TestCase(nameof(QueryCatchArgumentBeforeThrow), "0")]
        [TestCase(nameof(QueryCatchReturnSkipsFollowingCode), "0|1")]
        public void CatchScheduleModesFollowExceptionalValues(string method, string expected) =>
            SourceScheduleModesFollowQueryValues(method, expected);

        public partial struct CatchReadonlyScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryCatchReadonly();
        }
        public partial struct CatchMixedScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryCatchSiblings();
        }
        public partial struct CatchCleanupScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryCatchAfterCleanup();
        }
        public partial struct CatchNestedFunctionScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryCatchNestedFunction();
        }
        public partial struct CatchGenericScheduleSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) {
                var query = default(QueryBuilder).AsReadonly();
                try { QueryFinallyMayThrow(); }
                catch (System.Exception) { query.Schedule<FinallyGenericExplicitScheduleSystem<T>.Job, T>(); }
            }
        }
        public partial struct CatchNestedFunctionCreationJob : Unity.Jobs.IJob {
            public void Execute() {
                try {
                    void Local() {
                        try { throw null; }
                        catch (System.Exception) { Ent.New(); }
                    }
                    Local();
                } catch (System.Exception) { QueryFinallyMayThrow(); }
            }
        }

        [Test]
        public void CatchNestedFunctionEntityCountsUseTheirOwnOrdinalSpace() {
            var rows = ControlSummary(typeof(CatchNestedFunctionCreationJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            Assert.IsTrue(rows.Single(row => row.StartsWith("C\t", StringComparison.Ordinal)).EndsWith("\t1\t0", StringComparison.Ordinal),
                string.Join("\n", rows));
        }

        [TestCase(typeof(CatchReadonlyScheduleSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(CatchMixedScheduleSystem), typeof(TestComponent), 2)]
        [TestCase(typeof(CatchCleanupScheduleSystem), typeof(TestComponent), 2)]
        [TestCase(typeof(CatchNestedFunctionScheduleSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(CatchGenericScheduleSystem<AotMarker>), typeof(AotMarker), 0)]
        public void CatchScheduleModesKeepTheTypedDependencyUnion(Type system, Type component, int mode) {
            var rows = SystemDependencyRows(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { "C\t" + mode + "\t" + component.AssemblyQualifiedName },
                rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray());
            // The independent catch interpreter proves these scheduling-only
            // systems; both production contracts must now avoid legacy entirely.
            var sync = SynchronizationSummary(system);
            Assert.AreEqual("0", sync[2], string.Join("\n", sync));
            CollectionAssert.Contains(sync, "S\tproven");
            var selected = DependencySelector(system, () => Assert.Fail("Complete catch plans must not invoke legacy."), out _);
            CollectionAssert.AreEqual(rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray(),
                SelectedDependencyRows(selected), "The fallback's unrelated component must not replace the source dependency union.");
            Assert.AreEqual(0, SelectedDependencyErrors(selected).Count);
        }
    }
}
