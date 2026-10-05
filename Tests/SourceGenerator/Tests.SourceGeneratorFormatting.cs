using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // The C# operations for interpolation/concatenation need not contain calls
        // to these effectful methods. Fixtures are inspected as metadata, never run.
        public struct FormattingEffects : IFormattable {
            public override string ToString() {
                Ent.New();
                default(Ent).Set(new Test2Component());
                return "value";
            }
            public string ToString(string format, IFormatProvider provider) {
                default(Ent).Set(new Test3Component());
                return "formatted";
            }
        }
        public static string FormattingText(in Ent ent) {
            ent.Set(new Test1Component());
            return "text";
        }
        public partial struct FormattingInterpolationJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() { _ = $"{FormattingText(in this.ent)} {default(FormattingEffects):custom}"; }
        }
        public partial struct FormattingConcatenationJob : Unity.Jobs.IJob {
            public void Execute() { _ = "value: " + default(FormattingEffects); }
        }
        public partial struct FormattingCompoundJob : Unity.Jobs.IJob {
            public void Execute() {
                var text = "value: ";
                text += default(FormattingEffects);
            }
        }
        public static string FormattingGeneric<T>(T value) => $"{value}";
        public partial struct FormattingGenericJob : Unity.Jobs.IJob {
            public void Execute() { _ = FormattingGeneric(default(FormattingEffects)); }
        }
        public static string FormattingConcatGeneric<T>(T value) => "value: " + value;
        public partial struct FormattingGenericConcatJob : Unity.Jobs.IJob {
            public void Execute() { _ = FormattingConcatGeneric(default(FormattingEffects)); }
        }
        public partial struct FormattingRepeatedJob : Unity.Jobs.IJob {
            public void Execute() {
                _ = FormattingConcatGeneric(default(FormattingEffects));
                _ = FormattingConcatGeneric(default(FormattingEffects));
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FormattingLoopJob : Unity.Jobs.IJob {
            public void Execute() {
                for (var i = 0; i < 2; ++i) _ = FormattingConcatGeneric(default(FormattingEffects));
            }
        }
        public struct ExplicitFormattingEffects : IFormattable {
            string IFormattable.ToString(string format, IFormatProvider provider) {
                Ent.New();
                default(Ent).Set(new TestComponent());
                return "formatted";
            }
            public override string ToString() { default(Ent).Set(new Test2Component()); return "plain"; }
        }
        public sealed class SealedFormattingEffects : IFormattable {
            string IFormattable.ToString(string format, IFormatProvider provider) {
                Ent.New();
                default(Ent).Set(new TestComponent());
                return "formatted";
            }
        }
        public class FormattingBase {
            public override string ToString() { Ent.New(); default(Ent).Set(new Test2Component()); return "base"; }
        }
        public sealed class FormattingHidingDerived : FormattingBase {
            public new string ToString() { default(Ent).Set(new Test3Component()); return "hidden"; }
        }
        public struct FormattingHidingValue {
            public new string ToString() { default(Ent).Set(new Test3Component()); return "hidden"; }
        }
        public struct FormattingConversion {
            public static implicit operator FormattingEffects(FormattingConversion value) {
                default(Ent).Set(new TestComponent());
                return default;
            }
        }
        public static SealedFormattingEffects sealedFormattingValue;
        public static FormattingHidingDerived inheritedFormattingValue;
        public static object unknownFormattingValue;
        public static FormattingBase unknownFormattingBase;
        public static IFormattable unknownFormattable;
        public static int[] unknownFormattingArray;
        public partial struct FormattingExplicitJob : Unity.Jobs.IJob {
            public void Execute() { _ = $"{default(ExplicitFormattingEffects):custom}"; }
        }
        public partial struct FormattingSealedJob : Unity.Jobs.IJob {
            public void Execute() { _ = FormattingGeneric(sealedFormattingValue); }
        }
        public partial struct FormattingInheritedJob : Unity.Jobs.IJob {
            public void Execute() { _ = "value: " + inheritedFormattingValue; }
        }
        public partial struct FormattingHiddenJob : Unity.Jobs.IJob {
            public void Execute() { _ = "value: " + default(FormattingHidingValue); }
        }
        public partial struct FormattingNullableJob : Unity.Jobs.IJob {
            public FormattingEffects? value;
            public void Execute() { _ = $"{this.value:custom}"; }
        }
        public partial struct FormattingConversionJob : Unity.Jobs.IJob {
            public void Execute() { _ = $"{(FormattingEffects)default(FormattingConversion):custom}"; }
        }
        public partial struct FormattingUnknownJob : Unity.Jobs.IJob {
            public void Execute() { _ = $"{unknownFormattingValue}"; }
        }
        public partial struct FormattingUnknownBaseJob : Unity.Jobs.IJob {
            public void Execute() { _ = "value: " + unknownFormattingBase; }
        }
        public partial struct FormattingUnknownInterfaceJob : Unity.Jobs.IJob {
            public void Execute() { _ = $"{unknownFormattable}"; }
        }
        public partial struct FormattingArrayJob : Unity.Jobs.IJob {
            public void Execute() { _ = $"{unknownFormattingArray}"; }
        }
        public partial struct FormattingDeferredJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() { FormattableString text = $"{FormattingText(in this.ent)} {default(FormattingEffects):custom}"; }
        }
        public struct GenericFormattingEffects<T> : IFormattable where T : unmanaged, IComponent {
            string IFormattable.ToString(string format, IFormatProvider provider) { Ent.New(); default(Ent).Set(default(T)); return "generic"; }
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct FormattingJob : Unity.Jobs.IJob {
                public void Execute() { _ = FormattingGeneric(default(GenericFormattingEffects<T>)); }
            }
        }
        public struct SchedulingFormattingEffects {
            public override string ToString() { _ = Unity.Jobs.IJobExtensions.Schedule(default(ControlFirstJob), default); return "scheduled"; }
        }
        public partial struct FormattingConcatSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = "value: " + default(SchedulingFormattingEffects);
                default(Ent).Set(new TestComponent());
            }
        }
        public partial struct FormattingInterpolationSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = $"{default(SchedulingFormattingEffects)}";
                default(Ent).Set(new TestComponent());
            }
        }
        public partial struct FormattingCompoundSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                var text = "value: ";
                text += default(SchedulingFormattingEffects);
                default(Ent).Set(new TestComponent());
            }
        }
        public partial struct FormattingStringsJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() {
                _ = $"{FormattingText(in this.ent)} {typeof(FormattingEffects).Name}";
                _ = "value: " + FormattingText(in this.ent);
            }
        }

        [TestCase(typeof(FormattingUnknownJob))]
        [TestCase(typeof(FormattingUnknownBaseJob))]
        [TestCase(typeof(FormattingUnknownInterfaceJob))]
        [TestCase(typeof(FormattingArrayJob))]
        public void ImplicitFormattingCannotCertifySafetyCountsOrWeights(Type job) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var reader = CreateSafetyReader(() => Assert.Fail("Reading source coverage must not invoke IL."));
            Assert.AreEqual("Incomplete", ReadJobSafety(reader, job, out var safety, out var source));
            Assert.IsNull(source);
            Assert.IsTrue(safety.Any(row => row.StartsWith("G\tImplicitFormatting", StringComparison.Ordinal)), string.Join("\n", safety));
            var attributes = job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            foreach (var key in new[] { "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = attributes.Single(attribute => attribute.Key == key && attribute.Value.StartsWith(job.FullName + "\n", StringComparison.Ordinal)).Value.Split('\n');
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tImplicitFormatting", StringComparison.Ordinal)));
            }
        }

        [TestCase(typeof(FormattingConcatenationJob), typeof(Test2Component), "1", "0")]
        [TestCase(typeof(FormattingCompoundJob), typeof(Test2Component), "1", "0")]
        [TestCase(typeof(FormattingGenericConcatJob), typeof(Test2Component), "1", "0")]
        [TestCase(typeof(FormattingRepeatedJob), typeof(Test2Component), "2", "0")]
        [TestCase(typeof(FormattingLoopJob), typeof(Test2Component), "0", "1")]
        [TestCase(typeof(FormattingGenericJob), typeof(Test3Component), "0", "0")]
        [TestCase(typeof(FormattingExplicitJob), typeof(TestComponent), "1", "0")]
        [TestCase(typeof(FormattingSealedJob), typeof(TestComponent), "1", "0")]
        [TestCase(typeof(FormattingInheritedJob), typeof(Test2Component), "1", "0")]
        [TestCase(typeof(FormattingHiddenJob), null, "0", "0")]
        [TestCase(typeof(FormattingNullableJob), typeof(Test3Component), "0", "0")]
        [TestCase(typeof(GenericAotSystem<AotMarker>.FormattingJob), typeof(AotMarker), "1", "0")]
        public void ImplicitFormattingResolvesExactTypedEffectsAndEntityCounts(Type job, Type component, string inline, string loop) {
            var reader = CreateSafetyReader(() => Assert.Fail("Concrete formatter must use source, not IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var safety, out _), string.Join("\n", safety ?? Array.Empty<string>()));
            CollectionAssert.AreEqual(component == null ? Array.Empty<string>() : new[] { SafetyExceptionDependency(component, 2) },
                SafetySelectionRecords(SelectJobSafety(reader, job)));
            var counts = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            var entityRows = counts.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray();
            if (inline == "0" && loop == "0") CollectionAssert.IsEmpty(entityRows);
            else {
                Assert.AreEqual(1, entityRows.Length);
                Assert.IsTrue(entityRows[0].EndsWith("\t" + inline + "\t" + loop, StringComparison.Ordinal), entityRows[0]);
            }
            var weights = ControlSummary(job, "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            if (inline != "0" || loop != "0") CollectionAssert.Contains(weights, "W\tME.BECS.Ent.NewEnt_INTERNAL\t10");
        }

        [TestCase(typeof(FormattingInterpolationJob), typeof(Test1Component))]
        [TestCase(typeof(FormattingConversionJob), typeof(TestComponent))]
        public void InterpolationPreservesArgumentAndUserConversionEffects(Type job, Type argumentComponent) {
            var reader = CreateSafetyReader(() => Assert.Fail("Known interpolation must not use IL."));
            CollectionAssert.AreEqual(new[] { argumentComponent, typeof(Test3Component) }.Select(type => SafetyExceptionDependency(type, 2))
                    .OrderBy(row => row, StringComparer.Ordinal).ToArray(), SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [Test]
        public void DeferredInterpolationStoresFormattingWithoutInvokingIt() {
            var reader = CreateSafetyReader(() => Assert.Fail("Creating FormattableString must not invoke its stored formatter."));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(Test1Component), 2) },
                SafetySelectionRecords(SelectJobSafety(reader, typeof(FormattingDeferredJob))));
            var rows = ControlSummary(typeof(FormattingDeferredJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            Assert.IsFalse(rows.Any(row => row.StartsWith("C\t", StringComparison.Ordinal)));
        }

        [TestCase(typeof(FormattingConcatSystem))]
        [TestCase(typeof(FormattingInterpolationSystem))]
        [TestCase(typeof(FormattingCompoundSystem))]
        public void FormattingScheduledJobsRetainPendingSynchronization(Type system) {
            var jobs = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            Assert.AreEqual("0", jobs[2], string.Join("\n", jobs));
            Assert.AreEqual(1, jobs.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
            var synchronization = SynchronizationSummary(system);
            Assert.AreEqual("0", synchronization[2], string.Join("\n", synchronization));
            CollectionAssert.Contains(synchronization, "S\tunproven");
        }

        [Test]
        public void StringOnlyFormattingRetainsExplicitEffectsWithoutUserDispatch() {
            var reader = CreateSafetyReader(() => Assert.Fail("String-only formatting has no implicit user formatting target."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, typeof(FormattingStringsJob), out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(Test1Component), 2) }, SafetySelectionRecords(SelectJobSafety(reader, typeof(FormattingStringsJob))));
        }
    }
}
