using System;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata only: no pointer is dereferenced and no job/native call runs.
        private static unsafe partial class ILNativeSyncFixtures {
            private partial struct Job : IJob { public void Execute() { } }
            private static void Write() => default(Ent).Set(default(TestComponent));
            private static int ScheduleIndex() { _ = default(Job).Schedule(); return 0; }
            private static T Read<T>(void* source) => UnsafeUtility.ReadArrayElement<T>(source, 0);
            public static void Layout(ref SystemContext context) { _ = UnsafeUtility.SizeOf<JobHandle>(); _ = UnsafeUtility.AlignOf<TestComponent>(); }
            public static void AddressOnly(ref SystemContext context) { var value = default(TestComponent); _ = UnsafeUtility.AddressOf(ref value); }
            public static void ReadBefore(ref SystemContext context, void* source) { _ = Read<TestComponent>(source); context.dependsOn.Complete(); }
            public static void ReadAfter(ref SystemContext context, void* source) { context.dependsOn.Complete(); _ = Read<TestComponent>(source); }
            public static void ReadStride(ref SystemContext context, void* source) => _ = UnsafeUtility.ReadArrayElementWithStride<TestComponent>(source, 1, 16);
            public static void ScalarReadBefore(ref SystemContext context, void* source) => _ = Read<int>(source);
            public static void ScalarReadAfter(ref SystemContext context, void* source) { context.dependsOn.Complete(); _ = Read<int>(source); }
            public static void ReadLocalHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                Read<JobHandle>(UnsafeUtility.AddressOf(ref handle)).Complete();
                Write();
            }
            public static void ReadLocalHandleStride(ref SystemContext context) {
                var handle = context.dependsOn;
                UnsafeUtility.ReadArrayElementWithStride<JobHandle>(UnsafeUtility.AddressOf(ref handle), 0, 1).Complete();
                Write();
            }
            public static void CopyLocalHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                UnsafeUtility.CopyPtrToStructure(UnsafeUtility.AddressOf(ref handle), out JobHandle copy);
                copy.Complete(); Write();
            }
            public static void WriteDefaultHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                UnsafeUtility.WriteArrayElement(UnsafeUtility.AddressOf(ref handle), 0, default(JobHandle));
                handle.Complete(); Write();
            }
            public static void WriteInputHandle(ref SystemContext context) {
                var handle = default(JobHandle);
                UnsafeUtility.WriteArrayElement(UnsafeUtility.AddressOf(ref handle), 0, context.dependsOn);
                handle.Complete(); Write();
            }
            public static void WriteStrideDefaultHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                UnsafeUtility.WriteArrayElementWithStride(UnsafeUtility.AddressOf(ref handle), 0, 1, default(JobHandle));
                handle.Complete(); Write();
            }
            public static void CopyPointerOverwritesOldHandle(ref SystemContext context) {
                var handle = context.dependsOn; var empty = default(JobHandle);
                UnsafeUtility.CopyPtrToStructure(UnsafeUtility.AddressOf(ref empty), out handle);
                handle.Complete(); Write();
            }
            public static void CopyStructureOverwritesOldHandle(ref SystemContext context) {
                var handle = context.dependsOn; var empty = default(JobHandle);
                UnsafeUtility.CopyStructureToPtr(ref empty, UnsafeUtility.AddressOf(ref handle));
                handle.Complete(); Write();
            }
            public static void AsRefOverwritesOldHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                UnsafeUtility.AsRef<JobHandle>(UnsafeUtility.AddressOf(ref handle)) = default;
                handle.Complete(); Write();
            }
            public static void SameTypeCastRetainsHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                UnsafeUtility.As<JobHandle, JobHandle>(ref handle).Complete(); Write();
            }
            public static void ElementReferenceRetainsHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                UnsafeUtility.ArrayElementAsRef<JobHandle>(UnsafeUtility.AddressOf(ref handle), 0).Complete(); Write();
            }
            public static void NonzeroElementDoesNotRetainHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                UnsafeUtility.ArrayElementAsRef<JobHandle>(UnsafeUtility.AddressOf(ref handle), 1).Complete(); Write();
            }
            public static void UnknownIndexDoesNotRetainHandle(ref SystemContext context, int index) {
                var handle = context.dependsOn;
                UnsafeUtility.ReadArrayElement<JobHandle>(UnsafeUtility.AddressOf(ref handle), index).Complete(); Write();
            }
            public static void ReinterpretedContextDoesNotBecomeHandle(ref SystemContext context) {
                UnsafeUtility.As<SystemContext, JobHandle>(ref context).Complete(); Write();
            }
            public static void RawReinterpretDoesNotBecomeHandle(ref SystemContext context) {
                var handle = *(JobHandle*)UnsafeUtility.AddressOf(ref context);
                handle.Complete(); Write();
            }
            public static void PartialOverwriteDoesNotKeepHandle(ref SystemContext context) {
                var handle = context.dependsOn;
                *(long*)UnsafeUtility.AddressOf(ref handle) = 0;
                handle.Complete(); Write();
            }
            public static void PointerMutationOfContext(ref SystemContext context) {
                UnsafeUtility.WriteArrayElement(UnsafeUtility.AddressOf(ref context), 0, default(SystemContext));
                context.dependsOn.Complete(); Write();
            }
            public static void UnknownHandleCopy(ref SystemContext context, void* source) {
                var handle = context.dependsOn;
                UnsafeUtility.CopyPtrToStructure(source, out handle);
                handle.Complete(); Write();
            }
            public static void UnknownWriteMayAliasHandle(ref SystemContext context, void* target) {
                var handle = context.dependsOn;
                UnsafeUtility.WriteArrayElement(target, 0, 0);
                handle.Complete(); Write();
            }
            public static void UnknownCopyDestination(ref SystemContext context, void* target) {
                var component = default(TestComponent);
                context.dependsOn.Complete();
                UnsafeUtility.CopyStructureToPtr(ref component, target);
            }
            public static void SharedComponentAfterLateArgument(ref SystemContext context) {
                context.dependsOn.Complete();
                ref var value = ref SharedStatic<TestComponent>.GetOrCreate<BurstStorageContext>().Data;
                _ = UnsafeUtility.ReadArrayElement<TestComponent>(UnsafeUtility.AddressOf(ref value), ScheduleIndex());
            }
            public static void SharedComponentWriteAfterNewWork(ref SystemContext context) {
                context.dependsOn.Complete();
                ref var value = ref SharedStatic<TestComponent>.GetOrCreate<BurstStorageContext>().Data;
                var pointer = UnsafeUtility.AddressOf(ref value);
                _ = default(Job).Schedule();
                UnsafeUtility.WriteArrayElement(pointer, 0, default(TestComponent));
            }
            public static void DirectExternalComponentCopy(ref SystemContext context, ref TestComponent value) {
                var copy = value;
                context.dependsOn.Complete();
            }
            private static class UserMemory {
                public static T ReadArrayElement<T>(void* source, int index) { _ = default(Job).Schedule(); return default; }
            }
            public static void LookalikeKeepsUserScheduling(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = UserMemory.ReadArrayElement<int>(null, 0);
                Write();
            }
        }

        [TestCase(nameof(ILNativeSyncFixtures.Layout), "proven", false)]
        [TestCase(nameof(ILNativeSyncFixtures.AddressOnly), "proven", false)]
        [TestCase(nameof(ILNativeSyncFixtures.ReadBefore), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.ReadAfter), "proven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.ReadStride), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.ScalarReadBefore), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.ScalarReadAfter), "proven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.ReadLocalHandle), "proven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.ReadLocalHandleStride), "proven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.CopyLocalHandle), "proven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.WriteDefaultHandle), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.WriteInputHandle), "proven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.WriteStrideDefaultHandle), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.CopyPointerOverwritesOldHandle), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.CopyStructureOverwritesOldHandle), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.AsRefOverwritesOldHandle), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.SameTypeCastRetainsHandle), "proven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.ElementReferenceRetainsHandle), "proven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.PointerMutationOfContext), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.SharedComponentAfterLateArgument), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.SharedComponentWriteAfterNewWork), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.DirectExternalComponentCopy), "unproven", true)]
        [TestCase(nameof(ILNativeSyncFixtures.LookalikeKeepsUserScheduling), "unproven", true)]
        public void ILNativeMemoryTracksExactReadsWritesAndAliases(string method, string status, bool access) {
            var result = ReadILSynchronization(typeof(ILNativeSyncFixtures).GetMethod(method));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(access, result.accesses > 0);
        }

        [TestCase(nameof(ILNativeSyncFixtures.NonzeroElementDoesNotRetainHandle), "OpaqueHandleStorage")]
        [TestCase(nameof(ILNativeSyncFixtures.UnknownIndexDoesNotRetainHandle), "OpaqueHandleStorage")]
        [TestCase(nameof(ILNativeSyncFixtures.ReinterpretedContextDoesNotBecomeHandle), "OpaqueHandleStorage")]
        [TestCase(nameof(ILNativeSyncFixtures.RawReinterpretDoesNotBecomeHandle), "ReinterpretedStorage")]
        [TestCase(nameof(ILNativeSyncFixtures.PartialOverwriteDoesNotKeepHandle), "ReinterpretedStorage")]
        [TestCase(nameof(ILNativeSyncFixtures.UnknownHandleCopy), "OpaqueHandleStorage")]
        [TestCase(nameof(ILNativeSyncFixtures.UnknownWriteMayAliasHandle), "OpaqueNativeWrite")]
        [TestCase(nameof(ILNativeSyncFixtures.UnknownCopyDestination), "OpaqueNativeWrite")]
        public void ILNativeMemoryDoesNotCertifyUnknownOrReinterpretedStorage(string method, string gap) {
            var result = ReadILSynchronization(typeof(ILNativeSyncFixtures).GetMethod(method));
            Assert.AreEqual("incomplete", result.status);
            Assert.IsTrue(result.gaps.Any(value => value.StartsWith(gap + ":", StringComparison.Ordinal)), string.Join("\n", result.gaps));
        }

        [TestCase(typeof(NativeMemoryLayoutSystem), "proven")]
        [TestCase(typeof(NativeTypedReadSystem), "proven")]
        [TestCase(typeof(NativeMemoryReadHandleSystem), "incomplete")]
        [TestCase(typeof(NativeMemoryCopyHandleSystem), "incomplete")]
        public void ILNativeMemoryMatchesCompiledLifecycleContracts(Type system, string status) {
            var result = ReadILSynchronization(system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single());
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
        }
    }
}
