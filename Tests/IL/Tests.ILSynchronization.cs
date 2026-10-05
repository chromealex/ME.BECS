using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        private static (string status, string[] gaps, string[] unsafeSites, int accesses) ReadILSynchronization(MethodInfo root) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILSynchronization", true);
            var result = analyzer.GetMethod("Analyze", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { root });
            var type = result.GetType();
            return ((string)type.GetField("status").GetValue(result), (string[])type.GetField("gaps").GetValue(result),
                (string[])type.GetField("unsafeSites").GetValue(result), (int)type.GetField("accesses").GetValue(result));
        }

        [TestCase(typeof(SyncProofCopiesSystem), "proven")]
        [TestCase(typeof(SyncProofBranchSystem), "unproven")]
        [TestCase(typeof(SyncProofStaleSystem), "unproven")]
        [TestCase(typeof(SyncProofDefaultSystem), "unproven")]
        [TestCase(typeof(SyncProofNewHandleSystem), "proven")]
        [TestCase(typeof(SyncProofCombinedSystem), "proven")]
        [TestCase(typeof(SyncProofOnlyOneBranchSystem), "unproven")]
        [TestCase(typeof(SyncProofGenericHandleSystem), "proven")]
        [TestCase(typeof(SyncProofOutHandleSystem), "proven")]
        [TestCase(typeof(SyncProofRefContextSystem), "proven")]
        [TestCase(typeof(SyncProofContextCopySystem), "unproven")]
        [TestCase(typeof(SyncProofLateCompleteSystem), "unproven")]
        [TestCase(typeof(SyncProofWaitQuerySystem), "proven")]
        [TestCase(typeof(SyncProofGenericSystem<AotMarker>), "proven")]
        [TestCase(typeof(SyncProofExplicitSystem), "proven")]
        [TestCase(typeof(SyncProofLoopSystem), "proven")]
        [TestCase(typeof(SyncLoopChainSystem), "proven")]
        [TestCase(typeof(SyncLoopStaleSystem), "unproven")]
        [TestCase(typeof(SyncLoopIndependentSystem), "unproven")]
        [TestCase(typeof(SyncLoopCombinedSystem), "proven")]
        [TestCase(typeof(SyncLoopCompleteEachSystem), "proven")]
        [TestCase(typeof(SyncLoopMaySkipCompleteSystem), "unproven")]
        [TestCase(typeof(SyncLoopDoWhileSystem), "proven")]
        [TestCase(typeof(SyncLoopHelperChainSystem), "proven")]
        [TestCase(typeof(SyncLoopReturnedIndependentSystem), "unproven")]
        [TestCase(typeof(CombineTwoSyncSystem), "proven")]
        [TestCase(typeof(CombineThreeSyncSystem), "proven")]
        [TestCase(typeof(CombineWithoutCompleteSyncSystem), "unproven")]
        [TestCase(typeof(CombineMissingInputSyncSystem), "unproven")]
        [TestCase(typeof(FieldBeforeBaseSystem), "unproven")]
        [TestCase(typeof(FieldAfterCallerCompletionSystem), "proven")]
        [TestCase(typeof(ThisArgumentCompletionSystem), "proven")]
        [TestCase(typeof(BaseArgumentCompletionSystem), "proven")]
        [TestCase(typeof(BaseArgumentAfterFieldSystem), "unproven")]
        [TestCase(typeof(PendingConstructorInitializerSystem), "unproven")]
        [TestCase(typeof(CompletedConstructorInitializerSystem), "proven")]
        [TestCase(typeof(ConditionalConstructorInitializerSystem), "proven")]
        [TestCase(typeof(PendingBranchConstructorInitializerSystem), "unproven")]
        [TestCase(typeof(ImplicitBaseSchedulingSystem), "unproven")]
        [TestCase(typeof(GenericIntConstructorSystem), "proven")]
        [TestCase(typeof(EventInitializerConstructorSystem), "unproven")]
        [TestCase(typeof(ImplicitEventInitializerConstructorSystem), "unproven")]
        [TestCase(typeof(GenericConstructorSchedulingSystem), "unproven")]
        [TestCase(typeof(DefaultGenericHandleSystem), "proven")]
        [TestCase(typeof(SyncProofFinallySystem), "unproven")]
        [TestCase(typeof(ExceptionConstructorSystem), "proven")]
        [TestCase(typeof(FinallyCatchesSystem), "unproven")]
        [TestCase(typeof(FinallyFiltersSystem), "unproven")]
        public void ILSynchronizationTracksActualHandleCoverageWithoutExecutingBodies(Type system, string status) {
            var root = system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
            var result = ReadILSynchronization(root);
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.Greater(result.accesses, 0);
            Assert.AreEqual(status == "unproven", result.unsafeSites.Length != 0);
        }

        [TestCase(typeof(SyncProofUnknownSystem), "DelegateInvoke")]
        [TestCase(typeof(AllocatorRefBeforeCompletionSystem), "RefReturn")]
        [TestCase(typeof(AllocatorRefAfterCompletionSystem), "RefReturn")]
        [TestCase(typeof(Tests_Components_Destroy.TestSystem), "UnclosedDestroyRegistry")]
        [TestCase(typeof(Tests_Components_Destroy.TestSetSystem), "UnclosedDestroyRegistry")]
        [TestCase(typeof(DestroySynchronizedSystem), "UnclosedDestroyRegistry")]
        [TestCase(typeof(GenericHandleConstructorSystem), "OpaqueHandleStorage")]
        [TestCase(typeof(StaticInitializerConstructorSystem), "TypeInitialization")]
        public void ILSynchronizationUnsupportedPathsCannotPublishProof(Type system, string gap) {
            var result = ReadILSynchronization(system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single());
            Assert.AreEqual("incomplete", result.status);
            Assert.IsTrue(result.gaps.Any(reason => reason.StartsWith(gap, StringComparison.Ordinal)), string.Join("\n", result.gaps));
        }

        [TestCase(typeof(UnitySchedulingTerminalsSystem), "proven", false)]
        [TestCase(typeof(SchedulingExceptionUnionSystem), "proven", false)]
        [TestCase(typeof(SyncCaptureReadonlySystem), "unproven", true)]
        public void ILSynchronizationClosesAuditedCompilerSummaryGaps(Type system, string status, bool hasAccess) {
            // Defer-count addresses do not execute the deferred job body here.
            // A readonly context receiver, conversely, is a defensive copy: its
            // dependency update cannot cover work through the original context.
            var result = ReadILSynchronization(system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single());
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(hasAccess, result.accesses > 0);
        }

        private static partial class ILHandleFixtures {
            private partial struct EmptyJob : IJob { public void Execute() { } }
            public struct HandleBox { public JobHandle handle; }
            private static class EffectfulInitializer {
                static EffectfulInitializer() => throw new InvalidOperationException("IL analysis must not execute this initializer");
                public static void Complete(ref SystemContext context) => context.dependsOn.Complete();
            }
            public static void Combined(ref SystemContext context, in Ent ent) {
                var first = default(EmptyJob).Schedule(context.dependsOn);
                var second = default(EmptyJob).Schedule(context.dependsOn);
                JobHandle.CompleteAll(ref first, ref second);
                ent.Set(default(TestComponent));
            }
            public static void Poll(ref SystemContext context, in Ent ent) {
                _ = context.dependsOn.IsCompleted;
                JobHandle.ScheduleBatchedJobs();
                ent.Set(default(TestComponent));
            }
            public static void Added(ref SystemContext context, in Ent ent) {
                var work = default(EmptyJob).Schedule(context.dependsOn);
                context.AddDependency(work);
                context.dependsOn.Complete();
                ent.Set(default(TestComponent));
            }
            public static void StaleCopy(ref SystemContext context, in Ent ent) {
                var old = context.dependsOn;
                context.SetDependency(default(EmptyJob).Schedule(context.dependsOn));
                old.Complete();
                ent.Set(default(TestComponent));
            }
            public static void Aliased(ref SystemContext context, in Ent ent) {
                ref var alias = ref context;
                alias.dependsOn.Complete();
                ent.Set(default(TestComponent));
            }
            private static void Both(ref JobHandle first, ref JobHandle second) {
                first = default;
                second.Complete();
            }
            public static void SameRefArguments(ref SystemContext context, in Ent ent) {
                var work = context.dependsOn;
                Both(ref work, ref work);
                ent.Set(default(TestComponent));
            }
            public static void DistinctRefArguments(ref SystemContext context, in Ent ent) {
                var first = context.dependsOn;
                var second = first;
                Both(ref first, ref second);
                ent.Set(default(TestComponent));
            }
            public static void CompletedIteration(ref SystemContext context, in Ent ent, bool repeat) {
                while (repeat) {
                    var current = default(EmptyJob).Schedule(context.dependsOn);
                    current.Complete();
                    ent.Set(default(TestComponent));
                }
            }
            public static void UnchainedIteration(ref SystemContext context, in Ent ent, bool repeat) {
                var saved = default(JobHandle);
                while (repeat) {
                    var current = default(EmptyJob).Schedule(context.dependsOn);
                    saved.Complete();
                    ent.Set(default(TestComponent));
                    saved = current;
                }
            }
            [SafetyCheck(RefOp.ReadWrite)]
            private static JobHandle AnnotatedSchedule<T>(JobHandle dependsOn) where T : unmanaged, IComponent =>
                default(EmptyJob).Schedule(dependsOn);
            public static void UserAnnotation(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                _ = AnnotatedSchedule<TestComponent>(context.dependsOn);
                ent.Set(default(TestComponent));
            }
            public static void StaticInitializer(ref SystemContext context, in Ent ent) {
                EffectfulInitializer.Complete(ref context);
                ent.Set(default(TestComponent));
            }
            public static void Field(ref SystemContext context, in Ent ent, HandleBox storage) {
                storage.handle.Complete();
                ent.Set(default(TestComponent));
            }
            public static void Array(ref SystemContext context, in Ent ent, JobHandle[] storage) {
                storage[0].Complete();
                ent.Set(default(TestComponent));
            }
            public static unsafe void Pointer(ref SystemContext context, TestComponent* storage) {
                _ = *storage;
            }
            public static void Borrow(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                ref var value = ref ent.Get<TestComponent>();
                _ = default(EmptyJob).Schedule(context.dependsOn);
                value = default;
            }
            public static void DestroyOneShot(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                ent.SetOneShot(default(DestroyExplicitWrites));
            }
            public static void DestroyTag(ref SystemContext context, in Ent ent, bool present) {
                context.dependsOn.Complete();
                ent.SetTag<DestroyExplicitWrites>(present);
            }
        }

        [TestCase(nameof(ILHandleFixtures.Combined), "proven")]
        [TestCase(nameof(ILHandleFixtures.Poll), "unproven")]
        [TestCase(nameof(ILHandleFixtures.Added), "proven")]
        [TestCase(nameof(ILHandleFixtures.StaleCopy), "unproven")]
        [TestCase(nameof(ILHandleFixtures.Aliased), "proven")]
        [TestCase(nameof(ILHandleFixtures.SameRefArguments), "unproven")]
        [TestCase(nameof(ILHandleFixtures.DistinctRefArguments), "proven")]
        [TestCase(nameof(ILHandleFixtures.CompletedIteration), "proven")]
        [TestCase(nameof(ILHandleFixtures.UnchainedIteration), "unproven")]
        [TestCase(nameof(ILHandleFixtures.UserAnnotation), "unproven")]
        public void ILSynchronizationPreservesAliasesCopiesAndRepeatedScheduleGenerations(string method, string status) {
            var result = ReadILSynchronization(typeof(ILHandleFixtures).GetMethod(method));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            if (status != "incomplete") CollectionAssert.IsEmpty(result.gaps);
        }

        [TestCase(nameof(ILHandleFixtures.StaticInitializer), "TypeInitialization")]
        [TestCase(nameof(ILHandleFixtures.Field), "OpaqueHandleStorage")]
        [TestCase(nameof(ILHandleFixtures.Array), "UnsupportedOpcode ldelema")]
        [TestCase(nameof(ILHandleFixtures.Pointer), "OpaqueStorageRead")]
        [TestCase(nameof(ILHandleFixtures.Borrow), "OpaqueStorageWrite")]
        [TestCase(nameof(ILHandleFixtures.DestroyOneShot), "UnclosedDestroyRegistry")]
        [TestCase(nameof(ILHandleFixtures.DestroyTag), "UnclosedDestroyRegistry")]
        public void ILSynchronizationOpaqueEffectsNeverCountAsCompletedWork(string method, string gap) {
            var result = ReadILSynchronization(typeof(ILHandleFixtures).GetMethod(method));
            Assert.AreEqual("incomplete", result.status);
            Assert.IsTrue(result.gaps.Any(reason => reason.StartsWith(gap, StringComparison.Ordinal)), string.Join("\n", result.gaps));
        }

        private static (string totals, string details) ILSynchronizationReport(MethodInfo[] roots, Func<MethodInfo, int, int, bool> cancel = null) {
            var validator = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorILSynchronizationValidation", true);
            var report = validator.GetMethod("BuildReport", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { roots, cancel });
            return ((string)report.GetType().GetField("totals").GetValue(report), (string)report.GetType().GetField("details").GetValue(report));
        }

        [Test]
        public void ILSynchronizationReportKeepsUnknownSeparateAndHasStableOrdering() {
            var roots = new[] { nameof(ILHandleFixtures.Combined), nameof(ILHandleFixtures.Poll), nameof(ILHandleFixtures.Pointer) }
                .Select(name => typeof(ILHandleFixtures).GetMethod(name)).ToArray();
            var report = ILSynchronizationReport(roots);
            StringAssert.Contains("analyzed=3; selected=3; cancelled=False", report.totals);
            StringAssert.Contains("IL: proven=1; unproven=1; incomplete=1", report.totals);
            StringAssert.Contains("Source: complete=0; incomplete/unavailable=3", report.totals);
            StringAssert.Contains("Gap roots: OpaqueStorageRead=1", report.totals);
            StringAssert.Contains("OpaqueStorageRead", report.details);
            Assert.AreEqual(report, ILSynchronizationReport(roots.Reverse().Concat(roots).ToArray()),
                "Input order and duplicates must not affect the diagnostic snapshot.");
        }

        [Test]
        public void ILSynchronizationReportComparesExactLifecycleRootsWithoutExecutingThem() {
            var roots = new[] { typeof(SyncProofCopiesSystem), typeof(SyncProofBranchSystem) }
                .Select(type => type.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single()).ToArray();
            var report = ILSynchronizationReport(roots);
            StringAssert.Contains("IL: proven=1; unproven=1; incomplete=0", report.totals);
            #if BECS_SOURCE_ANALYSIS_DIAGNOSTICS
            StringAssert.Contains("Source: complete=2; incomplete/unavailable=0", report.totals);
            StringAssert.Contains("Both complete: compared=2; different=0; IL proven/source unproven=0", report.totals);
            #else
            StringAssert.Contains("Source: complete=0; incomplete/unavailable=2", report.totals);
            StringAssert.Contains("Both complete: compared=0; different=0; IL proven/source unproven=0", report.totals);
            #endif
        }

        [Test]
        public void ILSynchronizationReportMarksCancellationAsPartialCoverage() {
            var roots = new[] { nameof(ILHandleFixtures.Combined), nameof(ILHandleFixtures.Poll) }
                .Select(name => typeof(ILHandleFixtures).GetMethod(name)).ToArray();
            var report = ILSynchronizationReport(roots, (_, index, __) => index != 0);
            StringAssert.Contains("analyzed=1; selected=2; cancelled=True", report.totals);
            StringAssert.Contains("Both complete: compared=0", report.totals);
        }
    }
}
