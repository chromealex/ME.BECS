using System;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only: Analyze reads these bodies, never invokes them.
        private static partial class ILFilterFixtures {
            private partial struct EmptyJob : IJob { public void Execute() { } }
            private static bool WriteAccept() { FinallyWrite(); return true; }
            private static bool CompleteAccept(ref JobHandle handle) { handle.Complete(); return true; }
            private static bool ResetAccept(ref JobHandle handle) { handle = default; return true; }
            private static bool Reject() => false;
            private static bool ScheduleReject() { _ = default(EmptyJob).Schedule(); return false; }
            private static bool ScheduleThrow() { _ = default(EmptyJob).Schedule(); throw null; }
            private static bool GenericReject<T>() where T : struct, IJob { _ = default(T).Schedule(); return false; }
            private static void ResetInCleanup(ref JobHandle handle) {
                try { throw null; } finally { handle = default; }
            }
            private static void ForwardReset(ref JobHandle handle) => ResetInCleanup(ref handle);
            private static void WriteInCleanup() {
                try { throw null; } finally { FinallyWrite(); }
            }
            private sealed class ResettingConstructor {
                public ResettingConstructor(ref JobHandle handle) {
                    try { throw null; } finally { handle = default; }
                }
            }
            private static bool ThrowWithCleanup(ref JobHandle handle) {
                try { throw null; } finally { handle = default; }
            }
            private static bool NestedFilter(ref JobHandle handle) {
                try { throw null; }
                catch (System.Exception) when (CompleteAccept(ref handle)) { return false; }
            }
            public static void FilterReadsPendingWork(ref SystemContext context) {
                try { throw null; } catch (System.Exception) when (WriteAccept()) { }
            }
            public static void FilterReadsCompletedWork(ref SystemContext context) {
                context.dependsOn.Complete();
                try { throw null; } catch (System.Exception) when (WriteAccept()) { }
            }
            public static void FilterReadsBeforeFinallyCompletes(ref SystemContext context) {
                try { try { throw null; } finally { context.dependsOn.Complete(); } }
                catch (System.Exception) when (WriteAccept()) { }
            }
            public static void LocalFilterCompletesBeforeCleanupReset(ref SystemContext context) {
                var handle = context.dependsOn;
                try { try { throw null; } finally { handle = default; } }
                catch (System.Exception) when (CompleteAccept(ref handle)) { FinallyWrite(); }
            }
            public static void CallerFilterCompletesBeforeCalleeCleanupReset(ref SystemContext context) {
                var handle = context.dependsOn;
                try { ResetInCleanup(ref handle); }
                catch (System.Exception) when (CompleteAccept(ref handle)) { FinallyWrite(); }
            }
            public static void OuterFilterTraversesMultipleFramesBeforeCleanup(ref SystemContext context) {
                var handle = context.dependsOn;
                try { ForwardReset(ref handle); }
                catch (System.Exception) when (CompleteAccept(ref handle)) { FinallyWrite(); }
            }
            public static void CallerFilterPrecedesConstructorCleanup(ref SystemContext context) {
                var handle = context.dependsOn;
                try { _ = new ResettingConstructor(ref handle); }
                catch (System.Exception) when (CompleteAccept(ref handle)) { FinallyWrite(); }
            }
            public static void FilterFailureUnwindsItsOwnHelperBeforeContinuingSearch(ref SystemContext context) {
                var handle = context.dependsOn;
                try { throw null; }
                catch (System.Exception) when (ThrowWithCleanup(ref handle)) { }
                catch { handle.Complete(); FinallyWrite(); }
            }
            public static void NestedFilterFailureReturnsToOriginalSearch(ref SystemContext context) {
                var handle = context.dependsOn;
                try { throw null; }
                catch (System.Exception) when (NestedFilter(ref handle)) { }
                catch { handle.Complete(); FinallyWrite(); }
            }
            public static void FalseFilterPreservesNewWorkForSibling(ref SystemContext context) {
                context.dependsOn.Complete();
                try { throw null; }
                catch (System.Exception) when (ScheduleReject()) { }
                catch { FinallyWrite(); }
            }
            public static void ThrowingFilterPreservesNewWorkForSibling(ref SystemContext context) {
                context.dependsOn.Complete();
                try { throw null; }
                catch (System.Exception) when (ScheduleThrow()) { }
                catch { FinallyWrite(); }
            }
            public static void FalseFilterRunsBeforeCalleeCleanup(ref SystemContext context) {
                context.dependsOn.Complete();
                try { WriteInCleanup(); }
                catch (System.Exception) when (ScheduleReject()) { }
            }
            public static void ThrowingFilterRunsBeforeCalleeCleanup(ref SystemContext context) {
                context.dependsOn.Complete();
                try { WriteInCleanup(); }
                catch (System.Exception) when (ScheduleThrow()) { }
            }
            public static void ConstantFalseFilterNeverEntersHandler(ref SystemContext context) {
                try { throw null; }
                catch (System.Exception) when (Reject()) { FinallyWrite(); }
                catch { }
            }
            public static void SelectedFilterDoesNotRepeatAfterCleanup(ref SystemContext context) {
                var input = context.dependsOn;
                var handle = default(JobHandle);
                try { try { throw null; } finally { handle = input; } }
                catch (System.Exception) when (ResetAccept(ref handle)) { handle.Complete(); FinallyWrite(); }
            }
            public static void NewExceptionDuringCleanupRestartsFilter(ref SystemContext context) {
                var handle = context.dependsOn;
                try { try { throw null; } finally { handle = default; throw null; } }
                catch (System.Exception) when (CompleteAccept(ref handle)) { FinallyWrite(); }
            }
            public static void ClosedGenericFilterPreservesNewWork(ref SystemContext context) {
                context.dependsOn.Complete();
                try { throw null; }
                catch (System.Exception) when (GenericReject<EmptyJob>()) { }
                catch { FinallyWrite(); }
            }
            private static JobHandle ReturnThroughFilteredCleanup(JobHandle handle, bool accept) {
                try { return handle; }
                finally { try { throw null; } catch (System.Exception) when (accept) { } }
            }
            public static void LocalFilterPreservesSuspendedReturn(ref SystemContext context, bool accept) {
                ReturnThroughFilteredCleanup(context.dependsOn, accept).Complete();
                FinallyWrite();
            }
            public static void DelegateFilterIsNotAssumedPure(ref SystemContext context, Func<bool> accept) {
                context.dependsOn.Complete();
                try { throw null; } catch (System.Exception) when (accept()) { FinallyWrite(); }
            }
        }

        [TestCase(nameof(ILFilterFixtures.FilterReadsPendingWork), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.FilterReadsCompletedWork), "proven", true)]
        [TestCase(nameof(ILFilterFixtures.FilterReadsBeforeFinallyCompletes), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.LocalFilterCompletesBeforeCleanupReset), "proven", true)]
        [TestCase(nameof(ILFilterFixtures.CallerFilterCompletesBeforeCalleeCleanupReset), "proven", true)]
        [TestCase(nameof(ILFilterFixtures.OuterFilterTraversesMultipleFramesBeforeCleanup), "proven", true)]
        [TestCase(nameof(ILFilterFixtures.CallerFilterPrecedesConstructorCleanup), "proven", true)]
        [TestCase(nameof(ILFilterFixtures.FilterFailureUnwindsItsOwnHelperBeforeContinuingSearch), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.NestedFilterFailureReturnsToOriginalSearch), "proven", true)]
        [TestCase(nameof(ILFilterFixtures.FalseFilterPreservesNewWorkForSibling), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.ThrowingFilterPreservesNewWorkForSibling), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.FalseFilterRunsBeforeCalleeCleanup), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.ThrowingFilterRunsBeforeCalleeCleanup), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.ConstantFalseFilterNeverEntersHandler), "proven", false)]
        [TestCase(nameof(ILFilterFixtures.SelectedFilterDoesNotRepeatAfterCleanup), "proven", true)]
        [TestCase(nameof(ILFilterFixtures.NewExceptionDuringCleanupRestartsFilter), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.ClosedGenericFilterPreservesNewWork), "unproven", true)]
        [TestCase(nameof(ILFilterFixtures.LocalFilterPreservesSuspendedReturn), "proven", true)]
        public void ILSynchronizationFiltersExecuteFirstPassBeforeUnwinding(string method, string status, bool hasAccess) {
            var result = ReadILSynchronization(typeof(ILFilterFixtures).GetMethod(method));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(hasAccess, result.accesses != 0);
        }

        [Test]
        public void ILSynchronizationUnknownFilterCannotPublishProof() {
            var result = ReadILSynchronization(typeof(ILFilterFixtures).GetMethod(nameof(ILFilterFixtures.DelegateFilterIsNotAssumedPure)));
            Assert.AreEqual("incomplete", result.status);
            Assert.IsTrue(Array.Exists(result.gaps, gap => gap.StartsWith("DelegateInvoke", StringComparison.Ordinal)), string.Join("\n", result.gaps));
        }
    }
}
