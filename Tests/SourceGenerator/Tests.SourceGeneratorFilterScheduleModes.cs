using System;
using System.Linq;
using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Inspect metadata only. Never execute these builders, jobs or exceptions.
        public static bool QueryFilterAccept(QueryBuilder query) => true;
        public static bool QueryFilterHandle(JobHandle handle) => true;
        public static bool QueryFilterThrow(QueryBuilder query) => throw null;
        public static bool QueryFilterRef(ref QueryBuilder query) { query = default; return true; }
        public static Func<bool> QueryFilterUnknownCallback;

        public static void QueryFilterBeforeCleanup() {
            var query = default(QueryBuilder).AsReadonly();
            try { try { throw null; } finally { query = default; } }
            catch (System.Exception) when (QueryFilterHandle(query.Schedule<QueryModeJob, TestComponent>())) {
                query.Schedule<QueryModeJob, TestComponent>();
            }
        }
        public static void QueryFilterChangesCleanup() {
            var query = default(QueryBuilder).AsReadonly();
            try { try { throw null; } finally { query.Schedule<QueryModeJob, TestComponent>(); } }
            catch (System.Exception) when (QueryFilterAccept(query = default)) { }
        }
        public static void QueryFilterFalsePreservesMutation() {
            var query = default(QueryBuilder).AsReadonly();
            try { throw null; }
            catch (ArgumentException) when (QueryFilterAccept(query = default) && false) { }
            catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFilterThrowPreservesMutation() {
            var query = default(QueryBuilder).AsReadonly();
            try { throw null; }
            catch (ArgumentException) when (QueryFilterThrow(query = default)) { }
            catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFilterReadonlyCatch(bool accept) {
            var query = default(QueryBuilder).AsReadonly();
            try { throw null; }
            catch (System.Exception) when (accept) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFilterSetsReadonlyOnAllPaths(bool accept) {
            var query = default(QueryBuilder);
            try { query.AsReadonly(); }
            catch (ArgumentException) when (accept) { query.AsReadonly(); }
            catch (System.Exception) { query.AsReadonly(); }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryFilterResetBeforeThrow() {
            var query = default(QueryBuilder).AsReadonly();
            try { query = default; throw null; }
            catch (System.Exception) when (QueryFilterHandle(query.Schedule<QueryModeJob, TestComponent>())) { }
        }
        public static void QueryFilterLoop(int count) {
            var query = default(QueryBuilder).AsReadonly();
            for (var i = 0; i < count; ++i) {
                try { throw null; }
                catch (System.Exception) when (QueryFilterHandle(query.Schedule<QueryModeJob, TestComponent>())) { query = default; }
            }
        }
        public static void QueryFilterSymbolic(QueryBuilder query, bool accept) {
            try { throw null; }
            catch (System.Exception) when (accept) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFilterRefMutation() {
            var query = default(QueryBuilder).AsReadonly();
            try { throw null; }
            catch (System.Exception) when (QueryFilterRef(ref query)) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFilterRethrow() {
            var query = default(QueryBuilder).AsReadonly();
            try {
                try { throw null; }
                catch (ArgumentException) when (QueryFilterAccept(query = default)) { throw; }
            } catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFilterSiblingOrder() {
            var query = default(QueryBuilder).AsReadonly();
            try { throw null; }
            catch (ArgumentException) when (QueryFilterAccept(query = default) && false) { }
            catch (System.Exception) when (QueryFilterHandle(query.Schedule<QueryModeJob, TestComponent>())) { }
        }
        public static void QueryFilterThrowInCleanupRepeatsSearch() {
            var query = default(QueryBuilder).AsReadonly();
            try { try { throw null; } finally { query = default; throw null; } }
            catch (System.Exception) when (QueryFilterHandle(query.Schedule<QueryModeJob, TestComponent>())) { }
        }
        public static void QueryFilterInsideCleanupKeepsReturn(bool accept) {
            var query = default(QueryBuilder).AsReadonly();
            try { return; }
            finally {
                try { throw null; }
                catch (System.Exception) when (accept) { query = default; }
                query.Schedule<QueryModeJob, TestComponent>();
            }
        }
        public static void QueryFilterNestedCleanupParent(bool accept) {
            var query = default(QueryBuilder).AsReadonly();
            try { QueryFinallyMayThrow(); }
            finally {
                try { try { throw null; } finally { query = default; throw null; } }
                catch (System.Exception) when (accept) { query.AsReadonly(); }
                query.Schedule<QueryModeJob, TestComponent>();
            }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        public static void QueryFilterUnknownDispatch() {
            var query = default(QueryBuilder).AsReadonly();
            try { throw null; }
            catch (System.Exception) when (QueryFilterUnknownCallback()) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        public static void QueryFilterNestedFunction() {
            try {
                static void Local() {
                    var query = default(QueryBuilder).AsReadonly();
                    try { throw null; }
                    catch (System.Exception) when (QueryFilterAccept(query)) { query.Schedule<QueryModeJob, TestComponent>(); }
                }
                Local();
            } catch (System.Exception) { QueryFinallyMayThrow(); }
        }

        [TestCase(nameof(QueryFilterBeforeCleanup), "1,0")]
        [TestCase(nameof(QueryFilterChangesCleanup), "0|1")]
        [TestCase(nameof(QueryFilterFalsePreservesMutation), "0|1")]
        [TestCase(nameof(QueryFilterThrowPreservesMutation), "0|1")]
        [TestCase(nameof(QueryFilterReadonlyCatch), "1")]
        [TestCase(nameof(QueryFilterSetsReadonlyOnAllPaths), "1")]
        [TestCase(nameof(QueryFilterResetBeforeThrow), "0")]
        [TestCase(nameof(QueryFilterLoop), "0|1")]
        [TestCase(nameof(QueryFilterSymbolic), "p0")]
        [TestCase(nameof(QueryFilterRefMutation), "?")]
        [TestCase(nameof(QueryFilterRethrow), "0|1")]
        [TestCase(nameof(QueryFilterSiblingOrder), "0|1")]
        [TestCase(nameof(QueryFilterThrowInCleanupRepeatsSearch), "0|1")]
        [TestCase(nameof(QueryFilterInsideCleanupKeepsReturn), "0")]
        [TestCase(nameof(QueryFilterNestedCleanupParent), "1,1")]
        [TestCase(nameof(QueryFilterUnknownDispatch), "?")]
        public void FilterScheduleModesFollowFirstPassEffects(string method, string expected) =>
            SourceScheduleModesFollowQueryValues(method, expected);

        public partial struct FilterReadonlyScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryFilterReadonlyCatch(true);
        }
        public partial struct FilterCleanupScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryFilterChangesCleanup();
        }
        public partial struct FilterSiblingScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryFilterSiblingOrder();
        }
        public partial struct FilterNestedScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryFilterNestedFunction();
        }
        public partial struct FilterGenericScheduleSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public bool accept;
            public void OnUpdate(ref SystemContext context) {
                var query = default(QueryBuilder).AsReadonly();
                try { throw null; }
                catch (System.Exception) when (this.accept) { query.Schedule<FinallyGenericExplicitScheduleSystem<T>.Job, T>(); }
            }
        }

        [TestCase(typeof(FilterReadonlyScheduleSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(FilterCleanupScheduleSystem), typeof(TestComponent), 2)]
        [TestCase(typeof(FilterSiblingScheduleSystem), typeof(TestComponent), 2)]
        [TestCase(typeof(FilterNestedScheduleSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(FilterGenericScheduleSystem<AotMarker>), typeof(AotMarker), 0)]
        public void FilterDependencyUnionDoesNotNeedSynchronizationFallback(Type system, Type component, int mode) {
            var rows = SystemDependencyRows(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { "C\t" + mode + "\t" + component.AssemblyQualifiedName },
                rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray());
            // Ordered JobHandle proof still needs cross-frame first-pass dispatch.
            // Query-mode proof must not erase that gap or use fallback dependencies.
            var sync = SynchronizationSummary(system);
            CollectionAssert.Contains(sync, "S\tincomplete");
            var fallbackCalls = 0;
            var selected = DependencySelector(system, () => ++fallbackCalls, out _);
            Assert.AreEqual(1, fallbackCalls);
            CollectionAssert.AreEqual(rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray(),
                SelectedDependencyRows(selected));
        }
    }
}
