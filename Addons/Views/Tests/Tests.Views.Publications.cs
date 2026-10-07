using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Views.Tests {
    public class Tests_Views_Publications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Registry => typeof(BootstrapViews).Assembly.GetType("ME.BECS.Views.ViewsBootstrapRegistry", true);
        private static void On(object registry, string method, params object[] args) => Registry.GetMethod(method, Instance).Invoke(registry, args);
        private static object NewRegistry() => Activator.CreateInstance(Registry, true);
        private static void Missing(TestDelegate action) => Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(action).InnerException);
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorViewsFragmentFormat", true);
        private static object Call(string method, params object[] args) => Format.GetMethod(method, Static).Invoke(null, args);
        private static T Field<T>(object value, string field) => (T)value.GetType().GetField(field, Instance).GetValue(value);
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        internal static Assembly SelectionAssembly(string profile) => (Assembly)Assembly.Load("ME.BECS.Editor")
            .GetType("ME.BECS.Editor.SourceGeneratorViewSelectionCatalog", true).GetMethod("GetAssembly").Invoke(null, new object[] { profile == "Editor" });
        private static AssemblyMetadataAttribute[] Metadata(string profile) => SelectionAssembly(profile)
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
        internal static string[] Rows(string profile) => (string[])Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputCatalog", true)
            .GetMethod("GetRows", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { profile == "Editor" });
        internal static void AssertPhases(string profile) {
            var rows = Rows(profile).Select(row => row.Split('\t')).ToArray();
            var owner = Assembly.Load(Decode(rows.Single(row => row[0] == "bootstrap-registration-owner")[3]));
            Assert.IsFalse(owner.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
            var phases = owner.GetType("ME.BECS.SourceGenerated.BootstrapPhaseInputs", true);
            var feeders = rows.Where(row => row[0] == "bootstrap-feeder").OrderBy(row => int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            foreach (var item in new[] { (Field: "Initializers", Column: 3, Kind: "views", Method: "InitializeTrackers"),
                (Field: "Registrations", Column: 4, Kind: "view-types", Method: "RegisterInstalledTypes") }) {
                var callbacks = (Action<bool>[])phases.GetField(item.Field, Static).GetValue(null);
                Assert.AreEqual(feeders.Length, callbacks.Length);
                var indices = Enumerable.Range(0, feeders.Length).Where(index => feeders[index][item.Column] == item.Kind).ToArray();
                Assert.AreEqual(1, indices.Length);
                Assert.AreEqual(typeof(BootstrapViews).GetMethod(item.Method), callbacks[indices[0]].Method);
            }
        }
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call || instruction.OpCode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
        private static (string Kind, string[] Payload)[] Selection(string profile) => Metadata(profile)
            .Where(item => item.Key == "ME.BECS.ViewTrackerSelection.v1").Select(item => item.Value.Split('\t'))
            .Select(row => (row[0], Decode(row[2]).Split('\n'))).ToArray();

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void DependencySelectionHasItsOwnCompilerOwnerAndExactRoleUnion(string profile) {
            var owner = SelectionAssembly(profile);
            Assert.IsFalse(owner.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
            Assert.IsFalse(owner.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
            var format = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorViewSelectionFragmentFormat", true);
            object Invoke(string method, params object[] args) => format.GetMethod(method, Static).Invoke(null, args);
            var document = ((Array)Invoke("Documents", Rows(profile), profile == "Editor")).GetValue(0);
            Assert.AreEqual(owner.GetName().Name, Field<string>(document, "Owner"));
            var content = (string)Invoke("Serialize", document);
            var parsed = new object[] { content, null };
            Assert.IsTrue((bool)Invoke("TryParse", parsed));
            Assert.AreEqual(content, Invoke("Serialize", parsed[1]));
            var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
            var receipt = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { document, content });
            Assert.AreEqual(1, Metadata(profile).Count(attribute => attribute.Key == "ME.BECS.ViewSelectionFragment.v1" && attribute.Value == receipt));
            var root = owner.GetType("ME.BECS.SourceGenerated.ViewSelectionProfile_" + profile, true);
            var publish = root.GetMethod("Publish", Static);
            Assert.IsTrue(publish.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
            if (profile == "Editor") Assert.IsTrue(publish.IsDefined(typeof(UnityEditor.InitializeOnLoadMethodAttribute), false));
            else {
                Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                Assert.AreEqual(1, owner.GetCustomAttributes(false).Count(attribute => attribute.GetType().FullName == "UnityEngine.Scripting.AlwaysLinkAssemblyAttribute"));
            }
            var selection = owner.GetType("ME.BECS.SourceGenerated.BootstrapViewsSelection", true);
            CollectionAssert.AreEqual(new[] { selection.GetMethod("Publish") }, Calls(publish));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapViews).GetMethod("ExpectPlan") }, Calls(selection.GetMethod("Publish")));
            var selected = Selection(profile);
            var components = selected.Single(row => row.Kind == "view-tracker").Payload.Skip(2).Where(value => value.Length != 0).ToArray();
            var expected = selected.Where(row => row.Kind != "view-tracker").GroupBy(row => row.Payload[0], StringComparer.Ordinal)
                .Select(group => group.SelectMany(row => row.Payload.Skip(1)).Where(value => value.Length != 0).Distinct(StringComparer.Ordinal)
                    .Select(component => Array.IndexOf(components, component)).ToArray()).ToArray();
            var actual = (int[][])selection.GetField("Dependencies", Static).GetValue(null);
            Assert.AreEqual(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; ++i) CollectionAssert.AreEqual(expected[i], actual[i]);
            // Unrelated project input changes must not alter this feature's publication.
            var shuffled = Rows(profile).Reverse().Concat(new[] { "unrelated\t0\tdjE=" }).ToArray();
            Assert.AreEqual(content, Invoke("Serialize", ((Array)Invoke("Documents", shuffled, profile == "Editor")).GetValue(0)));
            document.GetType().GetField("Entries", Instance).SetValue(document, Array.Empty<KeyValuePair<int, string>>());
            Assert.IsTrue((bool)Invoke("TryParse", Invoke("Serialize", document), null));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PublicationOrderAndCallerMutationsCannotChangePhaseOrDependencyOrder(bool reverse) {
            var registry = NewRegistry();
            var calls = new System.Collections.Generic.List<string>();
            var actions = new ViewRegistrationCallback[] {
                info => { calls.Add("component0"); return 10u; }, info => { calls.Add("component1"); return 20u; },
                info => { calls.Add("tracker:" + info.tracker.Get(0) + "," + info.tracker.Get(1)); info.tracker.Dispose(); return 0u; },
                info => { calls.Add("type"); return 0u; },
            };
            foreach (var owner in reverse ? new[] { 1, 0 } : new[] { 0, 1 }) {
                var ordinals = owner == 0 ? new[] { 0, 3 } : new[] { 1, 2 };
                On(registry, "Install", "plan", "owner" + owner, 4, ordinals, ordinals.Select(index => actions[index]).ToArray());
            }
            Missing(() => On(registry, "RequireComplete"));
            var dependencies = new[] { new[] { 1, 0 } };
            On(registry, "Expect", "plan", 2, 1, 1, dependencies);
            dependencies[0][0] = 0;
            On(registry, "Expect", "plan", 2, 1, 1, new[] { new[] { 1, 0 } });
            Assert.IsEmpty(calls);
            try {
                On(registry, "InitializeTrackers");
                CollectionAssert.AreEqual(new[] { "component0", "component1", "tracker:20,10" }, calls);
                var register = (WorldStaticCallbacks.CallbackDelegate<ViewsModuleData>)Delegate.CreateDelegate(
                    typeof(WorldStaticCallbacks.CallbackDelegate<ViewsModuleData>), registry, Registry.GetMethod("RegisterTypes", Instance));
                var module = default(ViewsModuleData);
                register(ref module);
                CollectionAssert.AreEqual(new[] { "component0", "component1", "tracker:20,10", "type" }, calls);
            } finally { ME.BECS.Tests.AllTests.Start(); }
        }

        [Test]
        public void MissingOwnersAndChangedDependenciesAreRejectedBeforeReset() {
            var registry = NewRegistry();
            On(registry, "Expect", "plan", 2, 0, 1, new[] { new[] { 0 } });
            Missing(() => On(registry, "InitializeTrackers"));
            Missing(() => On(registry, "Expect", "plan", 2, 0, 1, new[] { new[] { 1 } }));
            Missing(() => On(registry, "RequireComplete"));
            Assert.IsInstanceOf<ArgumentException>(Assert.Throws<TargetInvocationException>(() =>
                On(NewRegistry(), "Expect", "bad", 1, 0, 1, new[] { new[] { 1 } })).InnerException);
            registry = NewRegistry();
            On(registry, "Expect", "empty", 0, 0, 0, Array.Empty<int[]>());
            Assert.DoesNotThrow(() => On(registry, "RequireComplete"));
            Assert.IsFalse(typeof(BootstrapRuntime).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name == "ME.BECS.Views"));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void OwnersMatchAllSelectedPhasesAndAggregateContainsOnlyUntypedFacades(string profile) {
            var rows = Rows(profile);
            CollectionAssert.Contains(rows, "views-publication-schema\t0\tdjE=");
            var selected = Selection(profile);
            var expected = selected.Single(row => row.Kind == "view-tracker").Payload.Skip(2).Where(value => value.Length != 0)
                .Select(type => (string)Call("EntryValue", "Component", type))
                .Concat(selected.Where(row => row.Kind != "view-tracker").GroupBy(row => row.Payload[0], StringComparer.Ordinal)
                    .Select(group => (string)Call("EntryValue", group.First().Kind == "view-tracker-module" ? "ModuleTracker" : "ViewTracker", group.Key)))
                .Concat(rows.Where(row => row.StartsWith("view-type\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                    .OrderBy(row => int.Parse(row[1])).Select(row => (string)Call("EntryValue", "ViewType", Decode(row[2])))).ToArray();
            var documents = ((Array)Call("Documents", rows, profile == "Editor")).Cast<object>().ToArray();
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var doc in documents) {
                var serialized = (string)Call("Serialize", doc);
                var parsed = new object[] { serialized, null };
                Assert.IsTrue((bool)Call("TryParse", parsed));
                Assert.AreEqual(serialized, Call("Serialize", parsed[1]));
                var owner = Field<string>(doc, "Owner");
                var assembly = Assembly.Load(owner);
                Assert.IsFalse(owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
                var metadata = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { doc, serialized });
                Assert.AreEqual(1, assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Count(item => item.Key == "ME.BECS.ViewsFragment.v1" && item.Value == metadata));
                var publisher = assembly.GetType("ME.BECS.SourceGenerated.ViewsFragment_" + profile, true);
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapViews).GetMethod("InstallFragment") }, Calls(publish));
                Assert.IsTrue(publish.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                if (profile == "Editor") Assert.IsTrue(publish.IsDefined(typeof(UnityEditor.InitializeOnLoadMethodAttribute), false));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var entries = Field<KeyValuePair<int, string>[]>(doc, "Entries");
                var callbacks = (ViewRegistrationCallback[])publisher.GetField("Callbacks", Static).GetValue(null);
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var i = 0; i < entries.Length; ++i) {
                    Assert.IsTrue(seen.Add(entries[i].Key));
                    Assert.AreEqual(expected[entries[i].Key], entries[i].Value);
                    Assert.AreEqual(assembly, callbacks[i].Method.DeclaringType.Assembly);
                    Assert.IsTrue(callbacks[i].Method.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                }
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, expected.Length), seen);
            AssertPhases(profile);
        }

        private static uint ComponentIndex<T>() where T : unmanaged, IComponentBase => StaticTypes<T>.trackerIndex;
        private static uint Index(string identity) => (uint)typeof(Tests_Views_Publications).GetMethod(nameof(ComponentIndex), Static)
            .MakeGenericMethod(Type.GetType(identity, true)).Invoke(null, null);

        [Test]
        public void RepeatedBootstrapKeepsDualRoleDependenciesAndActualTypeFlags() {
            var selected = Selection("Editor");
            var views = Metadata("Editor").Single(item => item.Key == "ME.BECS.ViewTypeInputs.v1").Value.Split('\n').Skip(2)
                .Select(row => row.Split('\t')).Where(row => row[0] == "T").ToArray();
            System.Collections.Generic.Dictionary<Type, uint> previous = null;
            for (var pass = 0; pass < 2; ++pass) {
                ME.BECS.Tests.AllTests.Start();
                var groups = selected.Where(row => row.Kind != "view-tracker").GroupBy(row => row.Payload[0], StringComparer.Ordinal).ToArray();
                Assert.AreEqual(groups.Length, ViewsTracker.typeToIndex.Count);
                foreach (var group in groups) {
                    var type = Type.GetType(group.Key, true);
                    var id = ViewsTracker.typeToIndex[type];
                    if (previous != null) Assert.AreEqual(previous[type], id);
                    var expected = group.SelectMany(row => row.Payload.Skip(1)).Where(value => value.Length != 0).Distinct(StringComparer.Ordinal).Select(Index).ToArray();
                    var info = ViewsTracker.info[id];
                    Assert.AreEqual(expected.Length, info.tracker.Length);
                    for (var i = 0; i < expected.Length; ++i) Assert.AreEqual(expected[i], info.tracker.Get((uint)i));
                }
                var module = default(ViewsModuleData);
                WorldStaticCallbacks.RaiseCallback(ref module);
                foreach (var row in views) {
                    var type = Type.GetType(row[2], true);
                    Assert.IsTrue(ViewsTypeInfo.types.TryGetValue(type, out var info));
                    Assert.AreEqual((TypeFlags)byte.Parse(row[3]), info.flags);
                }
                previous = new System.Collections.Generic.Dictionary<Type, uint>(ViewsTracker.typeToIndex);
            }
        }

        [Test]
        public void ExistingTrackerSelectionContractsRemainValid() {
            var tests = new Tests_Views_SourceSafety();
            foreach (var profile in new[] { "Editor", "Runtime" }) {
                tests.TrackerInputsTransportCanonicalILSnapshots(profile);
                tests.CompilerTrackerCatalogIsTheCanonicalUnionOfSelectedRoles(profile);
            }
            tests.CompilerTrackingAppliesExplicitOptInAfterCallbackExclusions(typeof(Tests_Views_SourceSafety.CompilerIgnoredRead), false);
            tests.CompilerTrackingAppliesExplicitOptInAfterCallbackExclusions(typeof(Tests_Views_SourceSafety.CompilerExplicitTracking), true);
        }
    }
}
