using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Inspect summaries only. No formatting, provider, callback or native job is invoked.
        public static void BclFormatNumbers() {
            _ = ((sbyte)1).ToString(CultureInfo.InvariantCulture); _ = ((byte)1).ToString(NumberFormatInfo.InvariantInfo);
            _ = ((short)1).ToString(CultureInfo.InvariantCulture); _ = ((ushort)1).ToString(NumberFormatInfo.InvariantInfo);
            _ = 1.ToString("D", CultureInfo.InvariantCulture); _ = 1u.ToString(NumberFormatInfo.InvariantInfo);
            _ = 1L.ToString(CultureInfo.InvariantCulture); _ = 1UL.ToString(NumberFormatInfo.InvariantInfo);
            _ = 1f.ToString("R", CultureInfo.InvariantCulture); _ = 1d.ToString(NumberFormatInfo.InvariantInfo);
            _ = 1m.ToString(CultureInfo.InvariantCulture);
        }
        public static void BclFormatIndependent() {
            _ = true.ToString(); _ = true.ToString(bclFormatProvider);
            _ = 'a'.ToString(); _ = 'a'.ToString(bclFormatProvider);
            _ = default(Guid).ToString(); _ = default(Guid).ToString("D"); _ = default(Guid).ToString("X", bclFormatProvider);
            _ = $"{true}{'x'}{default(Guid):D}"; _ = "x" + true + 'x' + default(Guid);
            _ = FormattingGeneric(default(Guid)); _ = FormattingConcatGeneric(true);
        }
        public static string BclFormatGeneric<T>(T value) where T : struct, IFormattable => value.ToString("D", CultureInfo.InvariantCulture);
        public static IFormatProvider bclFormatProvider;
        public static int BclFormatNumber() { default(Ent).Set(new TestComponent()); Ent.New(); return 1; }
        public static IFormatProvider BclFormatProvider() { default(Ent).Set(new Test1Component()); Ent.New(); return bclFormatProvider; }
        public static IFormatProvider BclFormatScheduledProvider() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return bclFormatProvider; }
        public static void BclFormatArguments() {
            _ = BclFormatNumber().ToString("D", CultureInfo.InvariantCulture);
            _ = true.ToString(BclFormatProvider());
            _ = default(Guid).ToString("D", BclFormatProvider());
        }
        public partial struct BclFormatCoreJob : IJob { public void Execute() { BclFormatNumbers(); BclFormatIndependent(); } }
        public partial struct BclFormatArgumentsJob : IJob { public void Execute() => BclFormatArguments(); }
        public partial struct BclFormatGenericNumericJob : IJob { public void Execute() { _ = BclFormatGeneric(1); _ = BclFormatGeneric(1d); } }
        public partial struct BclFormatGenericGuidJob : IJob { public void Execute() => _ = BclFormatGeneric(default(Guid)); }
        public partial struct BclFormatGenericUserJob : IJob { public void Execute() => _ = BclFormatGeneric(default(ExplicitFormattingEffects)); }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct BclFormatJob : IJob {
                public void Execute() { _ = default(Ent).Read<T>(); _ = BclFormatGeneric(1); _ = FormattingGeneric(default(Guid)); }
            }
        }
        public partial struct BclFormatMapJob : IJob {
            public void Execute() {
                var map = new NativeHashMap<bool, bool>(1, Allocator.Temp);
                map.Add(true, false); map.TryAdd(false, true); map[true] = true; _ = map[true]; map.Dispose();
            }
        }
        public static void BclFormatAmbient() { _ = 1.ToString(); _ = $"{1}"; }
        public static void BclFormatNull() => _ = 1.ToString((IFormatProvider)null);
        public static void BclFormatUnknown() => _ = 1.ToString(bclFormatProvider);
        public static void BclFormatLocal() { var provider = CultureInfo.InvariantCulture; _ = 1.ToString(provider); }
        public static void BclFormatFactory() => _ = 1.ToString(BclFormatProvider());
        public partial struct BclFormatAmbientJob : IJob { public void Execute() => BclFormatAmbient(); }
        public partial struct BclFormatNullJob : IJob { public void Execute() => BclFormatNull(); }
        public partial struct BclFormatUnknownJob : IJob { public void Execute() => BclFormatUnknown(); }
        public partial struct BclFormatLocalJob : IJob { public void Execute() => BclFormatLocal(); }
        public partial struct BclFormatFactoryJob : IJob { public void Execute() => BclFormatFactory(); }
        public static int BclFormatScheduledNumber() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return 1; }
        public partial struct BclFormatPureSystem : IUpdate { public void OnUpdate(ref SystemContext context) { BclFormatNumbers(); BclFormatIndependent(); } }
        public partial struct BclFormatLateReceiverSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); _ = BclFormatScheduledNumber().ToString(CultureInfo.InvariantCulture); _ = default(Ent).Read<TestComponent>();
            }
        }
        public partial struct BclFormatLateProviderSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete(); _ = default(Guid).ToString("D", BclFormatScheduledProvider()); _ = default(Ent).Read<TestComponent>();
            }
        }
        public partial struct BclFormatUnknownSystem : IUpdate { public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); BclFormatUnknown(); } }

        [Test]
        public void BclFormattingContractsSeparateProviderFactsFromPurity() {
            var rows = ExternalValueOperations(nameof(BclFormatNumbers));
            var calls = rows.Where(row => row.Contains("!invariant-format-provider=1")).ToArray();
            Assert.AreEqual(11, calls.Length, string.Join("\n", rows.Select(row => string.Join("\t", row))));
            Assert.IsFalse(calls.Any(row => row.Contains("!ecs-leaf")), "Numeric purity is selected only after validating the provider fact and exact target.");
            var getters = rows.Where(row => row[3].Contains(".get_InvariantCulture") || row[3].Contains(".get_InvariantInfo")).ToArray();
            Assert.IsTrue(getters.Length > 0 && getters.All(row => row.Contains("!ecs-leaf") && row.Contains("!bcl-invariant-format=1")));
        }
        [TestCase(typeof(BclFormatCoreJob))]
        [TestCase(typeof(BclFormatGenericNumericJob))]
        [TestCase(typeof(BclFormatGenericGuidJob))]
        [TestCase(typeof(BclFormatMapJob))]
        public void BclInvariantFormattingUsesCompleteSourceWithoutIL(Type job) {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + kind + ".v1");
                Assert.AreEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) || row.StartsWith("D\t", StringComparison.Ordinal)));
                if (kind == "JobWeights") Assert.AreEqual("0", rows[3]);
            }
        }
        [Test]
        public void BclInvariantFormattingRetainsEveryReceiverAndProviderArgument() {
            var job = typeof(BclFormatArgumentsJob);
            var reader = CreateSafetyReader(() => Assert.Fail("Proven BCL formatting must not read legacy IL."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(Test1Component), 2), SafetyExceptionDependency(typeof(TestComponent), 2) }
                .OrderBy(row => row, StringComparer.Ordinal).ToArray(), SafetySelectionRecords(SelectJobSafety(reader, job)));
            var counts = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsTrue(counts.Single(row => row.StartsWith("C\t", StringComparison.Ordinal)).EndsWith("\t3\t0", StringComparison.Ordinal));
            var weights = ControlSummary(job, "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            Assert.AreEqual("14", weights[3]);
        }
        [TestCase(typeof(BclFormatGenericUserJob), typeof(TestComponent), 2)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.BclFormatJob), typeof(AotMarker), 0)]
        public void BclInvariantProviderCannotEraseGenericUserEffects(Type job, Type component, int mode) {
            var reader = CreateSafetyReader(() => Assert.Fail("Closed formatting effects must use source."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(component, mode) }, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }
        [TestCase(nameof(BclFormatAmbient))]
        [TestCase(nameof(BclFormatNull))]
        [TestCase(nameof(BclFormatUnknown))]
        [TestCase(nameof(BclFormatLocal))]
        [TestCase(nameof(BclFormatFactory))]
        public void BclFormattingProviderProofDoesNotEscapeIntoUnknownValues(string method) {
            Assert.IsFalse(ExternalValueOperations(method).Any(row => row.Contains("!invariant-format-provider=1")));
        }
        [TestCase(typeof(BclFormatAmbientJob))]
        [TestCase(typeof(BclFormatNullJob))]
        [TestCase(typeof(BclFormatUnknownJob))]
        [TestCase(typeof(BclFormatLocalJob))]
        [TestCase(typeof(BclFormatFactoryJob))]
        public void BclUnknownFormattingCannotPublishCompleteCatalogs(Type job) {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + kind + ".v1");
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }
        [TestCase(typeof(BclFormatPureSystem), "proven")]
        [TestCase(typeof(BclFormatLateReceiverSystem), "unproven")]
        [TestCase(typeof(BclFormatLateProviderSystem), "unproven")]
        [TestCase(typeof(BclFormatUnknownSystem), "incomplete")]
        public void BclFormattingPreservesOrderedSynchronization(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.AreEqual(expected == "incomplete", rows[2] != "0", string.Join("\n", rows));
        }
    }
}
