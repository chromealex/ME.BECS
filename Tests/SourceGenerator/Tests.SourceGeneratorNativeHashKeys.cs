using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Analyze these fixtures as metadata only. No map, callback or job is executed.
        public partial struct NativeMapScalarJob : IJob {
            public void Execute() {
                var native = default(NativeHashMap<int, int>);
                _ = native.ContainsKey(0); _ = native.TryGetValue(0, out _); _ = native.Remove(0);
                var unsafeMap = default(UnsafeHashMap<int, int>);
                _ = unsafeMap.ContainsKey(0); _ = unsafeMap.TryGetValue(0, out _); _ = unsafeMap.Remove(0);
                var readOnly = default(NativeHashMap<int, int>.ReadOnly);
                _ = readOnly.ContainsKey(0); _ = readOnly.TryGetValue(0, out _);
                var unsafeReadOnly = default(UnsafeHashMap<int, int>.ReadOnly);
                _ = unsafeReadOnly.ContainsKey(0); _ = unsafeReadOnly.TryGetValue(0, out _); _ = unsafeReadOnly[0];
            }
        }
        public struct NativeMapEffectsKey : IEquatable<NativeMapEffectsKey>, IFormattable {
            bool IEquatable<NativeMapEffectsKey>.Equals(NativeMapEffectsKey other) { _ = default(Ent).Read<Test1Component>(); return true; }
            public bool Equals(NativeMapEffectsKey other) { default(Ent).Set(new Test3Component()); return false; }
            public override int GetHashCode() { default(Ent).Set(new TestComponent()); return 0; }
            string IFormattable.ToString(string format, IFormatProvider provider) { default(Ent).Set(new Test2Component()); return "key"; }
            public override string ToString() { default(Ent).Set(new Test3Component()); return "not the formatter"; }
        }
        public static TValue NativeMapRead<TKey, TValue>(NativeHashMap<TKey, TValue> map, TKey key)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged {
            map.TryGetValue(key, out var value); return value;
        }
        public partial struct NativeMapContainsJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeMapEffectsKey, int>).ContainsKey(default);
        }
        public partial struct NativeMapRemoveJob : IJob {
            public void Execute() => _ = default(UnsafeHashMap<NativeMapEffectsKey, int>).Remove(default);
        }
        public partial struct NativeMapReadJob : IJob {
            public void Execute() => _ = NativeMapRead(default(NativeHashMap<NativeMapEffectsKey, Test2Component>), default(NativeMapEffectsKey));
        }
        public partial struct NativeMapIndexerJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeMapEffectsKey, int>)[default];
        }
        public partial struct NativeMapReadOnlyIndexerJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeMapEffectsKey, int>.ReadOnly)[default];
        }
        public partial struct NativeMapUnsafeReadOnlyIndexerJob : IJob {
            public void Execute() => _ = default(UnsafeHashMap<NativeMapEffectsKey, int>.ReadOnly)[default];
        }
        public struct NativeMapComponentKey : IComponent, IEquatable<NativeMapComponentKey> {
            public bool Equals(NativeMapComponentKey other) => true;
            public override int GetHashCode() => 0;
        }
        public partial struct NativeMapComponentKeyJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeMapComponentKey, TestComponent>).TryGetValue(default, out _);
        }
        public struct NativeMapGenericKeys<T> where T : unmanaged, IComponent {
            public struct Key<U> : IEquatable<Key<U>> where U : unmanaged, IComponent {
                bool IEquatable<Key<U>>.Equals(Key<U> other) { default(Ent).Set(new U()); return true; }
                public override int GetHashCode() { _ = default(Ent).Read<T>(); return 0; }
            }
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct NativeMapJob : IJob {
                public void Execute() => _ = default(NativeHashMap<NativeMapGenericKeys<T>.Key<Test1Component>, T>).TryGetValue(default, out _);
            }
        }
        public struct NativeMapCreatingKey : IEquatable<NativeMapCreatingKey> {
            public bool Equals(NativeMapCreatingKey other) { Ent.New(); return true; }
            public override int GetHashCode() { Ent.New(); return 0; }
            public override string ToString() { Ent.New(); return "key"; }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct NativeMapCountJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeMapCreatingKey, int>).ContainsKey(default);
        }
        [EntitiesJobMaxCount(8)]
        public partial struct NativeMapRepeatedCountJob : IJob {
            public void Execute() { default(NativeMapCountJob).Execute(); default(NativeMapCountJob).Execute(); }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct NativeMapIndexerCountJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeMapCreatingKey, int>)[default];
        }
        [EntitiesJobMaxCount(8)]
        public partial struct NativeMapUnsafeReadOnlyCountJob : IJob {
            public void Execute() => _ = default(UnsafeHashMap<NativeMapCreatingKey, int>.ReadOnly)[default];
        }
        public struct NativeMapHidingHashKey : IEquatable<NativeMapHidingHashKey> {
            public bool Equals(NativeMapHidingHashKey other) => true;
            public new int GetHashCode() { default(Ent).Set(new Test3Component()); return 0; }
        }
        public partial struct NativeMapHidingHashJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeMapHidingHashKey, int>).ContainsKey(default);
        }
        public partial struct NativeMapGrowthJob : IJob {
            public void Execute() => default(NativeHashMap<int, int>).Add(0, 0);
        }
        public struct NativeMapOpaqueFormatKey : IEquatable<NativeMapOpaqueFormatKey> {
            public bool Equals(NativeMapOpaqueFormatKey other) => true;
            public override int GetHashCode() => 0;
            public override string ToString() { NativeContainerOpaqueKey.callback(); return "key"; }
        }
        public partial struct NativeMapOpaqueFormatJob : IJob {
            public void Execute() => _ = default(NativeHashMap<NativeMapOpaqueFormatKey, int>)[default];
        }
        public struct NativeMapSchedulingHashKey : IEquatable<NativeMapSchedulingHashKey> {
            public bool Equals(NativeMapSchedulingHashKey other) => true;
            public override int GetHashCode() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return 0; }
        }
        public struct NativeMapSchedulingEqualsKey : IEquatable<NativeMapSchedulingEqualsKey> {
            public bool Equals(NativeMapSchedulingEqualsKey other) { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return true; }
            public override int GetHashCode() => 0;
        }
        public struct NativeMapCompletedKey : IEquatable<NativeMapCompletedKey> {
            public bool Equals(NativeMapCompletedKey other) { IJobExtensions.Schedule(default(ControlFirstJob), default).Complete(); return true; }
            public override int GetHashCode() { IJobExtensions.Schedule(default(ControlSecondJob), default).Complete(); return 0; }
        }
        public struct NativeMapSchedulingFormatKey : IEquatable<NativeMapSchedulingFormatKey> {
            public bool Equals(NativeMapSchedulingFormatKey other) => true;
            public override int GetHashCode() => 0;
            public override string ToString() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return "key"; }
        }
        public partial struct NativeMapBeforeSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = default(NativeHashMap<int, int>).ContainsKey(0); context.dependsOn.Complete(); }
        }
        public partial struct NativeMapAfterSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = default(NativeHashMap<int, int>).ContainsKey(0); }
        }
        public partial struct NativeMapHashSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = default(NativeHashMap<NativeMapSchedulingHashKey, int>).ContainsKey(default); }
        }
        public partial struct NativeMapEqualsSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = default(NativeHashMap<NativeMapSchedulingEqualsKey, int>).ContainsKey(default); }
        }
        public partial struct NativeMapCompletedSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = default(NativeHashMap<NativeMapCompletedKey, int>).ContainsKey(default); }
        }
        public partial struct NativeMapFormattingSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                try { _ = default(NativeHashMap<NativeMapSchedulingFormatKey, int>)[default]; } catch (System.Exception) { }
                _ = default(Ent).Read<TestComponent>();
            }
        }
        public partial struct NativeMapHandleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); NativeMapRead(default(NativeHashMap<int, JobHandle>), 0).Complete(); }
        }
        public partial struct NativeMapIndexerHandleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); default(UnsafeHashMap<int, JobHandle>.ReadOnly)[0].Complete(); }
        }

        [Test]
        public void NativeMapScalarOperationsHaveCompleteSourceContracts() {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(typeof(NativeMapScalarJob), "ME.BECS." + kind + ".v1");
                Assert.AreEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("D\t", StringComparison.Ordinal) || row.StartsWith("C\t", StringComparison.Ordinal)));
            }
        }

        [TestCase(typeof(NativeMapContainsJob), -1)]
        [TestCase(typeof(NativeMapRemoveJob), -1)]
        [TestCase(typeof(NativeMapReadJob), 0)]
        [TestCase(typeof(NativeMapIndexerJob), 2)]
        [TestCase(typeof(NativeMapReadOnlyIndexerJob), 2)]
        [TestCase(typeof(NativeMapUnsafeReadOnlyIndexerJob), -1)]
        public void NativeMapDispatchSelectsActualHashEqualsAndFormatter(Type job, int valueMode) {
            var reader = CreateSafetyReader(() => Assert.Fail("Known key callbacks must use source summaries."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            var expected = new[] { SafetyExceptionDependency(typeof(TestComponent), 2), SafetyExceptionDependency(typeof(Test1Component), 0) }
                .Concat(valueMode < 0 ? Array.Empty<string>() : new[] { SafetyExceptionDependency(typeof(Test2Component), valueMode) })
                .OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [Test]
        public void NativeMapGenericDependenciesKeepKeyAndValueTypes() {
            foreach (var job in new[] { typeof(NativeMapComponentKeyJob), typeof(GenericAotSystem<AotMarker>.NativeMapJob) }) {
                var reader = CreateSafetyReader(() => Assert.Fail("Closed key/value types must use source."));
                var expected = job == typeof(NativeMapComponentKeyJob)
                    ? new[] { SafetyExceptionDependency(typeof(NativeMapComponentKey), 0), SafetyExceptionDependency(typeof(TestComponent), 0) }
                    : new[] { SafetyExceptionDependency(typeof(AotMarker), 0), SafetyExceptionDependency(typeof(Test1Component), 2) };
                CollectionAssert.AreEqual(expected.OrderBy(row => row, StringComparer.Ordinal).ToArray(), SafetySelectionRecords(SelectJobSafety(reader, job)));
            }
        }

        [TestCase(typeof(NativeMapCountJob), "1", "1")]
        [TestCase(typeof(NativeMapRepeatedCountJob), "2", "2")]
        [TestCase(typeof(NativeMapIndexerCountJob), "2", "1")]
        [TestCase(typeof(NativeMapUnsafeReadOnlyCountJob), "1", "1")]
        public void NativeMapCollisionCountsRetainLoopContext(Type job, string inline, string loop) {
            var rows = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var count = rows.Select(row => row.Split('\t')).Single(row => row[0] == "C");
            Assert.AreEqual(inline, count[3]); Assert.AreEqual(loop, count[4]);
            Assert.IsTrue(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
        }

        [TestCase(typeof(NativeMapHidingHashJob))]
        [TestCase(typeof(NativeMapGrowthJob))]
        [TestCase(typeof(NativeContainerUnknownKeyJob))]
        [TestCase(typeof(NativeMapOpaqueFormatJob))]
        public void NativeMapUnresolvedCallbacksCannotBecomeComplete(Type job) {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + kind + ".v1");
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\t", StringComparison.Ordinal)));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }

        [TestCase(typeof(NativeMapBeforeSystem), "unproven")]
        [TestCase(typeof(NativeMapAfterSystem), "proven")]
        [TestCase(typeof(NativeMapHashSystem), "unproven")]
        [TestCase(typeof(NativeMapEqualsSystem), "unproven")]
        [TestCase(typeof(NativeMapCompletedSystem), "proven")]
        [TestCase(typeof(NativeMapFormattingSystem), "unproven")]
        [TestCase(typeof(NativeMapHandleSystem), "incomplete")]
        [TestCase(typeof(NativeMapIndexerHandleSystem), "incomplete")]
        public void NativeMapSynchronizationIncludesRepeatedAndThrowingCallbacks(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.IsFalse(rows.Any(row => row.Contains("MalformedSynchronizationFlow")), string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.AreEqual(expected == "incomplete", rows[2] != "0", string.Join("\n", rows));
        }

        [TestCase(typeof(NativeMapHashSystem), false)]
        [TestCase(typeof(NativeMapEqualsSystem), false)]
        [TestCase(typeof(NativeMapFormattingSystem), false)]
        [TestCase(typeof(NativeMapCompletedSystem), true)]
        public void NativeMapScheduledJobDiscoveryIncludesKeyCallbacks(Type system, bool bothJobs) {
            var rows = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var types = bothJobs ? new[] { typeof(ControlFirstJob), typeof(ControlSecondJob) } : new[] { typeof(ControlFirstJob) };
            var expected = types.Select(type => {
                var identity = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(type.Assembly.FullName + "\nT:" + type.FullName.Replace('+', '.')));
                return "J\tn" + identity.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + identity + "0:";
            }).ToArray();
            CollectionAssert.AreEquivalent(expected, rows.Where(row => row.StartsWith("J\t", StringComparison.Ordinal)).ToArray());
        }
    }
}
