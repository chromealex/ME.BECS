using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata only. Do not execute the systems or their throwing helpers.
        public static void CatchResetHandle(ref JobHandle handle) { handle = default; throw null; }
        public static void CatchCompleteThenThrow(ref JobHandle handle) { handle.Complete(); throw null; }
        public static JobHandle CatchReturnHandle(JobHandle handle) {
            try { return handle; }
            finally { try { throw null; } catch (System.Exception) { FinallyTemporaryJob(); } }
        }
        public static T CatchReturnGeneric<T>(T handle) {
            try { return handle; }
            finally { try { throw null; } catch (System.Exception) { FinallyTemporaryJob(); } }
        }
        public static JobHandle CatchReturnBeforeReplacement(ref JobHandle handle) {
            try { return handle; }
            finally { try { throw null; } catch (System.Exception) { handle = FinallySchedule(); } }
        }
        public static JobHandle CatchReturnNestedCleanup(JobHandle handle) {
            try { return handle; }
            finally {
                try { try { FinallyThrow(); } finally { throw null; } }
                catch (System.Exception) { FinallyTemporaryJob(); }
            }
        }
        public static JobHandle CatchReplacesReturn(JobHandle handle) {
            try { try { return handle; } finally { throw null; } }
            catch (System.Exception) { return default; }
        }

        public partial struct CatchBeforeCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { try { context.dependsOn.Complete(); } catch (System.Exception) { FinallyWrite(); } }
        }
        public partial struct CatchAfterCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { FinallyThrow(); } catch (System.Exception exception) { _ = exception; FinallyWrite(); }
            }
        }
        public partial struct CatchCompletesInputSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { try { FinallyThrow(); } catch (System.Exception) { context.dependsOn.Complete(); FinallyWrite(); } }
        }
        public partial struct CatchHelperSchedulesSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { FinallyScheduleThenThrow(); } catch (System.Exception) { FinallyWrite(); }
            }
        }
        public partial struct CatchHelperRefSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                var handle = default(JobHandle);
                try { FinallyRefThenThrow(ref handle); } catch (System.Exception) { handle.Complete(); FinallyWrite(); }
            }
        }
        public partial struct CatchLostRefSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var handle = context.dependsOn;
                try { CatchResetHandle(ref handle); } catch (System.Exception) { handle.Complete(); FinallyWrite(); }
            }
        }
        public partial struct CatchRetainedRefSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var handle = context.dependsOn;
                try { CatchCompleteThenThrow(ref handle); } catch (System.Exception) { handle.Complete(); FinallyWrite(); }
            }
        }
        public partial struct CatchReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { CatchReturnHandle(context.dependsOn).Complete(); FinallyWrite(); }
        }
        public partial struct CatchGenericReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { CatchReturnGeneric(context.dependsOn).Complete(); FinallyWrite(); }
        }
        public partial struct CatchReplacedHandleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var handle = context.dependsOn;
                CatchReturnBeforeReplacement(ref handle).Complete();
                FinallyWrite();
            }
        }
        public partial struct CatchNestedReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { CatchReturnNestedCleanup(context.dependsOn).Complete(); FinallyWrite(); }
        }
        public partial struct CatchCanceledReturnSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { CatchReplacesReturn(context.dependsOn).Complete(); FinallyWrite(); }
        }
        public partial struct CatchThrowingCompletionSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { try { FinallyThrow(); } finally { context.dependsOn.Complete(); } }
                catch (System.Exception) { FinallyWrite(); }
            }
        }
        public partial struct CatchScheduledCleanupSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { try { FinallyThrow(); } catch (System.Exception) { _ = FinallySchedule(); } }
                finally { FinallyWrite(); }
            }
        }
        public partial struct CatchLoopSchedulesSystem : IUpdate {
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                while (this.repeat) {
                    try { FinallyWrite(); FinallyThrow(); }
                    catch (System.Exception) { _ = FinallySchedule(); continue; }
                }
            }
        }
        public partial struct CatchLoopCompletesSystem : IUpdate {
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                while (this.repeat) {
                    try { FinallyThrow(); }
                    catch (System.Exception) { context.dependsOn.Complete(); FinallyWrite(); continue; }
                }
            }
        }
        public partial struct CatchSiblingCompletionSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { FinallyThrow(); }
                catch (ArgumentException) { context.dependsOn.Complete(); }
                catch (System.Exception) { FinallyWrite(); }
            }
        }
        public partial struct CatchRethrowSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { try { FinallyThrow(); } catch (System.Exception) { context.dependsOn.Complete(); throw; } }
                catch (System.Exception) { FinallyWrite(); }
            }
        }
        public partial struct CatchRethrowCompletedSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { try { FinallyThrow(); } catch (System.Exception) { throw; } }
                catch (System.Exception) { FinallyWrite(); }
            }
        }
        public partial struct CatchGenericSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) {
                try { FinallyThrow(); }
                catch (System.Exception) { context.dependsOn.Complete(); default(Ent).Set(default(T)); }
            }
        }
        public partial struct CatchUsingSchedulesSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { FinallyUsing(new FinallySchedulingScope()); }
                catch (System.Exception) { FinallyWrite(); }
            }
        }
        public partial struct CatchEmptyTrySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { } catch (System.Exception) { _ = FinallySchedule(); }
                FinallyWrite();
            }
        }
        public partial struct CatchUnreachableSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
#pragma warning disable CS0162
                if (false) { try { FinallyThrow(); } catch (System.Exception) { _ = FinallySchedule(); } }
#pragma warning restore CS0162
                FinallyWrite();
            }
        }
        public partial struct CatchUntypedSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { FinallyThrow(); } catch { context.dependsOn.Complete(); FinallyWrite(); }
            }
        }
        public partial struct CatchPartiallyDeadSystem : IUpdate {
            public bool earlyReturn;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { FinallyThrow(); }
                catch (System.Exception) {
                    FinallyWrite();
                    if (this.earlyReturn) return;
#pragma warning disable CS0162
                    if (false) _ = FinallySchedule();
#pragma warning restore CS0162
                }
                FinallyWrite();
            }
        }
        public partial struct CatchTerminatingCleanupSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try {
                    try { } finally { _ = FinallySchedule(); throw null; }
#pragma warning disable CS0162
                    FinallyWrite();
#pragma warning restore CS0162
                } catch (System.Exception) { FinallyWrite(); }
            }
        }
        public partial struct CatchNestedFunctionSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                static void Local(JobHandle input) {
                    try { FinallyThrow(); }
                    catch (System.Exception) { input.Complete(); FinallyWrite(); }
                }
                context.dependsOn.Complete();
                try { Local(context.dependsOn); }
                finally { FinallyWrite(); }
            }
        }

        [TestCase(typeof(CatchBeforeCompleteSystem), "unproven")]
        [TestCase(typeof(CatchAfterCompleteSystem), "proven")]
        [TestCase(typeof(CatchCompletesInputSystem), "proven")]
        [TestCase(typeof(CatchHelperSchedulesSystem), "unproven")]
        [TestCase(typeof(CatchHelperRefSystem), "unproven")]
        [TestCase(typeof(CatchLostRefSystem), "unproven")]
        [TestCase(typeof(CatchRetainedRefSystem), "proven")]
        [TestCase(typeof(CatchReturnSystem), "proven")]
        [TestCase(typeof(CatchGenericReturnSystem), "proven")]
        [TestCase(typeof(CatchReplacedHandleSystem), "unproven")]
        [TestCase(typeof(CatchNestedReturnSystem), "proven")]
        [TestCase(typeof(CatchCanceledReturnSystem), "unproven")]
        [TestCase(typeof(CatchThrowingCompletionSystem), "unproven")]
        [TestCase(typeof(CatchScheduledCleanupSystem), "unproven")]
        [TestCase(typeof(CatchLoopSchedulesSystem), "unproven")]
        [TestCase(typeof(CatchLoopCompletesSystem), "proven")]
        [TestCase(typeof(CatchSiblingCompletionSystem), "unproven")]
        [TestCase(typeof(CatchRethrowSystem), "unproven")]
        [TestCase(typeof(CatchRethrowCompletedSystem), "proven")]
        [TestCase(typeof(CatchGenericSystem<AotMarker>), "proven")]
        [TestCase(typeof(CatchUsingSchedulesSystem), "unproven")]
        [TestCase(typeof(CatchEmptyTrySystem), "proven")]
        [TestCase(typeof(CatchUnreachableSystem), "proven")]
        [TestCase(typeof(CatchUntypedSystem), "proven")]
        [TestCase(typeof(CatchNestedFunctionSystem), "proven")]
        [TestCase(typeof(CatchPartiallyDeadSystem), "proven")]
        [TestCase(typeof(CatchTerminatingCleanupSystem), "unproven")]
        public void CatchSynchronizationRetainsPartialEffectsAndReturnContinuations(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.Greater(int.Parse(rows.Single(row => row.StartsWith("A\t", StringComparison.Ordinal)).Substring(2)), 0);
            Assert.IsNotNull(DependencySelector(system, () => Assert.Fail("Complete catch contracts must not read legacy IL."), out _));
        }

        [Test]
        public void CatchSynchronizationExportsVersionedHandlerRoutes() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var rows = typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    attribute.Value.StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.CatchReturnNestedCleanup(", StringComparison.Ordinal)).Value.Split('\n');
            var flags = rows[1].Split(',');
            CollectionAssert.Contains(flags, "sync-catch-schema=1");
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(flags.Single(flag => flag.StartsWith("sync-flow=", StringComparison.Ordinal)).Substring(10)));
            Assert.IsTrue(text.Split('\n').Any(row => row.StartsWith("K\t", StringComparison.Ordinal)), text);
            Assert.IsTrue(text.Split('\n').Any(row => row.StartsWith("J\t", StringComparison.Ordinal)), text);
            Assert.IsFalse(text.Contains("G\tExceptionControlFlow"), text);
        }
    }
}
