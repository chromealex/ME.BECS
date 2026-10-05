using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
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
        public void ILSynchronizationCatchPreservesPartialEffectsAndReturnContinuations(Type system, string status) {
            var result = ReadILSynchronization(system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single());
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.Greater(result.accesses, 0);
            Assert.AreEqual(status == "unproven", result.unsafeSites.Length != 0);
        }

        // Only the IL is analyzed. No job or throwing helper is invoked.
        private static partial class ILCatchFixtures {
            private partial struct EmptyJob : IJob { public void Execute() { } }
            private static JobHandle NestedCleanup(JobHandle handle) {
                try { return handle; }
                finally {
                    try {
                        try { throw null; }
                        finally { throw null; }
                    } catch { }
                }
            }
            public static void ReturnSurvivesCaughtInnerCleanup(ref SystemContext context, in Ent ent) {
                NestedCleanup(context.dependsOn).Complete();
                ent.Set(default(TestComponent));
            }
            public static void CatchDoesNotReenterItsSibling(ref SystemContext context, in Ent ent, System.Exception error) {
                context.dependsOn.Complete();
                try { throw error; }
                catch (ArgumentException) { _ = default(EmptyJob).Schedule(); throw; }
                catch (System.Exception) { ent.Set(default(TestComponent)); }
            }
            public static void OuterCatchSeesNewWorkFromInnerCatch(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                try {
                    try { throw null; }
                    catch (System.Exception) { _ = default(EmptyJob).Schedule(); throw; }
                } catch { ent.Set(default(TestComponent)); }
            }
            public static void CatchInsideFinallyPreservesOriginalException(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                try {
                    try { throw null; }
                    finally {
                        try { throw null; }
                        catch { _ = default(EmptyJob).Schedule(); }
                    }
                } catch { ent.Set(default(TestComponent)); }
            }
            public static void CompletionCannotCoverAnEarlierSiblingCatch(ref SystemContext context, in Ent ent, System.Exception error) {
                try { throw error; }
                catch (ArgumentException) { ent.Set(default(TestComponent)); }
                catch (System.Exception) { context.dependsOn.Complete(); }
            }
        }

        [TestCase(nameof(ILCatchFixtures.ReturnSurvivesCaughtInnerCleanup), "proven")]
        [TestCase(nameof(ILCatchFixtures.CatchDoesNotReenterItsSibling), "proven")]
        [TestCase(nameof(ILCatchFixtures.OuterCatchSeesNewWorkFromInnerCatch), "unproven")]
        [TestCase(nameof(ILCatchFixtures.CatchInsideFinallyPreservesOriginalException), "unproven")]
        [TestCase(nameof(ILCatchFixtures.CompletionCannotCoverAnEarlierSiblingCatch), "unproven")]
        public void ILSynchronizationCatchSeparatesHandlerEntryAndActiveUnwind(string method, string status) {
            var result = ReadILSynchronization(typeof(ILCatchFixtures).GetMethod(method));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.Greater(result.accesses, 0);
        }

        [TestCase(false, true, true, "proven", false)]
        [TestCase(true, true, true, "unproven", true)]
        [TestCase(true, true, false, "proven", true)]
        [TestCase(true, false, false, "unproven", true)]
        public void ILSynchronizationCatchReceivesFaultEffectsBeforeHandlerEntry(bool fail, bool completeBefore, bool scheduleInFault, string status, bool hasAccess) {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ILSyncCatchFault_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("Fixture").DefineType("CatchFaultFixture", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var method = type.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(void), new[] { typeof(SystemContext).MakeByRefType() });
            var il = method.GetILGenerator();
            if (completeBefore) {
                var handle = il.DeclareLocal(typeof(JobHandle));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, typeof(SystemContext).GetProperty(nameof(SystemContext.dependsOn)).GetMethod);
                il.Emit(OpCodes.Stloc, handle);
                il.Emit(OpCodes.Ldloca, handle);
                il.Emit(OpCodes.Call, typeof(JobHandle).GetMethod(nameof(JobHandle.Complete), Type.EmptyTypes));
            }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            il.BeginExceptionBlock();
            il.BeginExceptionBlock();
            if (fail) { il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Throw); }
            else il.Emit(OpCodes.Nop);
            il.BeginFaultBlock();
            if (scheduleInFault) {
                il.Emit(OpCodes.Call, typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(FinallySchedule), flags));
                il.Emit(OpCodes.Pop);
            } else il.Emit(OpCodes.Nop);
            il.EndExceptionBlock();
            il.BeginCatchBlock(typeof(object));
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Call, typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(FinallyWrite), flags));
            il.EndExceptionBlock();
            il.Emit(OpCodes.Ret);
            var result = ReadILSynchronization(type.CreateType().GetMethod("Run"));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(hasAccess, result.accesses != 0);
        }
    }
}
