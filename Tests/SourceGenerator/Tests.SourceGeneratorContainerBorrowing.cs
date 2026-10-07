using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only. Never allocate/iterate a container or execute a job.
        public static int BorrowMapRead<TKey, TValue>(NativeHashMap<TKey, TValue> map)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged {
            var count = 0;
            foreach (var pair in map) { _ = pair.Key; pair.GetKeyValue(out _, out _); ++count; }
            return count;
        }
        public static void BorrowMapRef(ref NativeHashMap<bool, int> map) => _ = BorrowMapRead(map);
        public static void BorrowMapIn(in NativeHashMap<bool, int> map) => _ = BorrowMapRead(map);
        public static void BorrowListIn(in NativeList<int> list) { _ = list.Length; _ = list[0]; }
        public static void BorrowUnsafeMap(in UnsafeHashMap<bool, int> map) {
            foreach (var pair in map) { _ = pair.Key; _ = pair.Value; }
        }
        public static void BorrowNativeCaller() {
            var map = new NativeHashMap<bool, int>(1, Allocator.Temp);
            map.TryAdd(true, 1); BorrowMapRef(ref map); BorrowMapIn(in map); map.Dispose();
            var list = new NativeList<int>(1, Allocator.Temp); list.Add(1); BorrowListIn(in list); list.Dispose();
            var unsafeMap = new UnsafeHashMap<bool, int>(1, Allocator.Temp); unsafeMap.TryAdd(true, 1); BorrowUnsafeMap(in unsafeMap); unsafeMap.Dispose();
        }
        public static void BorrowMapEffects(NativeHashMap<bool, int> map) {
            foreach (var pair in map) { _ = pair.Key; Ent.New(); }
            Ent.New(); default(Ent).Set(new TestComponent());
        }
        public static void BorrowMapSchedule(NativeHashMap<bool, int> map) {
            _ = BorrowMapRead(map); _ = IJobExtensions.Schedule(default(ControlFirstJob), default);
        }
        public static void BorrowMapGrow(ref NativeHashMap<bool, int> map) { map.TryAdd(true, 1); }
        public static void BorrowGenericGrow<TKey, TValue>(ref NativeHashMap<TKey, TValue> map, TKey key, TValue value)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged { map.TryAdd(key, value); }
        public static void BorrowGrowForward<TKey, TValue>(int unused, ref NativeHashMap<TKey, TValue> map, TKey key, TValue value)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged => BorrowGenericGrow(ref map, key, value);
        public static void BorrowListGrow<T>(ref NativeList<T> list, T value) where T : unmanaged { list.Add(value); list.Capacity = 16; }
        public static void BorrowUnsafeGrow<T>(ref UnsafeHashMap<bool, T> map, T value) where T : unmanaged { map.TryAdd(true, value); map.TrimExcess(); }
        public static void BorrowPairEffects(NativeHashMap<bool, int> first, NativeHashMap<bool, int> second) {
            _ = first.Count; _ = second.Count; Ent.New();
        }
        public static void BorrowGrowTyped() {
            var map = new NativeHashMap<NativeMapComponentKey, TestComponent>(1, Allocator.Temp);
            BorrowGrowForward(0, ref map, default(NativeMapComponentKey), default(TestComponent)); map.Dispose();
            var list = new NativeList<TestComponent>(1, Allocator.Temp); BorrowListGrow(ref list, default(TestComponent)); list.Dispose();
            var unsafeMap = new UnsafeHashMap<bool, TestComponent>(1, Allocator.Temp);
            BorrowUnsafeGrow(ref unsafeMap, default(TestComponent)); unsafeMap.Dispose();
        }
        public partial struct BorrowGrowTypedJob : IJob { public void Execute() => BorrowGrowTyped(); }
        public partial struct BorrowMixedAllocatorJob : IJob {
            public NativeHashMap<bool, int> external;
            public void Execute() {
                var known = new NativeHashMap<bool, int>(1, Allocator.Temp);
                BorrowMapGrow(ref known); BorrowMapGrow(ref this.external); BorrowMapGrow(ref known); known.Dispose();
            }
        }
        public partial struct BorrowMixedReverseJob : IJob {
            public NativeHashMap<bool, int> external;
            public void Execute() {
                BorrowMapGrow(ref this.external);
                var known = new NativeHashMap<bool, int>(1, Allocator.Temp); BorrowMapGrow(ref known); known.Dispose();
            }
        }
        [EntitiesJobMaxCount(64)]
        public partial struct BorrowGrowCountsJob : IJob {
            public void Execute() {
                var map = new NativeHashMap<NativeMapCreatingKey, int>(1, Allocator.Temp);
                BorrowGrowForward(0, ref map, default(NativeMapCreatingKey), 0);
                BorrowGrowForward(0, ref map, default(NativeMapCreatingKey), 1); map.Dispose();
            }
        }
        public partial struct BorrowContextWeightsJob : IJob {
            public void Execute() {
                var map = new NativeHashMap<bool, int>(1, Allocator.Temp);
                BorrowPairEffects(map, default); BorrowPairEffects(default, map); BorrowPairEffects(map, map); map.Dispose();
            }
        }
        public partial struct BorrowGrowBeforeSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { BorrowGrowTyped(); context.dependsOn.Complete(); }
        }
        public partial struct BorrowGrowAfterSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); BorrowGrowTyped(); }
        }
        public partial struct BorrowGrowCallbackSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); var map = new NativeHashMap<NativeMapSchedulingHashKey, int>(1, Allocator.Temp);
                BorrowGrowForward(0, ref map, default(NativeMapSchedulingHashKey), 0); map.Dispose();
            }
        }
        public static void BorrowMapRecursive(NativeHashMap<bool, int> map, bool recurse) {
            _ = map.Count; if (recurse) BorrowMapRecursive(map, false);
        }
        public partial struct BorrowCoreJob : IJob { public void Execute() => BorrowNativeCaller(); }
        [EntitiesJobMaxCount(16)]
        public partial struct BorrowEffectsJob : IJob {
            public void Execute() { var map = new NativeHashMap<bool, int>(1, Allocator.Temp); BorrowMapEffects(map); map.Dispose(); }
        }
        public partial struct BorrowTypedJob : IJob {
            public void Execute() {
                var map = new NativeHashMap<NativeMapComponentKey, TestComponent>(1, Allocator.Temp);
                map.TryAdd(default, default); _ = BorrowMapRead(map); map.Dispose();
            }
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct BorrowGrowGenericJob : IJob {
                public void Execute() {
                    var map = new NativeHashMap<bool, T>(1, Allocator.Temp);
                    BorrowGrowForward(0, ref map, true, default(T)); map.Dispose();
                }
            }
            public partial struct BorrowJob : IJob {
                public void Execute() { var map = new NativeHashMap<bool, T>(1, Allocator.Temp); _ = BorrowMapRead(map); map.Dispose(); }
            }
        }
        public partial struct BorrowUnknownAllocatorJob : IJob {
            public void Execute() { var map = new NativeHashMap<bool, int>(1, (Allocator)64); BorrowMapRef(ref map); map.Dispose(); }
        }
        public partial struct BorrowGrowJob : IJob {
            // Proven incoming allocator is bound to this call, not to every use of the helper.
            public void Execute() { var map = new NativeHashMap<bool, int>(1, Allocator.Temp); BorrowMapGrow(ref map); map.Dispose(); }
        }
        public partial struct MapEnumerateEmptyJob : IJob {
            public void Execute() { foreach (var pair in default(NativeHashMap<NativeMapEffectsKey, TestComponent>)) { } }
        }
        public partial struct MapEnumerateReadJob : IJob {
            public void Execute() => _ = BorrowMapRead(default(NativeHashMap<NativeMapComponentKey, TestComponent>));
        }
        public partial struct MapEnumerateValueJob : IJob {
            public void Execute() { foreach (var pair in default(NativeHashMap<NativeMapEffectsKey, TestComponent>)) _ = pair.Value; }
        }
        public partial struct MapEnumerateReadOnlyJob : IJob {
            public void Execute() {
                foreach (var pair in default(NativeHashMap<NativeMapComponentKey, TestComponent>.ReadOnly)) pair.GetKeyValue(out _, out _);
                foreach (var pair in default(UnsafeHashMap<NativeMapComponentKey, TestComponent>.ReadOnly)) pair.GetKeyValue(out _, out _);
            }
        }
        public static NativeHashMap<bool, int> borrowEscaped;
        public static KVPair<bool, int> borrowPairEscaped;
        public static Action borrowClosure;
        public static Action<NativeHashMap<bool, int>> borrowUnknown;
        public static void BorrowEscape(NativeHashMap<bool, int> map) => borrowEscaped = map;
        public static NativeHashMap<bool, int> BorrowReturn(NativeHashMap<bool, int> map) => map;
        public static void BorrowCopy(NativeHashMap<bool, int> map) { var copy = map; _ = copy.Count; }
        public static void BorrowReplace(ref NativeHashMap<bool, int> map) => map = new NativeHashMap<bool, int>(1, (Allocator)64);
        public static void BorrowUnknown(NativeHashMap<bool, int> map) => borrowUnknown(map);
        public static void BorrowCapture(NativeHashMap<bool, int> map) => borrowClosure = () => _ = map.Count;
        public static System.Collections.Generic.IEnumerable<int> BorrowIterator(NativeHashMap<bool, int> map) { yield return map.Count; }
        public static void BorrowPairEscape(NativeHashMap<bool, int> map) { foreach (var pair in map) borrowPairEscaped = pair; }
        public static void BorrowPairCopy(NativeHashMap<bool, int> map) { foreach (var pair in map) { var copy = pair; _ = copy.Key; } }
        public static void BorrowPairCapture(NativeHashMap<bool, int> map) { foreach (var pair in map) borrowClosure = () => _ = pair.Key; }
        public static void BorrowLocalEscape() { var map = new NativeHashMap<bool, int>(1, Allocator.Temp); BorrowEscape(map); map.TryAdd(true, 1); map.Dispose(); }
        public static void BorrowLocalAlias() { var map = new NativeHashMap<bool, int>(1, Allocator.Temp); BorrowReplace(ref map); map.TryAdd(true, 1); map.Dispose(); }
        public static void BorrowLocalEnumeration() {
            var map = new NativeHashMap<bool, int>(1, Allocator.Temp);
            foreach (var pair in map) { _ = pair.Key; _ = pair.Value; } map.Dispose();
        }
        public partial struct BorrowBeforeSystem : IUpdate { public void OnUpdate(ref SystemContext context) { BorrowNativeCaller(); context.dependsOn.Complete(); } }
        public partial struct BorrowAfterSystem : IUpdate { public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); BorrowNativeCaller(); } }
        public partial struct BorrowScheduleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); var map = new NativeHashMap<bool, int>(1, Allocator.Temp); BorrowMapSchedule(map); map.Dispose();
            }
        }
        public partial struct BorrowHandleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); foreach (var pair in default(NativeHashMap<bool, JobHandle>)) pair.Value.Complete();
            }
        }

        private static string[] BorrowMethodRows(string method) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2").Select(attribute => attribute.Value.Split('\n'))
            .Where(rows => !rows[0].Contains("~nested:"))
            .Single(rows => rows[0].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + method + "(", StringComparison.Ordinal) ||
                rows[0].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + method + "``", StringComparison.Ordinal));
        }
        [TestCase(nameof(BorrowMapRead))]
        [TestCase(nameof(BorrowMapRef))]
        [TestCase(nameof(BorrowMapIn))]
        [TestCase(nameof(BorrowListIn))]
        [TestCase(nameof(BorrowUnsafeMap))]
        [TestCase(nameof(BorrowMapEffects))]
        [TestCase(nameof(BorrowMapGrow))]
        [TestCase(nameof(BorrowMapRecursive))]
        public void ContainerBorrowProofIsSeparateFromEffects(string method) {
            CollectionAssert.Contains(BorrowMethodRows(method)[1].Split(','), "native-container-borrow=v1:0");
        }
        [TestCase(nameof(BorrowEscape))]
        [TestCase(nameof(BorrowReturn))]
        [TestCase(nameof(BorrowCopy))]
        [TestCase(nameof(BorrowReplace))]
        [TestCase(nameof(BorrowUnknown))]
        [TestCase(nameof(BorrowCapture))]
        [TestCase(nameof(BorrowIterator))]
        [TestCase(nameof(BorrowPairEscape))]
        [TestCase(nameof(BorrowPairCopy))]
        [TestCase(nameof(BorrowPairCapture))]
        public void ContainerBorrowProofRejectsEveryHeaderEscape(string method) {
            Assert.IsFalse(BorrowMethodRows(method)[1].Split(',').Any(flag => flag.StartsWith("native-container-borrow", StringComparison.Ordinal)));
        }
        [Test]
        public void BorrowingHelpersPreserveOnlyProvenLocalAllocators() {
            foreach (var method in new[] { nameof(BorrowNativeCaller), nameof(BorrowLocalEnumeration) })
                Assert.IsTrue(ExternalValueOperations(method).Any(row => row.Contains("!native-map-local-allocator=builtin-v1")), method);
            foreach (var method in new[] { nameof(BorrowLocalEscape), nameof(BorrowLocalAlias) })
                Assert.IsFalse(ExternalValueOperations(method).Any(row => row.Contains("!native-map-local-allocator=builtin-v1")), method);
            Assert.IsFalse(ExternalValueOperations(nameof(BorrowNativeCaller)).Where(row => row[3].Contains(".BorrowMap") || row[3].Contains(".BorrowUnsafeMap"))
                .Any(row => row.Contains("!ecs-leaf")), "The helper's complete effects must still be visited.");
        }
        [TestCase(typeof(BorrowCoreJob))]
        [TestCase(typeof(BorrowGrowJob))]
        [TestCase(typeof(MapEnumerateEmptyJob))]
        public void ContainerBorrowingAndEnumerationDoNotInventCallbacks(Type job) {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + kind + ".v1");
                Assert.AreEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("D\t", StringComparison.Ordinal) || row.StartsWith("C\t", StringComparison.Ordinal)));
                if (kind == "JobWeights") Assert.AreEqual("0", rows[3]);
            }
        }
        [TestCase(typeof(BorrowTypedJob), 2, 2)]
        [TestCase(typeof(MapEnumerateReadJob), 0, 0)]
        [TestCase(typeof(MapEnumerateReadOnlyJob), 0, 0)]
        [TestCase(typeof(MapEnumerateValueJob), -1, 2)]
        public void EnumerationKeepsExactKeyValueComponentAccess(Type job, int keyMode, int valueMode) {
            var expected = new[] { SafetyExceptionDependency(typeof(TestComponent), valueMode) }
                .Concat(keyMode < 0 ? Array.Empty<string>() : new[] { SafetyExceptionDependency(typeof(NativeMapComponentKey), keyMode) }).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            var reader = CreateSafetyReader(() => Assert.Fail("Enumeration must not read IL."));
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }
        [Test]
        public void BorrowingKeepsLoopCountsWeightsAndClosedGenericTypes() {
            var counts = ControlSummary(typeof(BorrowEffectsJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsTrue(counts.Single(row => row.StartsWith("C\t", StringComparison.Ordinal)).EndsWith("\t1\t1", StringComparison.Ordinal));
            var weights = ControlSummary(typeof(BorrowEffectsJob), "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights)); Assert.AreEqual("12", weights[3]);
            var reader = CreateSafetyReader(() => Assert.Fail("Borrowing must retain the helper's effects."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(TestComponent), 2) }, SafetySelectionRecords(SelectJobSafety(reader, typeof(BorrowEffectsJob))));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(AotMarker), 2) }, SafetySelectionRecords(SelectJobSafety(reader, typeof(GenericAotSystem<AotMarker>.BorrowJob))));
        }
        [TestCase(typeof(BorrowUnknownAllocatorJob))]
        [TestCase(typeof(BorrowMixedAllocatorJob))]
        [TestCase(typeof(BorrowMixedReverseJob))]
        public void BorrowingDoesNotCertifyCalleeAllocation(Type job) {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + kind + ".v1");
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }
        [TestCase(typeof(BorrowGrowTypedJob), true)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.BorrowGrowGenericJob), false)]
        public void BorrowedAllocatorBindingPreservesGenericTypedEffects(Type job, bool typedKey) {
            var reader = CreateSafetyReader(() => Assert.Fail("Bound helper allocators must not use IL."));
            var expected = typedKey ? new[] { typeof(NativeMapComponentKey), typeof(TestComponent) } : new[] { typeof(AotMarker) };
            CollectionAssert.AreEqual(expected.Select(type => SafetyExceptionDependency(type, 2)).OrderBy(row => row, StringComparer.Ordinal).ToArray(),
                SafetySelectionRecords(SelectJobSafety(reader, job)));
            foreach (var kind in new[] { "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + kind + ".v1");
                Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            }
        }
        [Test]
        public void BorrowedAllocatorBindingKeepsCallCountsButUniqueBodyWeight() {
            var callbacks = ControlSummary(typeof(BorrowGrowCountsJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", callbacks[2], string.Join("\n", callbacks));
            Assert.IsTrue(callbacks.Single(row => row.StartsWith("C\t", StringComparison.Ordinal)).EndsWith("\t4\t8", StringComparison.Ordinal));
            var counts = ControlSummary(typeof(BorrowContextWeightsJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsTrue(counts.Single(row => row.StartsWith("C\t", StringComparison.Ordinal)).EndsWith("\t3\t0", StringComparison.Ordinal));
            var weights = ControlSummary(typeof(BorrowContextWeightsJob), "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights)); Assert.AreEqual("10", weights[3]);
        }
        [TestCase(typeof(BorrowGrowBeforeSystem), "unproven")]
        [TestCase(typeof(BorrowGrowAfterSystem), "proven")]
        [TestCase(typeof(BorrowGrowCallbackSystem), "unproven")]
        public void BorrowedAllocatorBindingRetainsOrderedMemoryAndCallbacks(Type system, string status) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + status);
        }
        [TestCase(typeof(BorrowBeforeSystem), "unproven")]
        [TestCase(typeof(BorrowAfterSystem), "proven")]
        [TestCase(typeof(BorrowScheduleSystem), "unproven")]
        [TestCase(typeof(BorrowHandleSystem), "incomplete")]
        public void BorrowingAndEnumerationRetainPendingWork(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.AreEqual(expected == "incomplete", rows[2] != "0", string.Join("\n", rows));
        }
    }
}
