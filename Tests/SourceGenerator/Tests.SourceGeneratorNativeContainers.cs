using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only: never allocate, dereference, dispose or schedule.
        public static unsafe void NativeContainerCoreOperations(UnsafeList<int> list, NativeList<int> native,
            NativeHashMap<int, int> map, UnsafeHashMap<int, int> unsafeMap) {
            _ = list.Length; _ = list.Capacity; _ = list.IsCreated; _ = list.IsEmpty;
            _ = list[0]; list[0] = 1; _ = list.ElementAt(0);
            list.AddNoResize(1); list.AddRangeNoResize(null, 1); list.AddRangeNoResize(list);
            list.RemoveAt(0); list.RemoveAtSwapBack(0); list.RemoveRange(0, 1); list.RemoveRangeSwapBack(0, 1); list.Clear();
            _ = new UnsafeList<int>(null, 0);
            _ = native.Length; _ = native.Capacity; _ = native.IsCreated; _ = native.IsEmpty;
            _ = native[0]; native[0] = 1; _ = native.ElementAt(0);
            native.AddNoResize(1); native.AddRangeNoResize(null, 1); native.AddRangeNoResize(native);
            native.RemoveAt(0); native.RemoveAtSwapBack(0); native.RemoveRange(0, 1); native.RemoveRangeSwapBack(0, 1); native.Clear();
            _ = map.Count; _ = map.Capacity; _ = map.IsCreated; _ = map.IsEmpty; map.Clear();
            _ = unsafeMap.Count; _ = unsafeMap.Capacity; _ = unsafeMap.IsCreated; _ = unsafeMap.IsEmpty; unsafeMap.Clear();
        }
        public static void NativeContainerBuiltinConstructors() {
            _ = new NativeHashMap<int, int>(1, Allocator.Temp);
            _ = new UnsafeHashMap<int, int>(1, Allocator.Persistent);
            _ = new UnsafeList<int>(1, Allocator.TempJob);
            _ = new NativeList<int>(Allocator.Temp);
            _ = new NativeList<int>(1, Allocator.TempJob);
            _ = new NativeList<int>(1, AllocatorManager.Persistent);
            _ = new NativeHashMap<int, int>(1, AllocatorManager.Temp);
            const Allocator named = Allocator.Persistent;
            _ = new UnsafeList<int>(1, named, NativeArrayOptions.ClearMemory);
            // Construction must not invoke element Equals/GetHashCode callbacks.
            _ = new NativeHashMap<NativeContainerUserKey, int>(1, Allocator.Temp);
        }
        public static void NativeContainerUnknownAllocators(AllocatorManager.AllocatorHandle handle, Allocator variable) {
            _ = new NativeHashMap<int, int>(1, handle);
            _ = new NativeList<int>(1, variable);
            _ = new UnsafeList<int>(1, (Allocator)64);
        }
        public static void NativeContainerCallbacksRemainOpaque(UnsafeList<int> list, NativeList<int> native, NativeHashMap<int, int> map) {
            list.Add(1); list.Length = 1; list.Capacity = 1; list.Dispose();
            native.Add(1); native.Length = 1; native.Capacity = 1; native.Dispose();
            map.Add(1, 1); map.Capacity = 1; _ = map.ContainsKey(1); map.Dispose();
        }
        public static int NativeContainerCapacity(in Ent ent) { ent.Set(new Test1Component()); Ent.New(); return 1; }
        public partial struct NativeContainerArgumentJob : IJob {
            public Ent ent;
            public void Execute() => _ = new NativeHashMap<int, int>(NativeContainerCapacity(in this.ent), Allocator.Temp);
        }
        public partial struct NativeContainerCoreJob : IJob {
            public void Execute() { NativeContainerBuiltinConstructors(); NativeContainerCoreOperations(default, default, default, default); }
        }
        public static T NativeContainerRead<T>(UnsafeList<T> list) where T : unmanaged => list[0];
        public static void NativeContainerWrite<T>(NativeList<T> list, T value) where T : unmanaged => list.AddNoResize(value);
        public static void NativeContainerRemove<T>(UnsafeList<T> list) where T : unmanaged => list.RemoveRangeSwapBack(0, 1);
        public partial struct NativeContainerReadJob : IJob {
            public void Execute() => _ = NativeContainerRead(default(UnsafeList<TestComponent>));
        }
        public partial struct NativeContainerWriteJob : IJob {
            public void Execute() => NativeContainerWrite(default, default(TestComponent));
        }
        public partial struct NativeContainerRemoveJob : IJob {
            public void Execute() => NativeContainerRemove(default(UnsafeList<TestComponent>));
        }
        public partial struct NativeContainerHeaderJob : IJob {
            public void Execute() { _ = default(NativeList<TestComponent>).Length; _ = default(NativeHashMap<int, TestComponent>).Count; }
        }
        public struct NativeContainerUserKey : IEquatable<NativeContainerUserKey> {
            public bool Equals(NativeContainerUserKey other) { default(Ent).Set(new TestComponent()); return true; }
            public override int GetHashCode() { Ent.New(); return 0; }
        }
        public partial struct NativeContainerUnknownKeyJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeContainerOpaqueKey, int>).ContainsKey(default);
        }
        public struct NativeContainerOpaqueKey : IEquatable<NativeContainerOpaqueKey> {
            public static Action callback;
            public bool Equals(NativeContainerOpaqueKey other) { callback(); return true; }
            public override int GetHashCode() => 0;
        }
        public partial struct NativeContainerUnknownAllocatorJob : IJob {
            public AllocatorManager.AllocatorHandle allocator;
            public void Execute() => _ = new NativeList<int>(1, this.allocator);
        }
        public struct NativeContainerLookalike {
            public void AddNoResize(int value) => default(Ent).Set(new Test3Component());
        }
        public partial struct NativeContainerLookalikeJob : IJob {
            public void Execute() => default(NativeContainerLookalike).AddNoResize(1);
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct NativeContainerJob : IJob {
                public void Execute() {
                    _ = new NativeList<T>(1, Allocator.Temp);
                    _ = NativeContainerRead(default(UnsafeList<T>));
                }
            }
        }
        public partial struct NativeContainerBeforeCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = default(UnsafeList<int>)[0]; context.dependsOn.Complete(); }
        }
        public partial struct NativeContainerAfterCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = default(UnsafeList<int>)[0]; }
        }
        public static int NativeContainerScheduleArgument() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return 1; }
        public partial struct NativeContainerLateArgumentSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); default(UnsafeList<int>).AddRangeNoResize(null, NativeContainerScheduleArgument());
            }
        }
        public partial struct NativeContainerReadHandleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); default(UnsafeList<JobHandle>)[0].Complete(); }
        }
        public partial struct NativeContainerConstructSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); NativeContainerBuiltinConstructors(); }
        }
        public partial struct NativeContainerInlineHeaderSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                _ = default(NativeList<int>).IsCreated;
                _ = default(UnsafeList<int>).Length;
                _ = default(UnsafeHashMap<int, int>).Count;
            }
        }

        [Test]
        public void NativeContainerContractsAreExactAndKeepAllocatorProofAtCallSite() {
            var core = ExternalValueOperations(nameof(NativeContainerCoreOperations));
            Assert.AreEqual(41, core.Length);
            foreach (var row in core) { CollectionAssert.Contains(row, "!ecs-leaf"); CollectionAssert.Contains(row, "!native-container=1"); }
            var constructors = ExternalValueOperations(nameof(NativeContainerBuiltinConstructors)).Where(row => row[0] == "new").ToArray();
            Assert.AreEqual(9, constructors.Length);
            foreach (var row in constructors) CollectionAssert.Contains(row, "!native-container-allocator=builtin-v1");
            foreach (var method in new[] { nameof(NativeContainerUnknownAllocators), nameof(NativeContainerCallbacksRemainOpaque) })
                Assert.IsFalse(ExternalValueOperations(method).Any(row => row.Contains("!native-container=1") || row.Contains("!native-container-allocator=builtin-v1")), method);
        }

        [TestCase(typeof(NativeContainerReadJob), typeof(TestComponent), 0)]
        [TestCase(typeof(NativeContainerWriteJob), typeof(TestComponent), 2)]
        [TestCase(typeof(NativeContainerRemoveJob), typeof(TestComponent), 2)]
        [TestCase(typeof(NativeContainerLookalikeJob), typeof(Test3Component), 2)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.NativeContainerJob), typeof(AotMarker), 0)]
        public void NativeContainerTypedEffectsUseSourceAcrossGenericArguments(Type job, Type component, int mode) {
            var reader = CreateSafetyReader(() => Assert.Fail("Known container operation must not read IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(component, mode) }, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [Test]
        public void NativeContainerConstructionPreservesArgumentCountsAndWeights() {
            var reader = CreateSafetyReader(() => Assert.Fail("Constructor capacity effects must remain source-owned."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(Test1Component), 2) },
                SafetySelectionRecords(SelectJobSafety(reader, typeof(NativeContainerArgumentJob))));
            var counts = ControlSummary(typeof(NativeContainerArgumentJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsTrue(counts.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) && row.EndsWith("\t1\t0", StringComparison.Ordinal)));
            var weights = ControlSummary(typeof(NativeContainerArgumentJob), "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            Assert.AreEqual("12", weights[3]);
        }

        [TestCase(typeof(NativeContainerCoreJob))]
        [TestCase(typeof(NativeContainerHeaderJob))]
        public void NativeContainerHeadersDoNotInventElementComponentAccess(Type job) {
            foreach (var catalog in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + catalog + ".v1");
                Assert.AreEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("D\t", StringComparison.Ordinal) || row.StartsWith("C\t", StringComparison.Ordinal)));
            }
        }

        [TestCase(typeof(NativeContainerUnknownKeyJob))]
        [TestCase(typeof(NativeContainerUnknownAllocatorJob))]
        public void NativeContainerCallbacksCannotBecomeEmptyCompleteCatalogs(Type job) {
            foreach (var catalog in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + catalog + ".v1");
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\t", StringComparison.Ordinal)));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }

        [TestCase(typeof(NativeContainerBeforeCompleteSystem), "unproven")]
        [TestCase(typeof(NativeContainerAfterCompleteSystem), "proven")]
        [TestCase(typeof(NativeContainerLateArgumentSystem), "unproven")]
        [TestCase(typeof(NativeContainerReadHandleSystem), "incomplete")]
        [TestCase(typeof(NativeContainerConstructSystem), "proven")]
        [TestCase(typeof(NativeContainerInlineHeaderSystem), "proven")]
        public void NativeContainerSynchronizationRetainsOrderedAccessAndOpaqueHandles(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.AreEqual(expected == "incomplete", rows[2] != "0", string.Join("\n", rows));
        }
    }
}
