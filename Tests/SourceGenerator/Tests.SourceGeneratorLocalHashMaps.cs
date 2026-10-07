using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures. No native allocation, key callback or job is executed.
        public static void LocalMapOperations() {
            var native = new NativeHashMap<int, int>(1, Allocator.Temp);
            native.Add(0, 1); _ = native.TryAdd(1, 2); native[2] = 3;
            native.Capacity = 16; native.TrimExcess();
            _ = native.ContainsKey(0); _ = native.TryGetValue(0, out _); _ = native[0]; _ = native.Remove(0); native.Clear(); native.Dispose();
            var map = new UnsafeHashMap<int, int>(1, BuiltinAllocationHandle);
            map.Add(0, 1); _ = map.TryAdd(1, 2); map[2] = 3;
            map.Capacity = 16; map.TrimExcess(); _ = map.ContainsKey(0); map.Dispose();
        }
        public static void LocalMapUsing<TKey, TValue>() where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged {
            using (var map = new NativeHashMap<TKey, TValue>(1, Allocator.Temp)) { map.TryAdd(default, default); map.TrimExcess(); }
        }
        public static void LocalMapScalarOperations() {
            var map = new NativeHashMap<int, int>(1, Allocator.Temp);
            map.TryAdd(0, 1); map[0] = 2; map.Capacity = 16; map.TrimExcess();
            _ = map.TryGetValue(0, out _); _ = map.ContainsKey(0); _ = map.Remove(0); map.Dispose();
        }
        public static void LocalMapAdd() {
            var map = new UnsafeHashMap<NativeMapEffectsKey, Test3Component>(1, Allocator.Temp);
            map.Add(default, default); map.Dispose();
        }
        public static void LocalMapSet() {
            var map = new NativeHashMap<NativeMapEffectsKey, Test3Component>(1, Allocator.Temp);
            map[default] = default; map.TrimExcess(); map.Dispose();
        }
        public static void LocalMapCount(int kind) {
            var map = new NativeHashMap<NativeMapCreatingKey, int>(1, Allocator.Temp);
            // Separate methods below retain the operation-specific reservation counts.
            map.TryAdd(default, kind); map.Dispose();
        }
        public partial struct LocalMapCoreJob : IJob { public void Execute() => LocalMapScalarOperations(); }
        public partial struct LocalMapAddJob : IJob { public void Execute() => LocalMapAdd(); }
        public partial struct LocalMapSetJob : IJob { public void Execute() => LocalMapSet(); }
        public partial struct LocalMapTypedJob : IJob { public void Execute() => LocalMapUsing<NativeMapComponentKey, TestComponent>(); }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct LocalMapJob : IJob { public void Execute() => LocalMapUsing<NativeMapGenericKeys<T>.Key<Test1Component>, T>(); }
        }
        [EntitiesJobMaxCount(32)]
        public partial struct LocalMapTryCountJob : IJob { public void Execute() => LocalMapCount(0); }
        [EntitiesJobMaxCount(64)]
        public partial struct LocalMapRepeatedCountJob : IJob { public void Execute() { LocalMapCount(0); LocalMapCount(1); } }
        [EntitiesJobMaxCount(32)]
        public partial struct LocalMapAddCountJob : IJob {
            public void Execute() { var map = new NativeHashMap<NativeMapCreatingKey, int>(1, Allocator.Temp); map.Add(default, 0); map.Dispose(); }
        }
        [EntitiesJobMaxCount(32)]
        public partial struct LocalMapSetCountJob : IJob {
            public void Execute() { var map = new UnsafeHashMap<NativeMapCreatingKey, int>(1, Allocator.Temp); map[default] = 0; map.Dispose(); }
        }
        [EntitiesJobMaxCount(32)]
        public partial struct LocalMapResizeCountJob : IJob {
            public void Execute() { var map = new NativeHashMap<NativeMapCreatingKey, int>(1, Allocator.Temp); map.Capacity = 16; map.Dispose(); }
        }
        [EntitiesJobMaxCount(32)]
        public partial struct LocalMapTrimCountJob : IJob {
            public void Execute() { var map = new UnsafeHashMap<NativeMapCreatingKey, int>(1, Allocator.Temp); map.TrimExcess(); map.Dispose(); }
        }
        public partial struct LocalMapDisposeJob : IJob {
            public void Execute() { var map = new NativeHashMap<NativeMapCreatingKey, int>(1, Allocator.Temp); map.Dispose(); }
        }
        public static NativeHashMap<int, int> LocalMapEscaped;
        public static void LocalMapCopy() { var map = new NativeHashMap<int, int>(1, Allocator.Temp); var copy = map; map.TryAdd(0, 0); copy.Dispose(); }
        public static void LocalMapEscape() { var map = new NativeHashMap<int, int>(1, Allocator.Temp); LocalMapEscaped = map; map.TryAdd(0, 0); }
        public static int LocalMapMutate(ref NativeHashMap<int, int> map) { map = new NativeHashMap<int, int>(1, (Allocator)64); return 0; }
        public static void LocalMapArgumentEscape() { var map = new NativeHashMap<int, int>(1, Allocator.Temp); map[0] = LocalMapMutate(ref map); }
        public static void LocalMapUnknownAllocator(AllocatorManager.AllocatorHandle allocator) { var map = new NativeHashMap<int, int>(1, allocator); map.TryAdd(0, 0); }
        public static void LocalMapDefault() { var map = default(NativeHashMap<int, int>); map.TryAdd(0, 0); }
        public static void LocalMapDeferredDispose() { var map = new NativeHashMap<int, int>(1, Allocator.Temp); map.TryAdd(0, 0); map.Dispose(default(JobHandle)); }
        public partial struct LocalMapUnknownJob : IJob { public void Execute() => LocalMapArgumentEscape(); }
        public partial struct LocalMapUnknownHashJob : IJob { public void Execute() => LocalMapUsing<NativeMapHidingHashKey, int>(); }
        // Primitive Add has a checked formatting branch too. Its BCL formatter
        // still needs coverage; a proven map allocator must not hide that gap.
        public partial struct LocalMapScalarFormattingJob : IJob { public void Execute() => LocalMapOperations(); }
        public partial struct LocalMapUnknownFormatJob : IJob {
            public void Execute() { var map = new NativeHashMap<NativeMapOpaqueFormatKey, int>(1, Allocator.Temp); map.Add(default, 0); map.Dispose(); }
        }
        public partial struct LocalMapBeforeSystem : IUpdate { public void OnUpdate(ref SystemContext context) { LocalMapScalarOperations(); context.dependsOn.Complete(); } }
        public partial struct LocalMapAfterSystem : IUpdate { public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); LocalMapScalarOperations(); } }
        public partial struct LocalMapHashSystem : IUpdate { public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); LocalMapUsing<NativeMapSchedulingHashKey, int>(); } }
        public partial struct LocalMapEqualsSystem : IUpdate { public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); LocalMapUsing<NativeMapSchedulingEqualsKey, int>(); } }
        public partial struct LocalMapCompletedSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); var map = new NativeHashMap<NativeMapCompletedKey, int>(1, Allocator.Temp);
                map.TryAdd(default, 0); map.TrimExcess(); map.Dispose();
            }
        }
        public partial struct LocalMapCompletedUsingSystem : IUpdate { public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); LocalMapUsing<NativeMapCompletedKey, int>(); } }
        public partial struct LocalMapFormatSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); var map = new NativeHashMap<NativeMapSchedulingFormatKey, int>(1, Allocator.Temp);
                try { map.Add(default, 0); } catch (System.Exception) { }
                finally { map.Dispose(); }
                _ = default(Ent).Read<TestComponent>();
            }
        }
        public partial struct LocalMapLateArgumentSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); var map = new NativeHashMap<int, int>(1, Allocator.Temp);
                map[0] = NativeContainerScheduleArgument(); map.Dispose();
            }
        }
        public partial struct LocalMapHandleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); LocalMapUsing<int, JobHandle>(); }
        }

        [Test]
        public void LocalMapAllocationProofDoesNotEraseKeyCallbacks() {
            var rows = ExternalValueOperations(nameof(LocalMapOperations));
            var calls = rows.Where(row => row.Contains("!native-map-local-allocator=builtin-v1")).ToArray();
            Assert.AreEqual(12, calls.Length, string.Join("\n", rows.Select(row => string.Join("\t", row))));
            Assert.IsFalse(calls.Any(row => row.Contains("!ecs-leaf")));
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var summary = ControlSummary(typeof(LocalMapCoreJob), "ME.BECS." + kind + ".v1");
                Assert.AreEqual("0", summary[2], string.Join("\n", summary));
                Assert.IsFalse(summary.Any(row => row.StartsWith("D\t", StringComparison.Ordinal) || row.StartsWith("C\t", StringComparison.Ordinal)));
            }
        }
        [TestCase(typeof(LocalMapAddJob), true)]
        [TestCase(typeof(LocalMapSetJob), false)]
        public void LocalMapSourceSafetyIncludesCallbacksAndTypedMemory(Type job, bool formatting) {
            var reader = CreateSafetyReader(() => Assert.Fail("Proven local maps must not read IL."));
            var expected = new[] { SafetyExceptionDependency(typeof(TestComponent), 2), SafetyExceptionDependency(typeof(Test1Component), 0), SafetyExceptionDependency(typeof(Test3Component), 2) }
                .Concat(formatting ? new[] { SafetyExceptionDependency(typeof(Test2Component), 2) } : Array.Empty<string>()).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }
        [TestCase(typeof(LocalMapTypedJob))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.LocalMapJob))]
        public void LocalMapGenericKeyAndValueTypesSurviveUsingCleanup(Type job) {
            var reader = CreateSafetyReader(() => Assert.Fail("Closed local map operations must use source."));
            var types = job == typeof(LocalMapTypedJob) ? new[] { typeof(NativeMapComponentKey), typeof(TestComponent) } : new[] { typeof(AotMarker), typeof(Test1Component) };
            CollectionAssert.AreEqual(types.Select(type => SafetyExceptionDependency(type, 2)).OrderBy(row => row, StringComparer.Ordinal).ToArray(), SafetySelectionRecords(SelectJobSafety(reader, job)));
        }
        [TestCase(typeof(LocalMapTryCountJob), "2", "4")]
        [TestCase(typeof(LocalMapRepeatedCountJob), "4", "8")]
        [TestCase(typeof(LocalMapAddCountJob), "3", "4")]
        [TestCase(typeof(LocalMapSetCountJob), "3", "5")]
        [TestCase(typeof(LocalMapResizeCountJob), "0", "3")]
        [TestCase(typeof(LocalMapTrimCountJob), "0", "3")]
        public void LocalMapRehashCreationCountsRetainEachPhaseAndLoop(Type job, string inline, string loop) {
            var rows = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var count = rows.Select(row => row.Split('\t')).Single(row => row[0] == "C");
            Assert.AreEqual(inline, count[3]); Assert.AreEqual(loop, count[4]);
            Assert.IsTrue(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            var weights = ControlSummary(job, "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            Assert.AreEqual("10", weights[3], "Weights count the shared Ent.New body once, not dynamic callback executions.");
        }
        [Test]
        public void LocalMapDisposalDoesNotInvokeKeyCallbacks() {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(typeof(LocalMapDisposeJob), "ME.BECS." + kind + ".v1");
                Assert.AreEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("C\t", StringComparison.Ordinal)));
                if (kind == "JobWeights") Assert.AreEqual("0", rows[3]);
            }
        }
        [TestCase(nameof(LocalMapCopy))]
        [TestCase(nameof(LocalMapEscape))]
        [TestCase(nameof(LocalMapArgumentEscape))]
        [TestCase(nameof(LocalMapUnknownAllocator))]
        [TestCase(nameof(LocalMapDefault))]
        [TestCase(nameof(LocalMapDeferredDispose))]
        public void LocalMapEscapesCannotCertifyBuiltinAllocator(string method) {
            Assert.IsFalse(ExternalValueOperations(method).Any(row => row.Contains("!native-map-local-allocator=builtin-v1")));
        }
        [TestCase(typeof(LocalMapUnknownJob))]
        [TestCase(typeof(LocalMapUnknownHashJob))]
        [TestCase(typeof(LocalMapScalarFormattingJob))]
        [TestCase(typeof(LocalMapUnknownFormatJob))]
        public void LocalMapUnknownCallbacksAndAllocatorsCannotEmitInitializers(Type job) {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + kind + ".v1");
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }
        [TestCase(typeof(LocalMapBeforeSystem), "unproven")]
        [TestCase(typeof(LocalMapAfterSystem), "proven")]
        [TestCase(typeof(LocalMapHashSystem), "unproven")]
        [TestCase(typeof(LocalMapEqualsSystem), "unproven")]
        [TestCase(typeof(LocalMapCompletedSystem), "proven")]
        // Schedule can throw after submitting work but before its Complete call.
        // Dispose in using/finally must retain that exceptional pending-work path.
        [TestCase(typeof(LocalMapCompletedUsingSystem), "unproven")]
        [TestCase(typeof(LocalMapFormatSystem), "unproven")]
        [TestCase(typeof(LocalMapLateArgumentSystem), "unproven")]
        [TestCase(typeof(LocalMapHandleSystem), "incomplete")]
        public void LocalMapSynchronizationRetainsRehashArgumentsAndThrowingFormatter(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.IsFalse(rows.Any(row => row.Contains("MalformedSynchronizationFlow")), string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.AreEqual(expected == "incomplete", rows[2] != "0", string.Join("\n", rows));
        }
        [TestCase(typeof(LocalMapHashSystem), false)]
        [TestCase(typeof(LocalMapEqualsSystem), false)]
        [TestCase(typeof(LocalMapFormatSystem), false)]
        [TestCase(typeof(LocalMapCompletedSystem), true)]
        public void LocalMapScheduledJobsAreDiscoveredInsideCallbacks(Type system, bool both) {
            var rows = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            Assert.AreEqual(both ? 2 : 1, rows.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
        }
    }
}
