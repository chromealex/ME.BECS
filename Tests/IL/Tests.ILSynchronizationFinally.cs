using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public partial struct ILBorrowedSchedulingJob : IComponent, IJob { public void Execute() { } }
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
        [TestCase(typeof(FinallyGenericSystem<AotMarker>), "proven")]
        public void ILSynchronizationFinallyPreservesNormalAndExceptionalContinuations(Type system, string status) {
            var root = system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
            var result = ReadILSynchronization(root);
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.Greater(result.accesses, 0);
            Assert.AreEqual(status == "unproven", result.unsafeSites.Length != 0);
        }

        private static partial class ILFinallyFixtures {
            private partial struct EmptyJob : IJob { public void Execute() { } }
            private static void CompleteAndThrow(ref JobHandle handle) {
                try { throw null; } finally { handle.Complete(); }
            }
            public static void CrossFrameUnwind(ref SystemContext context, in Ent ent) {
                var handle = context.dependsOn;
                try { CompleteAndThrow(ref handle); } finally { ent.Set(default(TestComponent)); }
            }
            public static void CrossFrameAfterCompletion(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                var handle = context.dependsOn;
                try { CompleteAndThrow(ref handle); } finally { ent.Set(default(TestComponent)); }
            }
            public static void ReturnOrContinue(ref SystemContext context, in Ent ent, bool stop) {
                var handle = context.dependsOn;
                try {
                    if (stop) return;
                    handle = default(EmptyJob).Schedule(handle);
                } finally { handle.Complete(); }
                ent.Set(default(TestComponent));
            }
            private sealed class ThrowingConstructor {
                public ThrowingConstructor() { _ = default(EmptyJob).Schedule(); throw null; }
            }
            public static void ConstructorUnwind(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                try { _ = new ThrowingConstructor(); } finally { ent.Set(default(TestComponent)); }
            }
            private static void ForwardJob<T>(ref T job) where T : struct, IJob {
                try { } finally { _ = IJobExtensions.ScheduleByRef(ref job); }
            }
            public static void BorrowedJobAfterScheduling(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                ref var job = ref ent.Get<ILBorrowedSchedulingJob>();
                _ = default(EmptyJob).Schedule();
                ForwardJob(ref job);
            }
            public static void BorrowedJobAfterCompletion(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                ref var job = ref ent.Get<ILBorrowedSchedulingJob>();
                ForwardJob(ref job);
            }
        }

        [TestCase(nameof(ILFinallyFixtures.CrossFrameUnwind), "unproven")]
        [TestCase(nameof(ILFinallyFixtures.CrossFrameAfterCompletion), "proven")]
        [TestCase(nameof(ILFinallyFixtures.ReturnOrContinue), "proven")]
        [TestCase(nameof(ILFinallyFixtures.ConstructorUnwind), "unproven")]
        [TestCase(nameof(ILFinallyFixtures.BorrowedJobAfterScheduling), "unproven")]
        [TestCase(nameof(ILFinallyFixtures.BorrowedJobAfterCompletion), "proven")]
        public void ILSynchronizationFinallyKeepsCalleeFailuresAndNormalReturnsDistinct(string method, string status) {
            var result = ReadILSynchronization(typeof(ILFinallyFixtures).GetMethod(method));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.Greater(result.accesses, 0);
        }

        [Test]
        public void ILSynchronizationLocalJobReferenceInFinallyIsNotAComponentAccess() {
            var system = typeof(SchedulingFinallySystem);
            var result = ReadILSynchronization(system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single());
            Assert.AreEqual("proven", result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(0, result.accesses,
                "This ref job points to a caller local, not an arbitrary component address or the deferred Execute body.");
        }

        [TestCase(false, false, false, "proven", false)]
        [TestCase(true, false, false, "unproven", true)]
        [TestCase(true, true, false, "proven", true)]
        [TestCase(false, false, true, "unproven", true)]
        public void ILSynchronizationFaultExecutesOnlyOnExceptionalExit(bool fail, bool completeBefore, bool completeInFault, string status, bool hasAccess) {
            // C# has no fault syntax. Build a tiny CLI fixture without invoking it;
            // fault must not complete incoming work on a successful normal leave.
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ILSyncFault_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("Fixture").DefineType("FaultFixture", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var method = type.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(void), new[] { typeof(SystemContext).MakeByRefType() });
            var il = method.GetILGenerator();
            var handle = il.DeclareLocal(typeof(JobHandle));
            void Complete() {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, typeof(SystemContext).GetProperty(nameof(SystemContext.dependsOn)).GetMethod);
                il.Emit(OpCodes.Stloc, handle);
                il.Emit(OpCodes.Ldloca, handle);
                il.Emit(OpCodes.Call, typeof(JobHandle).GetMethod(nameof(JobHandle.Complete), Type.EmptyTypes));
            }
            var write = typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(FinallyWrite), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            Assert.IsNotNull(write);
            if (completeBefore) Complete();
            il.BeginExceptionBlock();
            if (fail) { il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Throw); }
            else il.Emit(OpCodes.Nop);
            il.BeginFaultBlock();
            if (completeInFault) Complete();
            else il.Emit(OpCodes.Call, write);
            il.EndExceptionBlock();
            if (completeInFault) il.Emit(OpCodes.Call, write);
            il.Emit(OpCodes.Ret);
            var result = ReadILSynchronization(type.CreateType().GetMethod("Run"));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(hasAccess, result.accesses != 0);
        }
    }
}
