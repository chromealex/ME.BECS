using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Views.Tests {
    public partial class Tests_Views_SourceSafety {
        // Metadata fixtures only: tests never instantiate a view or invoke its callbacks.
        public sealed class ExplicitModule : IViewApplyState {
            void IViewApplyState.ApplyState(in ViewData data) { _ = default(Ent).Read<ComponentA>(); }
            public void ApplyState() { _ = default(Ent).Read<ComponentB>(); }
        }

        public sealed class DualPhaseModule : IViewApplyState, IViewApplyStateParallel {
            public void ApplyState(in ViewData data) { _ = default(Ent).Read<ComponentA>(); }
            void IViewApplyStateParallel.ApplyStateParallel(in ViewData data) { _ = default(Ent).Get<ComponentB>(); }
        }

        public sealed class OverloadedModule : GenericModule<ComponentB> {
            public void ApplyState(int unrelated) { _ = default(Ent).Read<ComponentA>(); }
        }

        public sealed class HiddenModule : GenericModule<ComponentA> {
            public new void ApplyState(in ViewData data) { _ = default(Ent).Read<ComponentB>(); }
        }

        public class NestedContainer<T> where T : unmanaged, IComponent {
            public abstract class Module<TOther> : IViewApplyState where TOther : unmanaged, IComponent {
                public void ApplyState(in ViewData data) {
                    _ = default(Ent).Read<T>();
                    _ = default(Ent).Read<TOther>();
                }
            }
        }
        public sealed class NestedInheritedModule : NestedContainer<ComponentA>.Module<ComponentB> { }

        public class ViewWithOverload : EntityView {
            protected internal override void ApplyState(in ViewData data) { _ = default(Ent).Read<ComponentA>(); }
            public void ApplyState(int unrelated) { _ = default(Ent).Read<ComponentB>(); }
        }
        public sealed class ViewWithHiddenSlot : ViewWithOverload {
            new private void ApplyState(in ViewData data) { _ = default(Ent).Read<ComponentB>(); }
        }

        public sealed class DualRoleView : EntityView, IViewApplyState {
            protected internal override void ApplyState(in ViewData data) { _ = default(Ent).Read<ComponentA>(); }
            void IViewApplyState.ApplyState(in ViewData data) { _ = default(Ent).Get<ComponentB>(); }
        }

        public sealed class NamedOnlyModule : IViewModule {
            public void ApplyState() { _ = default(Ent).Read<ComponentB>(); }
        }

        private struct PrivateTrackingComponent : IComponent { }
        // This fixture deliberately references an inaccessible component to test
        // incomplete safety. It must not request runtime tracking for that type.
        public sealed class PrivateComponentModule : IViewApplyState, IViewIgnoreTracker {
            public void ApplyState(in ViewData data) { _ = default(Ent).Read<PrivateTrackingComponent>(); }
        }

        [Test]
        public void BootstrapRetainsSeparateTrackingRecordsForBothViewRoles() {
            var inputs = Assembly.Load("ME.BECS.Gen.Editor").GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.ViewTrackerSelection.v1")
                .Select(attribute => attribute.Value.Split('\t')).Where(row => row.Length == 3 &&
                    (row[0] == "view-tracker-view" || row[0] == "view-tracker-module"))
                .Select(row => (kind: row[0], values: System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[2])).Split('\n'))).ToArray();
            var view = inputs.Single(row => row.kind == "view-tracker-view" && row.values[0] == typeof(DualRoleView).AssemblyQualifiedName);
            var module = inputs.Single(row => row.kind == "view-tracker-module" && row.values[0] == typeof(DualRoleView).AssemblyQualifiedName);
            CollectionAssert.Contains(view.values, typeof(ComponentA).AssemblyQualifiedName);
            CollectionAssert.Contains(module.values, typeof(ComponentB).AssemblyQualifiedName);
            Assert.IsTrue(typeof(IViewIgnoreTracker).IsAssignableFrom(typeof(PrivateComponentModule)),
                "Metadata-only private-component fixture must not request runtime tracking.");
        }

        [Test]
        public void BootstrapExcludesIgnoredPrivateComponentFromTrackerInputs() {
            // Inspect the exported catalog, not just the marker interface: the owner
            // must remain registered, but neither tracker table may retain its dependency.
            var inputs = Assembly.Load("ME.BECS.Gen.Editor").GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.ViewTrackerSelection.v1")
                .Select(attribute => attribute.Value.Split('\t')).Where(row => row.Length == 3)
                .Select(row => (kind: row[0], values: System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[2])).Split('\n'))).ToArray();
            var global = inputs.Single(row => row.kind == "view-tracker");
            var module = inputs.Single(row => row.kind == "view-tracker-module" &&
                row.values[0] == typeof(PrivateComponentModule).AssemblyQualifiedName);
            Assert.That(global.values.Skip(2), Does.Not.Contain(typeof(PrivateTrackingComponent).AssemblyQualifiedName),
                "Regenerate tracker inputs: an ignored test fixture must not export its private component.");
            Assert.That(module.values.Skip(1).Where(value => value.Length != 0), Is.Empty,
                "IViewIgnoreTracker must preserve owner registration without component dependencies.");
        }

        private static Type ViewSafetyReaderType => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorViewSafety", true);

        private static Func<Type, string, T> ViewFallback<T>(Action visited) => (owner, phase) => {
            visited();
            return Activator.CreateInstance<T>();
        };

        private static object ViewSafetyReader(Action legacyVisited) {
            var type = ViewSafetyReaderType;
            var factory = typeof(Tests_Views_SourceSafety).GetMethod(nameof(ViewFallback), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(type.GetMethod("Select").ReturnType);
            return type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single()
                .Invoke(new[] { factory.Invoke(null, new object[] { legacyVisited }) });
        }

        private static object SelectView(object reader, Type owner, string phase = "ApplyState") {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return reader.GetType().GetMethod("Select").Invoke(reader, new object[] { owner, phase });
        }

        private static string ReadView(object reader, Type owner, string phase, out string[] rows, out object source) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var args = new object[] { owner, phase, null, null, null };
            var status = reader.GetType().GetMethod("ReadSource", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reader, args);
            rows = args[2] as string[];
            source = args[3];
            return status.ToString();
        }

        private static string[] ViewRecords(object source) => ((System.Collections.IEnumerable)source).Cast<object>().Select(item => {
            var type = (Type)item.GetType().GetField("type").GetValue(item);
            return type.FullName + " / " + item.GetType().GetField("op").GetValue(item) + " / " + item.GetType().GetField("isArg").GetValue(item);
        }).OrderBy(row => row, StringComparer.Ordinal).ToArray();

        [TestCase(typeof(InheritedModule), "ApplyState", typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(OverrideModule), "ApplyState", typeof(ComponentB), RefOp.ReadOnly)]
        [TestCase(typeof(ExplicitModule), "ApplyState", typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(OverloadedModule), "ApplyState", typeof(ComponentB), RefOp.ReadOnly)]
        [TestCase(typeof(HiddenModule), "ApplyState", typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(DualPhaseModule), "ApplyState", typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(DualPhaseModule), "ApplyStateParallel", typeof(ComponentB), RefOp.ReadWrite)]
        [TestCase(typeof(ViewWithOverload), "ApplyState", typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(ViewWithHiddenSlot), "ApplyState", typeof(ComponentA), RefOp.ReadOnly)]
        public void CompleteViewSafetySelectsActualCallbackWithoutIL(Type owner, string phase, Type component, RefOp mode) {
            var reader = ViewSafetyReader(() => Assert.Fail("Complete source view safety must not read IL."));
            Assert.AreEqual("Complete", ReadView(reader, owner, phase, out var rows, out var source), string.Join("\n", rows ?? Array.Empty<string>()));
            var expected = new[] { component.FullName + " / " + mode + " / False" };
            CollectionAssert.AreEqual(expected, ViewRecords(source));
            CollectionAssert.AreEqual(expected, ViewRecords(SelectView(reader, owner, phase)));
            var selected = SelectView(reader, owner, phase);
            selected.GetType().GetMethod("Clear").Invoke(selected, null);
            rows[2] = "corrupted caller copy";
            Assert.AreEqual("Complete", ReadView(reader, owner, phase, out _, out _));
            CollectionAssert.AreEqual(expected, ViewRecords(SelectView(reader, owner, phase)));
        }

        [Test]
        public void NestedGenericViewCallbackSubstitutesBothTypeOwners() {
            var reader = ViewSafetyReader(() => Assert.Fail("Closed nested generic base must use source."));
            CollectionAssert.AreEqual(new[] {
                typeof(ComponentA).FullName + " / ReadOnly / False", typeof(ComponentB).FullName + " / ReadOnly / False",
            }, ViewRecords(SelectView(reader, typeof(NestedInheritedModule))));
        }

        [TestCase(typeof(InheritedModule), "ApplyState", true)]
        [TestCase(typeof(NestedInheritedModule), "ApplyState", true)]
        [TestCase(typeof(DualPhaseModule), "ApplyStateParallel", true)]
        [TestCase(typeof(DualRoleView), "ApplyState", false)]
        [TestCase(typeof(DualRoleView), "ApplyState", true)]
        [TestCase(typeof(UnknownDispatchModule), "ApplyState", true)]
        [TestCase(typeof(PrivateComponentModule), "ApplyState", true)]
        public void TrackerExportAlwaysSelectsAnIndependentILSnapshot(Type owner, string phase, bool module) {
            var legacyCalls = 0;
            var reader = ViewSafetyReader(() => ++legacyCalls);
            var export = reader.GetType().GetMethod("SelectForExport");
            var arguments = new object[] { owner, phase, module, null };
            var selected = ViewRecords(export.Invoke(reader, arguments));
            Assert.AreEqual(false, arguments[3]);
            CollectionAssert.IsEmpty(selected, "Even a complete source catalog must not replace an empty IL snapshot.");
            CollectionAssert.AreEqual(selected, ViewRecords(export.Invoke(reader, arguments)));
            Assert.AreEqual(false, arguments[3]);
            Assert.AreEqual(1, legacyCalls);
        }

        [TestCase(typeof(InheritedModule), false, typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(ExplicitModule), true, typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(OverloadedModule), true, typeof(ComponentB), RefOp.ReadOnly)]
        [TestCase(typeof(HiddenModule), true, typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(ViewWithHiddenSlot), false, typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(DualRoleView), false, typeof(ComponentA), RefOp.ReadOnly)]
        [TestCase(typeof(DualRoleView), true, typeof(ComponentB), RefOp.ReadWrite)]
        public void TrackerILSnapshotUsesTheDispatchedCallback(Type owner, bool module, Type component, RefOp mode) {
            var reader = Activator.CreateInstance(ViewSafetyReaderType);
            var export = reader.GetType().GetMethod("SelectForExport");
            var args = new object[] { owner, "ApplyState", module, null };
            var selected = export.Invoke(reader, args);
            CollectionAssert.AreEqual(new[] { component.FullName + " / " + mode + " / False" }, ViewRecords(selected));
            selected.GetType().GetMethod("Clear").Invoke(selected, null);
            CollectionAssert.AreEqual(new[] { component.FullName + " / " + mode + " / False" }, ViewRecords(export.Invoke(reader, args)),
                "Mutating a returned set must not alter the run-local IL snapshot.");
        }

        [Test]
        public void TrackerILSnapshotClosesNestedGenericCallbacks() {
            var reader = Activator.CreateInstance(ViewSafetyReaderType);
            var selected = reader.GetType().GetMethod("SelectForExport").Invoke(reader, new object[] { typeof(NestedInheritedModule), "ApplyState", true, null });
            CollectionAssert.AreEqual(new[] {
                typeof(ComponentA).FullName + " / ReadOnly / False", typeof(ComponentB).FullName + " / ReadOnly / False",
            }, ViewRecords(selected));
        }

        [Test]
        public void TrackerILSnapshotsKeepCallbackRolesAndPhasesSeparate() {
            var reader = Activator.CreateInstance(ViewSafetyReaderType);
            var export = reader.GetType().GetMethod("SelectForExport");
            CollectionAssert.AreEqual(new[] { typeof(ComponentA).FullName + " / ReadOnly / False" },
                ViewRecords(export.Invoke(reader, new object[] { typeof(DualRoleView), "ApplyState", false, null })));
            CollectionAssert.AreEqual(new[] { typeof(ComponentB).FullName + " / ReadWrite / False" },
                ViewRecords(export.Invoke(reader, new object[] { typeof(DualRoleView), "ApplyState", true, null })));
            CollectionAssert.AreEqual(new[] { typeof(ComponentA).FullName + " / ReadOnly / False" },
                ViewRecords(export.Invoke(reader, new object[] { typeof(DualPhaseModule), "ApplyState", true, null })));
            CollectionAssert.AreEqual(new[] { typeof(ComponentB).FullName + " / ReadWrite / False" },
                ViewRecords(export.Invoke(reader, new object[] { typeof(DualPhaseModule), "ApplyStateParallel", true, null })));
        }

        [Test]
        public void TrackerILSnapshotIsIndependentOfSourceComparisonAndCorruptCatalogs() {
            var calls = 0;
            var reader = ViewSafetyReader(() => ++calls);
            var owner = typeof(ExplicitModule);
            CollectionAssert.IsNotEmpty(ViewRecords(SelectView(reader, owner)));
            var catalogs = (System.Collections.IDictionary)reader.GetType().GetField("catalogs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reader);
            ((System.Collections.IDictionary)catalogs[owner.Assembly])["ApplyState\n" + owner.FullName] = new[] { owner.FullName };
            var export = reader.GetType().GetMethod("SelectForExport");
            CollectionAssert.IsEmpty(ViewRecords(export.Invoke(reader, new object[] { owner, "ApplyState", true, null })));
            Assert.AreEqual(1, calls, "Source selection and production IL must not share caches.");
        }

        [Test]
        public void TrackerILFailureCannotFallBackToCompleteSource() {
            var reader = ViewSafetyReader(() => throw new InvalidOperationException("IL view analysis failed"));
            var export = reader.GetType().GetMethod("SelectForExport");
            var error = Assert.Throws<TargetInvocationException>(() => export.Invoke(reader, new object[] { typeof(ExplicitModule), "ApplyState", true, null }));
            Assert.AreEqual("IL view analysis failed", error.GetBaseException().Message);
        }

        [TestCase(typeof(UnknownDispatchModule), "Incomplete")]
        [TestCase(typeof(PrivateComponentModule), "Incomplete")]
        [TestCase(typeof(NamedOnlyModule), "Missing")]
        public void ViewFallbackIsRunLocalAndNeverExposesPartialSource(Type owner, string expectedStatus) {
            var calls = 0;
            var reader = ViewSafetyReader(() => ++calls);
            Assert.AreEqual(expectedStatus, ReadView(reader, owner, "ApplyState", out _, out var source));
            Assert.IsNull(source);
            SelectView(reader, owner);
            SelectView(reader, owner);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ViewCallbackPhasesHaveIndependentSelectionCaches() {
            var reader = ViewSafetyReader(() => Assert.Fail("Both phases have complete source."));
            var owner = typeof(DualPhaseModule);
            CollectionAssert.AreEqual(new[] { typeof(ComponentA).FullName + " / ReadOnly / False" }, ViewRecords(SelectView(reader, owner)));
            CollectionAssert.AreEqual(new[] { typeof(ComponentB).FullName + " / ReadWrite / False" }, ViewRecords(SelectView(reader, owner, "ApplyStateParallel")));
        }

        [Test]
        public void InaccessibleViewComponentIsAnExplicitGapNotACorruptCompleteCatalog() {
            var reader = ViewSafetyReader(() => Assert.Fail("Reading coverage must not evaluate IL."));
            Assert.AreEqual("Incomplete", ReadView(reader, typeof(PrivateComponentModule), "ApplyState", out var rows, out var source));
            Assert.IsNull(source);
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tInaccessibleSafetyComponent", StringComparison.Ordinal)));
            Assert.IsFalse(rows.Any(row => row.StartsWith("A\t", StringComparison.Ordinal)));
        }

        [Test]
        public void ViewAndModuleRolesOnTheSameTypeDoNotShareSafetySelection() {
            var reader = ViewSafetyReader(() => Assert.Fail("Both roles have complete source."));
            var owner = typeof(DualRoleView);
            CollectionAssert.AreEqual(new[] { typeof(ComponentA).FullName + " / ReadOnly / False" }, ViewRecords(SelectView(reader, owner)));
            var module = reader.GetType().GetMethod("SelectModule").Invoke(reader, new object[] { owner, "ApplyState" });
            CollectionAssert.AreEqual(new[] { typeof(ComponentB).FullName + " / ReadWrite / False" }, ViewRecords(module));
            CollectionAssert.AreEqual(new[] { typeof(ComponentA).FullName + " / ReadOnly / False" }, ViewRecords(SelectView(reader, owner)));
        }

        [Test]
        public void LegacyFallbackAlsoDistinguishesViewAndModuleRoles() {
            var owner = typeof(DualRoleView);
            var view = (MethodInfo)ViewSafetyReaderType.GetMethod("GetCallback").Invoke(null, new object[] { owner, "ApplyState" });
            var module = (MethodInfo)ViewSafetyReaderType.GetMethod("GetModuleCallback").Invoke(null, new object[] { owner, "ApplyState" });
            Assert.IsFalse(view.IsPrivate);
            Assert.IsTrue(module.IsPrivate);
            Assert.AreNotEqual(view, module);
        }

        [Test]
        public void InvalidViewCatalogCannotSilentlyFallBackToIL() {
            var owner = typeof(ExplicitModule);
            var baseline = ViewSafetyReader(() => Assert.Fail("No IL expected"));
            Assert.AreEqual("Complete", ReadView(baseline, owner, "ApplyState", out var rows, out _));
            var dependency = rows.Single(row => row.StartsWith("D\t", StringComparison.Ordinal));
            foreach (var mutation in new[] {
                         (string[])null, new[] { rows[0] },
                         rows.Select((row, index) => index == 2 ? "00" : row).ToArray(),
                         rows.Concat(new[] { "G\tunreported coverage gap" }).ToArray(),
                         rows.Where(row => !row.StartsWith("A\t", StringComparison.Ordinal)).ToArray(),
                         rows.Select(row => row == dependency ? row.Substring(0, row.Length - 3) + "9\t0" : row).ToArray(),
                         rows.Concat(new[] { dependency }).ToArray(),
                     }) {
                var reader = ViewSafetyReader(() => Assert.Fail("Invalid metadata must stop export."));
                Assert.AreEqual("Complete", ReadView(reader, owner, "ApplyState", out _, out _));
                var catalogs = (System.Collections.IDictionary)reader.GetType().GetField("catalogs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reader);
                ((System.Collections.IDictionary)catalogs[owner.Assembly])["ApplyState\n" + owner.FullName] = mutation;
                Assert.AreEqual("Invalid", ReadView(reader, owner, "ApplyState", out _, out var source));
                Assert.IsNull(source);
                var error = Assert.Throws<TargetInvocationException>(() => SelectView(reader, owner));
                Assert.IsInstanceOf<InvalidOperationException>(error.GetBaseException());
            }
        }

        [Test]
        public void ViewComparisonDoesNotVetoCompleteSourceSelection() {
            var reader = ViewSafetyReader(() => Assert.Fail("Comparison must not become the production baseline."));
            var owner = typeof(ExplicitModule);
            var selected = SelectView(reader, owner);
            var empty = Activator.CreateInstance(selected.GetType());
            Assert.AreEqual(0, reader.GetType().GetMethod("Compare").Invoke(reader, new[] { (object)owner, "ApplyState", empty, null }));
            CollectionAssert.AreEqual(ViewRecords(selected), ViewRecords(SelectView(reader, owner)));
        }

        [TestCase(typeof(ExplicitModule), typeof(ExplicitModule), true)]
        [TestCase(typeof(OverrideModule), typeof(OverrideModule), false)]
        [TestCase(typeof(HiddenModule), typeof(GenericModule<ComponentA>), false)]
        [TestCase(typeof(OverloadedModule), typeof(GenericModule<ComponentB>), false)]
        [TestCase(typeof(ViewWithHiddenSlot), typeof(ViewWithOverload), false)]
        public void LegacyViewFallbackResolvesTheSameRuntimeCallbackSlot(Type owner, Type declaringType, bool explicitImplementation) {
            var method = (MethodInfo)ViewSafetyReaderType.GetMethod("GetCallback").Invoke(null, new object[] { owner, "ApplyState" });
            Assert.AreEqual(declaringType, method.DeclaringType);
            Assert.AreEqual(explicitImplementation, method.IsPrivate);
            Assert.AreEqual(typeof(ViewData).MakeByRefType(), method.GetParameters().Single().ParameterType);
        }

        [Test]
        public void SameNamedModuleMethodIsNotARuntimeCallback() {
            Assert.IsNull(ViewSafetyReaderType.GetMethod("GetCallback").Invoke(null, new object[] { typeof(NamedOnlyModule), "ApplyState" }));
        }
    }
}
