using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Source metadata only. These callbacks, jobs and throwing helpers are never executed.
        public static void FinallyWrite() => default(Ent).Set(new TestComponent());
        public static JobHandle FinallySchedule() => IJobExtensions.Schedule(default(ControlFirstJob), default);
        public static void FinallyThrow() => throw null;
        public static void FinallyScheduleThenThrow() { _ = FinallySchedule(); throw null; }
        public static void FinallyRefThenThrow(ref JobHandle handle) { handle = FinallySchedule(); throw null; }
        public static void FinallyResetThenComplete(ref JobHandle handle) { var old = handle; handle = default; old.Complete(); throw null; }
        public static void FinallyTemporaryJob() => FinallySchedule().Complete();
        public static JobHandle FinallyReturn(JobHandle handle) {
            try { return handle; } finally { FinallyTemporaryJob(); }
        }
        public static T FinallyReturnGeneric<T>(T handle) {
            try { return handle; } finally { FinallyTemporaryJob(); }
        }
        public static JobHandle FinallyReturnBeforeReplacement(ref JobHandle handle) {
            try { return handle; } finally { handle = FinallySchedule(); }
        }
        public static JobHandle FinallyReturnCompleted(JobHandle handle) {
            try { return handle; } finally { handle.Complete(); }
        }
        public struct FinallyWritingScope : IDisposable { public void Dispose() => FinallyWrite(); }
        public struct FinallySchedulingScope : IDisposable { public void Dispose() => _ = FinallySchedule(); }
        public static void FinallyUsing<T>(T scope) where T : struct, IDisposable { using (scope) { } }

        public partial struct FinallyBeforeCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { try { context.dependsOn.Complete(); } finally { FinallyWrite(); } }
        }
        public partial struct FinallyAfterCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); try { FinallyWrite(); } finally { FinallyWrite(); } }
        }
        public partial struct FinallyCompletesSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { try { } finally { context.dependsOn.Complete(); FinallyWrite(); } }
        }
        public partial struct FinallySchedulesSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); try { } finally { _ = FinallySchedule(); } FinallyWrite(); }
        }
        public partial struct FinallyCompletesScheduledSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                var handle = default(JobHandle);
                try { handle = FinallySchedule(); } finally { handle.Complete(); }
                FinallyWrite();
            }
        }
        public partial struct FinallyReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { FinallyReturn(context.dependsOn).Complete(); FinallyWrite(); }
        }
        public partial struct FinallyGenericReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { FinallyReturnGeneric(context.dependsOn).Complete(); FinallyWrite(); }
        }
        public partial struct FinallyReplacedReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var handle = context.dependsOn;
                FinallyReturnBeforeReplacement(ref handle).Complete();
                FinallyWrite();
            }
        }
        public partial struct FinallyCompletedReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { FinallyReturnCompleted(context.dependsOn).Complete(); FinallyWrite(); }
        }
        public partial struct FinallyNestedSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                var handle = default(JobHandle);
                try { try { } finally { handle = FinallySchedule(); } } finally { handle.Complete(); }
                FinallyWrite();
            }
        }
        public partial struct FinallyThrowingHelperSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); try { FinallyScheduleThenThrow(); } finally { FinallyWrite(); } }
        }
        public partial struct FinallyPureThrowSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); try { FinallyThrow(); } finally { FinallyWrite(); } }
        }
        public partial struct FinallyRefThrowSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                var handle = default(JobHandle);
                try { FinallyRefThenThrow(ref handle); } finally { handle.Complete(); FinallyWrite(); }
            }
        }
        public partial struct FinallyRefResetSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var handle = context.dependsOn;
                try { FinallyResetThenComplete(ref handle); } finally { handle.Complete(); FinallyWrite(); }
            }
        }
        public partial struct FinallyThrowsDuringUnwindSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { try { return; } finally { FinallyScheduleThenThrow(); } }
                finally { FinallyWrite(); }
            }
        }
        public partial struct FinallyCancelsReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { return; } finally { try { throw null; } finally { context.dependsOn.Complete(); FinallyWrite(); } }
            }
        }
        public partial struct FinallyLoopSystem : IUpdate {
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                while (this.repeat) { try { if (this.repeat) continue; break; } finally { FinallyWrite(); } }
                FinallyWrite();
            }
        }
        public partial struct FinallyLoopSchedulesSystem : IUpdate {
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                while (this.repeat) { try { FinallyWrite(); if (this.repeat) continue; break; } finally { _ = FinallySchedule(); } }
                FinallyWrite();
            }
        }
        public partial struct FinallyUsingScopeSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); FinallyUsing(default(FinallyWritingScope)); }
        }
        public partial struct FinallyUsingPendingSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => FinallyUsing(default(FinallyWritingScope));
        }
        public partial struct FinallyUsingSchedulesSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); FinallyUsing(default(FinallySchedulingScope)); FinallyWrite(); }
        }
        public partial struct FinallyGenericSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { default(Ent).Set(default(T)); } finally { FinallyWrite(); }
            }
        }
        public partial struct FinallyAllocatorSystem : IUpdate {
            public ExplicitDispatchAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { AllocateThroughGenericHelper(ref this.allocator); } finally { FinallyWrite(); }
            }
        }
        public partial struct FinallyAllocatorInterruptedSystem : IUpdate {
            public CompletedTryAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { AllocateThroughGenericHelper(ref this.allocator); } finally { FinallyWrite(); }
            }
        }
        public partial struct FinallyAllocatorInCleanupSystem : IUpdate {
            public CompletedTryAllocator allocator;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { } finally { AllocateThroughGenericHelper(ref this.allocator); }
                FinallyWrite();
            }
        }
        public partial struct FinallyCatchesSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { try { FinallyWrite(); } catch (System.Exception) { context.dependsOn.Complete(); } }
        }
        public partial struct FinallyFiltersSystem : IUpdate {
            public bool accept;
            public void OnUpdate(ref SystemContext context) { try { FinallyWrite(); } catch (System.Exception) when (this.accept) { context.dependsOn.Complete(); } }
        }
        public partial struct FinallyQueryModeSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { context.Query().AsReadonly().Schedule<QueryModeJob, TestComponent>(); } finally { FinallyWrite(); }
            }
        }

        [TestCase(typeof(FinallyBeforeCompleteSystem), "unproven")]
        [TestCase(typeof(FinallyAfterCompleteSystem), "proven")]
        [TestCase(typeof(FinallyCompletesSystem), "proven")]
        [TestCase(typeof(FinallySchedulesSystem), "unproven")]
        [TestCase(typeof(FinallyCompletesScheduledSystem), "proven")]
        [TestCase(typeof(FinallyReturnSystem), "proven")]
        [TestCase(typeof(FinallyGenericReturnSystem), "proven")]
        [TestCase(typeof(FinallyReplacedReturnSystem), "unproven")]
        [TestCase(typeof(FinallyCompletedReturnSystem), "proven")]
        [TestCase(typeof(FinallyNestedSystem), "proven")]
        [TestCase(typeof(FinallyThrowingHelperSystem), "unproven")]
        [TestCase(typeof(FinallyPureThrowSystem), "proven")]
        [TestCase(typeof(FinallyRefThrowSystem), "unproven")]
        [TestCase(typeof(FinallyRefResetSystem), "unproven")]
        [TestCase(typeof(FinallyThrowsDuringUnwindSystem), "unproven")]
        [TestCase(typeof(FinallyCancelsReturnSystem), "proven")]
        [TestCase(typeof(FinallyLoopSystem), "proven")]
        [TestCase(typeof(FinallyLoopSchedulesSystem), "unproven")]
        [TestCase(typeof(FinallyUsingScopeSystem), "proven")]
        [TestCase(typeof(FinallyUsingPendingSystem), "unproven")]
        [TestCase(typeof(FinallyUsingSchedulesSystem), "unproven")]
        [TestCase(typeof(ExceptionConstructorSystem), "proven")]
        [TestCase(typeof(FinallyGenericSystem<AotMarker>), "proven")]
        [TestCase(typeof(FinallyAllocatorSystem), "proven")]
        [TestCase(typeof(FinallyAllocatorInterruptedSystem), "unproven")]
        [TestCase(typeof(FinallyAllocatorInCleanupSystem), "proven")]
        [TestCase(typeof(FinallyCatchesSystem), "unproven")]
        public void FinallySynchronizationPreservesNormalAndExceptionalOrdering(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.Greater(int.Parse(rows.Single(row => row.StartsWith("A\t", StringComparison.Ordinal)).Substring(2)), 0);
        }

        [TestCase(typeof(FinallyFiltersSystem))]
        public void FinallySynchronizationDoesNotSilentlySkipFilters(Type system) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\tincomplete");
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tExceptionControlFlow", StringComparison.Ordinal)));
        }

        [TestCase(typeof(FinallyBeforeCompleteSystem))]
        [TestCase(typeof(FinallyAfterCompleteSystem))]
        [TestCase(typeof(FinallyReturnSystem))]
        [TestCase(typeof(FinallyUsingScopeSystem))]
        [TestCase(typeof(InstrumentationUsingSystem))]
        [TestCase(typeof(FinallyGenericSystem<AotMarker>))]
        [TestCase(typeof(FinallyAllocatorSystem))]
        [TestCase(typeof(FinallyAllocatorInCleanupSystem))]
        public void FinallyDependencySelectionDoesNotReadLegacyIL(Type system) {
            Assert.AreEqual("0", SystemDependencyRows(system)[2], string.Join("\n", SystemDependencyRows(system)));
            Assert.IsNotNull(DependencySelector(system, () => Assert.Fail("Complete finally contracts must not fall back to IL."), out _));
        }

        [Test]
        public void FinallyQueryModeHasCompilerProof() {
            var rows = ControlSummary(typeof(FinallyQueryModeSystem), "ME.BECS.SystemScheduleModes.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t1\t" + typeof(QueryModeJob).AssemblyQualifiedName);
            Assert.AreEqual("0", SystemDependencyRows(typeof(FinallyQueryModeSystem))[2]);
        }

        [Test]
        public void FinallyReturnFlowHasExplicitCleanupAndExceptionalHandlers() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var summary = typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    attribute.Value.StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.FinallyReturn(", StringComparison.Ordinal)).Value.Split('\n');
            CollectionAssert.Contains(summary[1].Split(','), "sync-exception-schema=1");
            var payload = summary[1].Split(',').Single(flag => flag.StartsWith("sync-flow=", StringComparison.Ordinal)).Substring(10);
            var rows = Encoding.UTF8.GetString(Convert.FromBase64String(payload)).Split('\n').Select(row => row.Split('\t')).ToArray();
            Assert.IsTrue(rows.Any(row => row[0] == "Y"));
            Assert.IsTrue(rows.Any(row => row[0] == "H"));
            Assert.IsTrue(rows.Where(row => row[0] == "B").Any(row => row.Skip(4).Any(edge => edge.Contains(":Return:"))));
            Assert.IsFalse(rows.Any(row => row[0] == "G"), string.Join("\n", rows.Select(row => string.Join("\t", row))));
        }
    }
}
