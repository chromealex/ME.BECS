using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using Unity.Burst;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public struct ILSharedPayload { public int number; }
        public struct ILSharedNestedComponent : IComponent { public ILSharedPayload payload; }

        // Metadata-only fixtures. Never invoke these methods or allocate shared storage.
        private static partial class ILSharedFixtures {
            private partial struct Job : IJob { public void Execute() { } }
            private static void Schedule() => _ = default(Job).Schedule();
            private static ref TestComponent Borrow() => ref SharedStatic<TestComponent>.GetOrCreate<BurstStorageContext>().Data;
            private static TestComponent Copy(ref TestComponent value) => value;
            private static Type Identity(Type value) => value;
            private static Type ScheduleAndType() { Schedule(); return typeof(BurstStorageContext); }
            private struct HandleHolder { public JobHandle handle; }

            public static void BorrowBeforeCompletion(ref SystemContext context) { _ = Copy(ref Borrow()); context.dependsOn.Complete(); }
            public static void BorrowAfterCompletion(ref SystemContext context) { context.dependsOn.Complete(); _ = Copy(ref Borrow()); }
            public static void BorrowThenScheduleThenRead(ref SystemContext context) {
                context.dependsOn.Complete();
                ref var value = ref Borrow();
                Schedule();
                _ = Copy(ref value);
            }
            public static void BorrowThenScheduleThenWrite(ref SystemContext context) {
                context.dependsOn.Complete();
                ref var value = ref Borrow();
                Schedule();
                value = default;
            }
            public static int BorrowFieldThenScheduleThenRead(ref SystemContext context) {
                context.dependsOn.Complete();
                ref var value = ref Borrow().data;
                Schedule();
                return value;
            }
            public static void BorrowFieldThenScheduleThenWrite(ref SystemContext context) {
                context.dependsOn.Complete();
                ref var value = ref Borrow().data;
                Schedule();
                value = 1;
            }
            public static int BorrowNestedFieldThenSchedule(ref SystemContext context) {
                context.dependsOn.Complete();
                ref var payload = ref SharedStatic<ILSharedNestedComponent>.GetOrCreate<BurstStorageContext>().Data.payload;
                Schedule();
                return payload.number;
            }
            public static int CopyBeforeScheduleDoesNotBorrow(ref SystemContext context) {
                context.dependsOn.Complete();
                var payload = SharedStatic<ILSharedNestedComponent>.GetOrCreate<BurstStorageContext>().Data.payload;
                Schedule();
                return payload.number;
            }
            public static int MixedPayloadReferenceRetainsPossibleComponent(ref SystemContext context, bool branch) {
                context.dependsOn.Complete();
                var local = default(ILSharedPayload);
                ref var borrowed = ref SharedStatic<ILSharedNestedComponent>.GetOrCreate<BurstStorageContext>().Data.payload;
                ref var value = ref (branch ? ref borrowed : ref local);
                Schedule();
                return value.number;
            }
            public static TestComponent UnknownReferenceDoesNotBecomeShared(ref SystemContext context, bool branch, ref TestComponent unknown) {
                context.dependsOn.Complete();
                ref var value = ref (branch ? ref Borrow() : ref unknown);
                return value;
            }
            public static TestComponent SharedReferenceJoinRetainsBorrow(ref SystemContext context, bool branch) {
                context.dependsOn.Complete();
                ref var value = ref (branch ? ref Borrow() : ref SharedStatic<TestComponent>.GetOrCreate<BurstStorageContext, TestComponent>().Data);
                Schedule();
                return value;
            }
            public static void BorrowAndCompleteNewWork(ref SystemContext context) {
                context.dependsOn.Complete();
                ref var value = ref Borrow();
                default(Job).Schedule().Complete();
                _ = Copy(ref value);
            }
            public static void LiteralIdentityAcrossHelper(ref SystemContext context) {
                var type = Identity(typeof(BurstStorageContext));
                _ = SharedStatic<int>.GetOrCreate((Type)(object)type);
                _ = BurstRuntime.GetHashCode32(type);
                _ = BurstRuntime.GetHashCode64(type);
            }
            public static void LiteralIdentityAcrossJoin(ref SystemContext context, bool branch) {
                var type = branch ? typeof(BurstStorageContext) : typeof(TestComponent);
                _ = SharedStatic<int>.GetOrCreate(type);
            }
            public static void UnknownIdentityAcrossJoin(ref SystemContext context, bool branch, Type unknown) {
                var type = branch ? typeof(BurstStorageContext) : unknown;
                _ = SharedStatic<int>.GetOrCreate(type);
            }
            public static void OneUnknownSubcontext(ref SystemContext context, Type unknown) =>
                _ = SharedStatic<int>.GetOrCreate(typeof(BurstStorageContext), unknown);
            public static void OneUnknownContext(ref SystemContext context, Type unknown) =>
                _ = SharedStatic<int>.GetOrCreate(unknown, typeof(BurstStorageContext));
            public static void TypeFactoryEffectsPrecedeData(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = SharedStatic<TestComponent>.GetOrCreate(ScheduleAndType()).Data;
            }
            public static void NestedSharedHandleCannotCompleteInput(ref SystemContext context) {
                SharedStatic<HandleHolder>.GetOrCreate<BurstStorageContext>().Data.handle.Complete();
                default(Ent).Set(new TestComponent());
            }
            public static unsafe void PointerOnly(ref SystemContext context) =>
                _ = SharedStatic<TestComponent>.GetOrCreate<BurstStorageContext>().UnsafeDataPointer;
            public static unsafe TestComponent RawPointerRead(ref SystemContext context) =>
                *(TestComponent*)SharedStatic<TestComponent>.GetOrCreate<BurstStorageContext>().UnsafeDataPointer;
            public static int UnknownScalarStorage(ref SystemContext context) => SharedStatic<int>.GetOrCreate<BurstStorageContext>().Data;
            private static class Lookalike {
                public static long GetHashCode64<T>() { Schedule(); return 1; }
            }
            public static void UserHashIsNotAnIdentityLeaf(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = Lookalike.GetHashCode64<BurstStorageContext>();
                default(Ent).Set(new TestComponent());
            }
        }

        [TestCase(typeof(BurstStorageOnlySystem), "proven", false)]
        [TestCase(typeof(BurstStorageBeforeCompleteSystem), "unproven", true)]
        [TestCase(typeof(BurstStorageAfterCompleteSystem), "proven", true)]
        [TestCase(typeof(BurstStorageLateArgumentSystem), "unproven", true)]
        [TestCase(typeof(BurstStorageReflectedLateArgumentSystem), "unproven", true)]
        public void ILSharedStorageRetainsArgumentOrderAndComponentAccess(Type system, string status, bool access) {
            var result = ReadILSynchronization(system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single());
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(access, result.accesses > 0);
        }

        [TestCase(nameof(ILSharedFixtures.BorrowBeforeCompletion), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.BorrowAfterCompletion), "proven", true)]
        [TestCase(nameof(ILSharedFixtures.BorrowThenScheduleThenRead), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.BorrowThenScheduleThenWrite), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.BorrowFieldThenScheduleThenRead), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.BorrowFieldThenScheduleThenWrite), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.BorrowNestedFieldThenSchedule), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.CopyBeforeScheduleDoesNotBorrow), "proven", true)]
        [TestCase(nameof(ILSharedFixtures.MixedPayloadReferenceRetainsPossibleComponent), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.SharedReferenceJoinRetainsBorrow), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.BorrowAndCompleteNewWork), "proven", true)]
        [TestCase(nameof(ILSharedFixtures.LiteralIdentityAcrossHelper), "proven", false)]
        [TestCase(nameof(ILSharedFixtures.LiteralIdentityAcrossJoin), "proven", false)]
        [TestCase(nameof(ILSharedFixtures.PointerOnly), "proven", false)]
        [TestCase(nameof(ILSharedFixtures.TypeFactoryEffectsPrecedeData), "unproven", true)]
        [TestCase(nameof(ILSharedFixtures.UserHashIsNotAnIdentityLeaf), "unproven", true)]
        public void ILSharedStorageCarriesBorrowedProvenanceUntilActualUse(string method, string status, bool access) {
            var result = ReadILSynchronization(typeof(ILSharedFixtures).GetMethod(method, BindingFlags.Public | BindingFlags.Static));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(access, result.accesses > 0);
            Assert.AreEqual(status == "unproven", result.unsafeSites.Length != 0);
        }

        [TestCase(nameof(ILSharedFixtures.UnknownIdentityAcrossJoin), "UnknownBurstTypeIdentity")]
        [TestCase(nameof(ILSharedFixtures.OneUnknownSubcontext), "UnknownBurstTypeIdentity")]
        [TestCase(nameof(ILSharedFixtures.OneUnknownContext), "UnknownBurstTypeIdentity")]
        [TestCase(nameof(ILSharedFixtures.NestedSharedHandleCannotCompleteInput), "OpaqueHandleStorage")]
        [TestCase(nameof(ILSharedFixtures.RawPointerRead), "OpaqueStorageRead")]
        [TestCase(nameof(ILSharedFixtures.UnknownScalarStorage), "OpaqueStorageRead")]
        [TestCase(nameof(ILSharedFixtures.UnknownReferenceDoesNotBecomeShared), "OpaqueStorageRead")]
        public void ILSharedStorageDoesNotInventIdentityOrHandleCoverage(string method, string gap) {
            var result = ReadILSynchronization(typeof(ILSharedFixtures).GetMethod(method, BindingFlags.Public | BindingFlags.Static));
            Assert.AreEqual("incomplete", result.status);
            Assert.IsTrue(result.gaps.Any(value => value.StartsWith(gap + ":", StringComparison.Ordinal)), string.Join("\n", result.gaps));
        }

        [TestCase(typeof(BurstStorageUnknownHandleSystem), "OpaqueHandleStorage")]
        [TestCase(typeof(BurstStorageUnknownTypeSystem), "UnknownBurstTypeIdentity")]
        public void ILSharedStorageRefusesUnknownInputCoverage(Type system, string gap) {
            var result = ReadILSynchronization(system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single());
            Assert.AreEqual("incomplete", result.status);
            Assert.IsTrue(result.gaps.Any(value => value.StartsWith(gap + ":", StringComparison.Ordinal)), string.Join("\n", result.gaps));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ILScalarIndirectAccessCannotReuseUnconvertedConstants(bool indirectLoad) {
            // stind.i1 truncates 128 to -128 in sbyte storage. Treating the
            // original ldc.i4 constant as the later value would choose Complete
            // on the wrong branch and incorrectly certify the component write.
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ILSyncScalar_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("Fixture").DefineType("ScalarFixture", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var method = type.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(void), new[] { typeof(SystemContext).MakeByRefType() });
            var il = method.GetILGenerator();
            var scalar = il.DeclareLocal(typeof(sbyte));
            var handle = il.DeclareLocal(typeof(JobHandle));
            var writeLabel = il.DefineLabel();
            il.Emit(OpCodes.Ldloca, scalar);
            il.Emit(OpCodes.Ldc_I4, 128);
            il.Emit(OpCodes.Stind_I1);
            il.Emit(indirectLoad ? OpCodes.Ldloca : OpCodes.Ldloc, scalar);
            if (indirectLoad) il.Emit(OpCodes.Ldind_I1);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Clt);
            il.Emit(OpCodes.Brtrue, writeLabel);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(SystemContext).GetProperty(nameof(SystemContext.dependsOn)).GetMethod);
            il.Emit(OpCodes.Stloc, handle);
            il.Emit(OpCodes.Ldloca, handle);
            il.Emit(OpCodes.Call, typeof(JobHandle).GetMethod(nameof(JobHandle.Complete), Type.EmptyTypes));
            il.MarkLabel(writeLabel);
            il.Emit(OpCodes.Call, typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(FinallyWrite), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
            il.Emit(OpCodes.Ret);
            var result = ReadILSynchronization(type.CreateType().GetMethod("Run"));
            Assert.AreEqual("unproven", result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
        }
    }
}
