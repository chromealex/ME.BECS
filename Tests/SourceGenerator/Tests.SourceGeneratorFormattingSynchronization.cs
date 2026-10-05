using System;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only: formatters/systems must never execute in these tests.
        public struct FormattingAccessValue : IFormattable {
            public override string ToString() { default(Ent).Set(new Test1Component()); return "plain"; }
            string IFormattable.ToString(string format, IFormatProvider provider) {
                default(Ent).Set(new Test2Component()); return "formatted";
            }
        }
        public struct FormattingDualSchedule : IFormattable {
            public override string ToString() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return "scheduled"; }
            string IFormattable.ToString(string format, IFormatProvider provider) => "no scheduling";
        }
        public sealed class FormattingReferenceSchedule : IFormattable {
            string IFormattable.ToString(string format, IFormatProvider provider) {
                _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return "scheduled";
            }
        }
        public static FormattingReferenceSchedule formattingReferenceSchedule;
        public sealed class FormattingReferenceNoop {
            public override string ToString() => "noop";
        }
        public static FormattingReferenceNoop formattingReferenceNoop;
        public static string FormattingCompleteText(ref SystemContext context) { context.dependsOn.Complete(); return "completed"; }
        public static string FormattingAccessText() { default(Ent).Set(new TestComponent()); return "accessed"; }
        public static string FormattingConcatThenComplete<T>(T value, ref SystemContext context) => value + FormattingCompleteText(ref context);
        public static string FormattingInterpolationThenComplete<T>(T value, ref SystemContext context) => $"{value} {FormattingCompleteText(ref context)}";
        public static string FormattingProperty {
            get { default(Ent).Set(new Test3Component()); return "getter"; }
            set { default(Ent).Set(new Test2Component()); }
        }
        public static string FormattingSchedulingProperty {
            get { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return "getter"; }
            set { }
        }

        public partial struct FormatConcatBeforeCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = "prefix" + default(FormattingAccessValue) + FormattingCompleteText(ref context); }
        }
        public partial struct FormatConcatAfterCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = "prefix" + FormattingCompleteText(ref context) + default(FormattingAccessValue); }
        }
        public partial struct FormatInterpolationBeforeCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = $"{default(FormattingAccessValue)} {FormattingCompleteText(ref context)}"; }
        }
        public partial struct FormatInterpolationAfterCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = $"{FormattingCompleteText(ref context)} {default(FormattingAccessValue)}"; }
        }
        public partial struct FormatConcatBeforeAccessSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = "prefix" + default(SchedulingFormattingEffects) + FormattingAccessText();
            }
        }
        public partial struct FormatInterpolationBeforeAccessSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = $"{default(SchedulingFormattingEffects)} {FormattingAccessText()}";
            }
        }
        public partial struct FormatInterpolationPairSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = $"{default(SchedulingFormattingEffects)} {default(FormattingAccessValue)}";
            }
        }
        public partial struct FormatInterpolationReversePairSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = $"{default(FormattingAccessValue)} {default(SchedulingFormattingEffects)}";
            }
        }
        public partial struct FormatGenericConcatSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = FormattingConcatThenComplete(default(FormattingAccessValue), ref context); }
        }
        public partial struct FormatGenericInterpolationSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = FormattingInterpolationThenComplete(default(FormattingAccessValue), ref context); }
        }
        public partial struct FormatDualConcatSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = FormattingConcatGeneric(default(FormattingDualSchedule));
                _ = FormattingAccessText();
            }
        }
        public partial struct FormatDualInterpolationSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = FormattingGeneric(default(FormattingDualSchedule));
                _ = FormattingAccessText();
            }
        }
        public partial struct FormatPropertySetterSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                FormattingProperty += default(SchedulingFormattingEffects);
            }
        }
        public partial struct FormatPropertyGetterSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                FormattingSchedulingProperty += default(FormattingAccessValue);
            }
        }
        public partial struct FormatReferenceSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = FormattingGeneric(formattingReferenceSchedule);
                _ = FormattingAccessText();
            }
        }
        public partial struct FormatNullableSystem : IUpdate {
            public SchedulingFormattingEffects? value;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = $"{this.value}";
                _ = FormattingAccessText();
            }
        }
        public partial struct FormatDeferredSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                FormattableString text = $"{default(SchedulingFormattingEffects)}";
                _ = FormattingAccessText();
            }
        }
        public partial struct FormatDeferredArgumentSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                FormattableString text = $"{FormattingAccessText()}";
                context.dependsOn.Complete();
            }
        }
        public partial struct FormatHandlePendingSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                _ = FormattingConcatGeneric(context.dependsOn);
                _ = FormattingAccessText();
            }
        }
        public partial struct FormatHandleCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                _ = FormattingConcatGeneric(context.dependsOn);
                context.dependsOn.Complete();
                _ = FormattingAccessText();
            }
        }
        public static string FormattingRepeatedReferences<T>(T value) =>
            $"{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}{value}";
        public partial struct FormatManyReferenceNoopsSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = FormattingRepeatedReferences(formattingReferenceNoop);
                _ = FormattingAccessText();
            }
        }
        public partial struct FormatManyReferenceSchedulesSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = FormattingRepeatedReferences(formattingReferenceSchedule);
                _ = FormattingAccessText();
            }
        }
        public struct GenericFormattingAccess<T> : IFormattable where T : unmanaged, IComponent {
            string IFormattable.ToString(string format, IFormatProvider provider) { default(Ent).Set(default(T)); return "generic"; }
        }
        public partial struct FormattingGenericSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = FormattingGeneric(default(GenericFormattingAccess<T>));
            }
        }

        [TestCase(typeof(FormatConcatBeforeCompleteSystem), "unproven")]
        [TestCase(typeof(FormatConcatAfterCompleteSystem), "proven")]
        [TestCase(typeof(FormatInterpolationBeforeCompleteSystem), "proven")]
        [TestCase(typeof(FormatInterpolationAfterCompleteSystem), "proven")]
        [TestCase(typeof(FormatConcatBeforeAccessSystem), "unproven")]
        [TestCase(typeof(FormatInterpolationBeforeAccessSystem), "proven")]
        [TestCase(typeof(FormatInterpolationPairSystem), "unproven")]
        [TestCase(typeof(FormatInterpolationReversePairSystem), "proven")]
        [TestCase(typeof(FormatGenericConcatSystem), "unproven")]
        [TestCase(typeof(FormatGenericInterpolationSystem), "proven")]
        [TestCase(typeof(FormatDualConcatSystem), "unproven")]
        [TestCase(typeof(FormatDualInterpolationSystem), "proven")]
        [TestCase(typeof(FormatPropertySetterSystem), "unproven")]
        [TestCase(typeof(FormatPropertyGetterSystem), "unproven")]
        [TestCase(typeof(FormatReferenceSystem), "unproven")]
        [TestCase(typeof(FormatNullableSystem), "unproven")]
        [TestCase(typeof(FormatDeferredSystem), "proven")]
        [TestCase(typeof(FormatDeferredArgumentSystem), "unproven")]
        [TestCase(typeof(FormatHandlePendingSystem), "unproven")]
        [TestCase(typeof(FormatHandleCompleteSystem), "proven")]
        [TestCase(typeof(FormatManyReferenceNoopsSystem), "proven")]
        [TestCase(typeof(FormatManyReferenceSchedulesSystem), "unproven")]
        [TestCase(typeof(FormattingGenericSystem<AotMarker>), "proven")]
        public void ImplicitFormattingSynchronizationUsesCompilerEvaluationOrder(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
        }
    }
}
