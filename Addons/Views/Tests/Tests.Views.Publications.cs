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
        private static AssemblyMetadataAttribute[] Metadata(string profile) => Assembly.Load("ME.BECS.Gen." + profile)
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
        private static string[] Rows(string profile) => Metadata(profile).Where(item => item.Key == "ME.BECS.TypeInput.v1" &&
            item.Value.StartsWith(profile.ToLowerInvariant() + "\t", StringComparison.Ordinal)).Select(item => item.Value.Substring(profile.Length + 1)).ToArray();
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call || instruction.OpCode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
        private static (string Kind, string[] Payload)[] Selection(string profile) => Metadata(profile)
            .Where(item => item.Key == "ME.BECS.ViewTrackerSelection.v1").Select(item => item.Value.Split('\t'))
            .Select(row => (row[0], Decode(row[2]).Split('\n'))).ToArray();

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
            var aggregate = Assembly.Load("ME.BECS.Gen." + profile);
            foreach (var pair in new[] { ("ViewTrackerInputs", "InitializeTrackers"), ("ViewTypeInputs", "RegisterInstalledTypes") }) {
                var facade = aggregate.GetType("ME.BECS.SourceGenerated." + pair.Item1, true);
                CollectionAssert.AreEqual(new[] { "Initialize" }, facade.GetMethods(Static | BindingFlags.Public | BindingFlags.DeclaredOnly).Select(method => method.Name));
                CollectionAssert.AreEqual(new[] { typeof(BootstrapViews).GetMethod(pair.Item2) }, Calls(facade.GetMethod("Initialize")));
            }
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
