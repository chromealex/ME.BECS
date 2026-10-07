using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only. These methods never allocate, mutate or free native memory in tests.
        public static unsafe void LocalListOperations() {
            var native = new NativeList<int>(1, Allocator.Temp);
            native.Add(1); native.AddReplicate(1, 2); native.AddRange(null, 0); native.AddRange(default(NativeArray<int>));
            native.Resize(2, NativeArrayOptions.ClearMemory); native.ResizeUninitialized(3);
            native.SetCapacity(4); native.TrimExcess(); native.Length = 2; native.Capacity = 4; native.Dispose();
            var list = new UnsafeList<int>(1, BuiltinAllocationHandle);
            list.Add(1); list.AddReplicate(1, 2); list.AddRange(null, 0); list.AddRange(default(UnsafeList<int>));
            list.Resize(2); list.SetCapacity(4); list.TrimExcess(); list.Length = 2; list.Capacity = 4; list.Dispose();
        }
        public static void LocalListBranches(bool choose) {
            NativeList<int> list;
            if (choose) list = new NativeList<int>(1, Allocator.Temp);
            else list = new NativeList<int>(2, Allocator.TempJob);
            list.Add(choose ? 1 : 2);
            for (var i = 0; i < 3; ++i) list.Add(i);
            list.Dispose();
        }
        public static void LocalListUsing<T>() where T : unmanaged {
            using (var list = new NativeList<T>(1, Allocator.Temp)) { list.Add(default); list.ResizeUninitialized(4); }
        }
        public static void LocalListFinally() {
            var list = new UnsafeList<int>(1, Allocator.Temp);
            try { list.Add(1); } finally { list.Dispose(); }
        }
        public static int LocalListValue() { default(Ent).Set(new TestComponent()); Ent.New(); return 1; }
        public static void LocalListArguments() {
            var list = new NativeList<int>(1, Allocator.Temp);
            list.Add(LocalListValue());
            for (var i = 0; i < 3; ++i) list.Add(LocalListValue());
            list.Dispose();
        }
        public partial struct LocalListCoreJob : IJob {
            public void Execute() { LocalListOperations(); LocalListBranches(false); LocalListFinally(); }
        }
        public partial struct LocalListComponentJob : IJob {
            public void Execute() => LocalListUsing<Test1Component>();
        }
        [EntitiesJobMaxCount(8)]
        public partial struct LocalListArgumentsJob : IJob {
            public void Execute() => LocalListArguments();
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct LocalListJob : IJob { public void Execute() => LocalListUsing<T>(); }
        }
        public static void LocalListHeaderMutation() {
            var list = new UnsafeList<int>(1, Allocator.Temp); list.Allocator = (Allocator)64; list.Add(1);
        }
        public static int LocalListMutate(ref UnsafeList<int> list) { list.Allocator = (Allocator)64; return 1; }
        public static void LocalListLateArgumentMutation() {
            var list = new UnsafeList<int>(1, Allocator.Temp); list.Add(LocalListMutate(ref list));
        }
        public static void LocalListBranchMutation(bool choose, AllocatorManager.AllocatorHandle handle) {
            var list = new NativeList<int>(1, Allocator.Temp);
            list.Add(0);
            if (choose) list = new NativeList<int>(1, handle);
            list.Add(1);
        }
        public static void LocalListRefAlias() {
            var list = new UnsafeList<int>(1, Allocator.Temp); ref var alias = ref list; alias.Allocator = (Allocator)64; list.Add(1);
        }
        public static void LocalListCopyAlias() {
            var list = new UnsafeList<int>(1, Allocator.Temp); var alias = list; alias.Add(1); list.Add(1);
        }
        public static NativeList<int> localListEscaped;
        public static void LocalListFieldEscape() {
            var list = new NativeList<int>(1, Allocator.Temp); localListEscaped = list; list.Add(1);
        }
        public static NativeList<int> LocalListReturnEscape() {
            var list = new NativeList<int>(1, Allocator.Temp); list.Add(1); return list;
        }
        public static void LocalListCaptured() {
            var list = new UnsafeList<int>(1, Allocator.Temp);
            Action callback = () => list.Allocator = (Allocator)64; callback(); list.Add(1);
        }
        public static void LocalListPointerEscape() {
            var list = new UnsafeList<int>(1, Allocator.Temp);
            unsafe { var pointer = (UnsafeList<int>*)UnsafeUtility.AddressOf(ref list); pointer->Allocator = (Allocator)64; }
            list.Add(1);
        }
        public static void LocalListDeferredDispose() {
            var list = new NativeList<int>(1, Allocator.TempJob); list.Add(1); _ = list.Dispose(default(JobHandle));
        }
        public static unsafe void LocalListBorrowed() { var list = new UnsafeList<int>(null, 0); list.Add(1); }
        public partial struct LocalListUnknownJob : IJob {
            public void Execute() => LocalListLateArgumentMutation();
        }
        public partial struct LocalListBeforeCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { LocalListUsing<TestComponent>(); context.dependsOn.Complete(); }
        }
        public partial struct LocalListAfterCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); LocalListUsing<TestComponent>(); }
        }
        public partial struct LocalListLateArgumentSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); var list = new NativeList<int>(1, Allocator.Temp);
                list.Add(NativeContainerScheduleArgument()); list.Dispose();
            }
        }
        public partial struct LocalListLateSetterSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); var list = new NativeList<int>(1, Allocator.Temp);
                list.Capacity = NativeContainerScheduleArgument(); list.Dispose();
            }
        }
        public partial struct LocalListFinallyCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); LocalListFinally(); }
        }

        [Test]
        public void LocalContainerContractsCoverAuditedOperationsWithoutErasingArguments() {
            var rows = ExternalValueOperations(nameof(LocalListOperations));
            var calls = rows.Where(row => row.Contains("!native-container-local-allocator=builtin-v1")).ToArray();
            Assert.AreEqual(21, calls.Length, string.Join("\n", rows.Select(row => string.Join("\t", row))));
            Assert.IsTrue(calls.All(row => row.Contains("!ecs-leaf") && row.Contains("!native-memory-access")));
            Assert.IsFalse(rows.Where(row => row[3].Contains("BuiltinAllocationHandle")).Any(row => row.Contains("!ecs-leaf")));
        }

        [TestCase(typeof(LocalListCoreJob), null)]
        [TestCase(typeof(LocalListComponentJob), typeof(Test1Component))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.LocalListJob), typeof(AotMarker))]
        [TestCase(typeof(LocalListArgumentsJob), typeof(TestComponent))]
        public void LocalContainerSafetyAndCountsAreSourceOwned(Type job, Type component) {
            var reader = CreateSafetyReader(() => Assert.Fail("Proven local containers must not read legacy IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var safety, out _), string.Join("\n", safety ?? Array.Empty<string>()));
            CollectionAssert.AreEqual(component == null ? Array.Empty<string>() : new[] { SafetyExceptionDependency(component, 2) },
                SafetySelectionRecords(SelectJobSafety(reader, job)));
            var counts = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            var creations = counts.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(job == typeof(LocalListArgumentsJob) ? 1 : 0, creations.Length);
            if (creations.Length != 0) Assert.IsTrue(creations.Single().EndsWith("\t1\t1", StringComparison.Ordinal));
            var weights = ControlSummary(job, "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            Assert.AreEqual(job == typeof(LocalListArgumentsJob) ? "12" : "0", weights[3]);
        }

        [TestCase(nameof(LocalListHeaderMutation))]
        [TestCase(nameof(LocalListLateArgumentMutation))]
        [TestCase(nameof(LocalListBranchMutation))]
        [TestCase(nameof(LocalListRefAlias))]
        [TestCase(nameof(LocalListCopyAlias))]
        [TestCase(nameof(LocalListFieldEscape))]
        [TestCase(nameof(LocalListReturnEscape))]
        [TestCase(nameof(LocalListCaptured))]
        [TestCase(nameof(LocalListPointerEscape))]
        [TestCase(nameof(LocalListDeferredDispose))]
        [TestCase(nameof(LocalListBorrowed))]
        public void LocalContainerEscapesCannotCertifyAllocatorProvenance(string method) {
            var rows = ExternalValueOperations(method);
            Assert.IsFalse(rows.Any(row => row.Contains("!native-container-local-allocator=builtin-v1")),
                string.Join("\n", rows.Select(row => string.Join("\t", row))));
        }

        [Test]
        public void LocalContainerUnprovedAllocatorCannotEmitInitializers() {
            foreach (var kind in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(LocalListUnknownJob), kind);
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }

        [TestCase(typeof(LocalListBeforeCompleteSystem), "unproven")]
        [TestCase(typeof(LocalListAfterCompleteSystem), "proven")]
        [TestCase(typeof(LocalListLateArgumentSystem), "unproven")]
        [TestCase(typeof(LocalListLateSetterSystem), "unproven")]
        [TestCase(typeof(LocalListFinallyCompleteSystem), "proven")]
        public void LocalContainerSynchronizationRetainsGrowingFreeingAndFinallyAccess(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
        }
    }
}
