using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures. Never dereference these pointers or invoke native code.
        public static unsafe void NativeMemoryOperations(void* destination, void* source, ref int data) {
            UnsafeUtility.MemCpy(destination, source, 4L);
            UnsafeUtility.MemCpyReplicate(destination, source, 4, 1);
            UnsafeUtility.MemCpyStride(destination, 4, source, 4, 4, 1);
            UnsafeUtility.MemMove(destination, source, 4L);
            UnsafeUtility.MemSwap(destination, source, 4L);
            UnsafeUtility.MemSet(destination, 1, 4L);
            UnsafeUtility.MemClear(destination, 4L);
            _ = UnsafeUtility.MemCmp(destination, source, 4L);
            _ = UnsafeUtility.SizeOf<int>();
            _ = UnsafeUtility.AlignOf<int>();
            _ = UnsafeUtility.AddressOf(ref data);
            _ = UnsafeUtility.As<int, uint>(ref data);
            _ = UnsafeUtility.AsRef<int>(source);
            _ = UnsafeUtility.ArrayElementAsRef<int>(source, 0);
            _ = UnsafeUtility.ReadArrayElement<int>(source, 0);
            _ = UnsafeUtility.ReadArrayElementWithStride<int>(source, 0, 4);
            UnsafeUtility.WriteArrayElement(destination, 0, data);
            UnsafeUtility.WriteArrayElementWithStride(destination, 0, 4, data);
            UnsafeUtility.CopyPtrToStructure(source, out data);
            UnsafeUtility.CopyStructureToPtr(ref data, destination);
        }
        public static unsafe void* NativeDestination(in Ent ent) { ent.Set(new TestComponent()); return null; }
        public static unsafe void* NativeSource(in Ent ent) { ent.Set(new Test1Component()); return null; }
        public static long NativeByteCount(in Ent ent) { ent.Set(new Test2Component()); Ent.New(); return 4L; }
        public static unsafe void NativeMemoryArguments(in Ent ent) =>
            UnsafeUtility.MemCpy(NativeDestination(in ent), NativeSource(in ent), NativeByteCount(in ent));
        public partial struct NativeMemoryArgumentJob : IJob {
            public Ent ent;
            public void Execute() => NativeMemoryArguments(in this.ent);
        }
        public static unsafe T NativeRead<T>(void* ptr) => UnsafeUtility.ReadArrayElement<T>(ptr, 0);
        public static unsafe void NativeWrite<T>(void* ptr, T value) => UnsafeUtility.WriteArrayElement(ptr, 0, value);
        public static unsafe void NativeCopy<T>(void* source, void* destination) where T : unmanaged {
            UnsafeUtility.CopyPtrToStructure(source, out T value);
            UnsafeUtility.CopyStructureToPtr(ref value, destination);
        }
        public partial struct NativeTypedReadJob : IJob {
            public unsafe void Execute() => _ = NativeRead<TestComponent>(null);
        }
        public partial struct NativeTypedWriteJob : IJob {
            public unsafe void Execute() => NativeWrite<TestComponent>(null, default);
        }
        public partial struct NativeTypedCopyJob : IJob {
            public unsafe void Execute() => NativeCopy<TestComponent>(null, null);
        }
        public partial struct NativeScalarMemoryJob : IJob {
            public unsafe void Execute() { var value = 0; NativeMemoryOperations(null, null, ref value); }
        }
        public partial struct NativeAddressComponentsJob : IJob {
            public unsafe void Execute() {
                var value = default(TestComponent);
                _ = UnsafeUtility.As<TestComponent, Test1Component>(ref value);
            }
        }
        public static unsafe void NativeMemoryExcluded(System.Type type, Unity.Collections.Allocator allocator) {
            _ = UnsafeUtility.SizeOf(type);
            _ = UnsafeUtility.GetFieldOffset(null);
            _ = UnsafeUtility.Malloc(4L, 4, allocator);
            UnsafeUtility.Free(null, allocator);
        }
        public static class UserNativeMemory {
            public static unsafe void MemCpy(void* destination, void* source, long size) => default(Ent).Set(new Test3Component());
        }
        public static unsafe void NativeMemoryLookalike() => UserNativeMemory.MemCpy(null, null, 4L);
        public partial struct NativeMemoryLookalikeJob : IJob {
            public void Execute() => NativeMemoryLookalike();
        }
        public partial struct NativeMemoryBeforeCompleteSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) { UnsafeUtility.MemClear(null, 4L); context.dependsOn.Complete(); }
        }
        public partial struct NativeMemoryAfterCompleteSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); UnsafeUtility.MemClear(null, 4L); }
        }
        public static long NativeScheduleArgument() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return 4L; }
        public static long NativeCompleteArgument() {
            var handle = IJobExtensions.Schedule(default(ControlFirstJob), default);
            handle.Complete(); return 4L;
        }
        public partial struct NativeMemoryLateArgumentSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                UnsafeUtility.MemCpy(NativeDestination(default), null, NativeScheduleArgument());
            }
        }
        public partial struct NativeMemoryCompletedArgumentSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                UnsafeUtility.MemCpy(NativeDestination(default), null, NativeCompleteArgument());
            }
        }
        public partial struct NativeMemoryReadHandleSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); NativeRead<JobHandle>(null).Complete();
            }
        }
        public partial struct NativeMemoryCopyHandleSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); UnsafeUtility.CopyPtrToStructure(null, out JobHandle handle); handle.Complete();
            }
        }
        public partial struct NativeMemoryLayoutSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = UnsafeUtility.SizeOf<JobHandle>(); _ = UnsafeUtility.AlignOf<TestComponent>(); }
        }
        public partial struct NativeTypedReadSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = NativeRead<TestComponent>(null); }
        }

        [Test]
        public void NativeMemoryContractsCoverOnlyAuditedSignaturesAndKeepArguments() {
            var operations = ExternalValueOperations(nameof(NativeMemoryOperations));
            Assert.AreEqual(20, operations.Length);
            Assert.AreEqual(14, operations.Count(row => row.Contains("!native-memory-access")));
            foreach (var operation in operations) CollectionAssert.Contains(operation, "!ecs-leaf");
            var arguments = ExternalValueOperations(nameof(NativeMemoryArguments));
            Assert.AreEqual(4, arguments.Length);
            Assert.AreEqual(1, arguments.Count(row => row.Contains("!ecs-leaf")));
            foreach (var method in new[] { nameof(NativeMemoryExcluded), nameof(NativeMemoryLookalike) })
                Assert.IsFalse(ExternalValueOperations(method).Any(row => row.Contains("!ecs-leaf")), method);
        }

        [TestCase(typeof(NativeTypedReadJob), typeof(TestComponent), 0)]
        [TestCase(typeof(NativeTypedWriteJob), typeof(TestComponent), 2)]
        [TestCase(typeof(NativeTypedCopyJob), typeof(TestComponent), 2)]
        [TestCase(typeof(NativeMemoryLookalikeJob), typeof(Test3Component), 2)]
        public void NativeMemoryTypedEffectsUseSourceWithoutExecutingMemory(Type job, Type component, int mode) {
            var reader = CreateSafetyReader(() => Assert.Fail("Known native body and typed accesses must use source."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(component, mode) }, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [Test]
        public void NativeMemoryArgumentsRetainSafetyCountsAndWeights() {
            var reader = CreateSafetyReader(() => Assert.Fail("Native argument effects must use source."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(Test1Component), 2), SafetyExceptionDependency(typeof(Test2Component), 2),
                SafetyExceptionDependency(typeof(TestComponent), 2) }.OrderBy(row => row, StringComparer.Ordinal).ToArray(),
                SafetySelectionRecords(SelectJobSafety(reader, typeof(NativeMemoryArgumentJob))));
            var counts = ControlSummary(typeof(NativeMemoryArgumentJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsTrue(counts.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) && row.EndsWith("\t1\t0", StringComparison.Ordinal)));
            var weights = ControlSummary(typeof(NativeMemoryArgumentJob), "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            Assert.AreEqual("16", weights[3]);
            CollectionAssert.Contains(weights, "W\tME.BECS.Ent.NewEnt_INTERNAL\t10");
            CollectionAssert.Contains(weights, "W\tME.BECS.EntExt.Set\t6");
        }

        [Test]
        public void NativeMemoryScalarValuesAreNotComponentDependenciesButBothReinterpretedComponentsAre() {
            var scalar = ControlSummary(typeof(NativeScalarMemoryJob), "ME.BECS.JobSafety.v1");
            Assert.AreEqual("0", scalar[2], string.Join("\n", scalar));
            Assert.IsFalse(scalar.Any(row => row.StartsWith("D\t", StringComparison.Ordinal)));
            var reader = CreateSafetyReader(() => Assert.Fail("Reinterpreted component types must be retained."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(Test1Component), 2), SafetyExceptionDependency(typeof(TestComponent), 2) },
                SafetySelectionRecords(SelectJobSafety(reader, typeof(NativeAddressComponentsJob))));
            CollectionAssert.Contains(SystemDependencyRows(typeof(NativeTypedReadSystem)), SystemComponent(0, typeof(TestComponent)));
        }

        [TestCase(typeof(NativeMemoryBeforeCompleteSystem), "unproven")]
        [TestCase(typeof(NativeMemoryAfterCompleteSystem), "proven")]
        [TestCase(typeof(NativeMemoryLateArgumentSystem), "unproven")]
        [TestCase(typeof(NativeMemoryCompletedArgumentSystem), "proven")]
        [TestCase(typeof(NativeMemoryReadHandleSystem), "incomplete")]
        [TestCase(typeof(NativeMemoryCopyHandleSystem), "incomplete")]
        [TestCase(typeof(NativeMemoryLayoutSystem), "proven")]
        [TestCase(typeof(NativeTypedReadSystem), "proven")]
        public void NativeMemoryAccessOccursAfterArgumentsAndDoesNotInventHandleOwnership(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.AreEqual(expected == "incomplete", rows[2] != "0", string.Join("\n", rows));
            if (expected != "incomplete") Assert.IsNotNull(DependencySelector(system, () => Assert.Fail("Known native memory flow must not read IL."), out _));
        }
    }
}
