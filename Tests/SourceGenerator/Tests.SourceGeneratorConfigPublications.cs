using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorConfigPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorConfigFragmentFormat", true);
        private static Type Registry => typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapConfigRegistry", true);
        private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, Static).Invoke(null, args);
        private static void On(object registry, string name, params object[] args) => Registry.GetMethod(name, Instance).Invoke(registry, args);
        private static T Field<T>(object doc, string name) => (T)doc.GetType().GetField(name, Instance).GetValue(doc);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string Row(int id, string phase, string component, string owner) => "config-registration-owner\t" + id + "\t" + Encode(phase + "|" + Encode(component)) + "\t" + Encode(owner);
        private static object[] Documents(IEnumerable<string> rows, bool editor) => ((Array)Call(Format, "Documents", rows, editor)).Cast<object>().ToArray();
        private static string Serialize(object document) => (string)Call(Format, "Serialize", document);
        private static string[] Rows(Assembly assembly, string profile) => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(item => item.Key == "ME.BECS.TypeInput.v1" && item.Value.StartsWith(profile.ToLowerInvariant() + "\t", StringComparison.Ordinal))
            .Select(item => item.Value.Substring(profile.Length + 1)).ToArray();
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt).Select(instruction => (MethodInfo)instruction.Operand).ToArray();
        private static void Incomplete(TestDelegate action) => Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(action).InnerException);

        internal static Type Catalog(Assembly selection, string phase, Type component, out string key, string[][] records = null) {
            var profile = selection.GetName().Name.EndsWith("Editor", StringComparison.Ordinal) ? "Editor" : "Runtime";
            var rows = records == null ? Rows(selection, profile).Select(value => value.Split('\t')) :
                records.Where(value => value[0] == profile.ToLowerInvariant()).Select(value => value.Skip(1).ToArray());
            var row = rows.Where(value => value[0] == "config-registration-owner")
                .Single(value => Decode(value[2]) == phase + "|" + Encode(component.AssemblyQualifiedName));
            key = row[1];
            return Assembly.Load(Decode(row[3])).GetType("ME.BECS.SourceGenerated.ConfigFragment_" + profile, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SeparatePhasesFollowGlobalOrderAndDoNotRunOnPublication(bool reverse) {
            var registry = Activator.CreateInstance(Registry, true);
            var calls = new System.Collections.Generic.List<int>();
            var actions = Enumerable.Range(0, 6).Select(index => (Action)(() => calls.Add(index))).ToArray();
            foreach (var owner in reverse ? new[] { 1, 0 } : new[] { 0, 1 }) {
                var indices = owner == 0 ? new[] { 0, 3, 4 } : new[] { 1, 2, 5 };
                On(registry, "Install", "plan", "owner" + owner, 6, indices, indices.Select(index => actions[index]).ToArray());
            }
            Incomplete(() => On(registry, "ExecuteCounts")); // An owner is not the authoritative phase selection.
            On(registry, "Expect", "plan", 2, 2, 2);
            On(registry, "Expect", "plan", 2, 2, 2);
            Assert.IsEmpty(calls);
            On(registry, "ExecuteCounts");
            CollectionAssert.AreEqual(new[] { 0, 1 }, calls);
            On(registry, "ExecuteMasks");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, calls);
            On(registry, "ExecuteCollections");
            On(registry, "ExecuteCounts");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 0, 1 }, calls);
        }

        [Test]
        public void MissingAndConflictingPhasesFailBeforeAnyCallbackAndEmptySelectionWorks() {
            var registry = Activator.CreateInstance(Registry, true);
            var calls = 0;
            On(registry, "Expect", "plan", 1, 1, 1);
            On(registry, "Install", "plan", "owner", 3, new[] { 0 }, new Action[] { () => ++calls });
            foreach (var phase in new[] { "Counts", "Masks", "Collections" }) Incomplete(() => On(registry, "Execute" + phase));
            Assert.AreEqual(0, calls);
            registry = Activator.CreateInstance(Registry, true);
            On(registry, "Expect", "empty", 0, 0, 0);
            foreach (var phase in new[] { "Counts", "Masks", "Collections" }) Assert.DoesNotThrow(() => On(registry, "Execute" + phase));
            registry = Activator.CreateInstance(Registry, true);
            On(registry, "Expect", "plan", 1, 1, 1);
            Incomplete(() => On(registry, "Expect", "plan", 0, 2, 1)); // Same total, different phase boundaries.
            Incomplete(() => On(registry, "RequireComplete"));
            CollectionAssert.Contains(Calls(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan")), Registry.GetMethod("RequireComplete", Instance));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DocumentsKeepPhaseAndTypeOrderAndSupportRetirement(bool editor) {
            var rows = new[] { Row(0, "Counts", "A, Types", "Z"), Row(1, "Masks", "B, Types", "A"), Row(2, "Collections", "A, Types", "Z") };
            var docs = Documents(rows, editor);
            CollectionAssert.AreEqual(docs.Select(Serialize), Documents(rows.Reverse(), editor).Select(Serialize));
            CollectionAssert.AreEqual(docs.Select(Serialize), Documents(rows.Concat(new[] { "graph-input-snapshot\t0\tchanged" }), editor).Select(Serialize));
            CollectionAssert.AreEqual(new[] { 0, 2 }, Field<KeyValuePair<int, string>[]>(docs[1], "Entries").Select(entry => entry.Key));
            var parse = new object[] { Serialize(docs[1]), null };
            Assert.IsTrue((bool)Call(Format, "TryParse", parse));
            Assert.AreEqual(Serialize(docs[1]), Serialize(parse[1]));
            docs[1].GetType().GetField("Entries", Instance).SetValue(docs[1], Array.Empty<KeyValuePair<int, string>>());
            parse = new object[] { Serialize(docs[1]), null };
            Assert.IsTrue((bool)Call(Format, "TryParse", parse));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(2, "Masks", "B, Types", "A") }, editor));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(1, "Counts", "A, Types", "A") }, editor));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { Row(0, "Masks", "B, Types", "A"), Row(1, "Counts", "A, Types", "Z") }, editor));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { Row(0, "Masks", "A, Types", "ME.BECS.Gen.Editor") }, editor));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void TypedRegistrationsAndBurstBodiesLiveInEligibleOwners(string profile) {
            var selection = Assembly.Load("ME.BECS.Gen." + profile);
            var rows = Rows(selection, profile);
            CollectionAssert.Contains(rows, "config-publication-schema\t0\tdjE=");
            var expected = rows.Where(row => row.StartsWith("config-collection-callback\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1])).Select(row => "Counts|" + row[2])
                .Concat(rows.Where(row => row.StartsWith("config-mask-registration\t", StringComparison.Ordinal)).Select(row => row.Split('\t')).OrderBy(row => int.Parse(row[1])).Select(row => "Masks|" + row[2]))
                .Concat(rows.Where(row => row.StartsWith("config-collection-callback\t", StringComparison.Ordinal)).Select(row => row.Split('\t')).OrderBy(row => int.Parse(row[1])).Select(row => "Collections|" + row[2])).ToArray();
            var publishSelection = selection.GetType("ME.BECS.SourceGenerated.BootstrapConfigSelection", true);
            Assert.IsEmpty(publishSelection.GetFields(Static));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("ExpectConfigPlan") }, Calls(publishSelection.GetMethod("Publish")));
            foreach (var pair in new[] { (Facade: "ConfigCollectionCounts", Phase: "Counts"), (Facade: "ConfigMaskInputs", Phase: "Masks"), (Facade: "ConfigCollectionsInputs", Phase: "Collections") }) {
                var facade = selection.GetType("ME.BECS.SourceGenerated." + pair.Facade, true);
                CollectionAssert.AreEqual(new[] { "Initialize" }, facade.GetMethods(Static | BindingFlags.Public | BindingFlags.DeclaredOnly).Select(method => method.Name));
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("RegisterInstalledConfig" + pair.Phase) }, Calls(facade.GetMethod("Initialize")));
            }
            var seen = new System.Collections.Generic.HashSet<int>();
            var bridge = Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorBridge", true);
            using var scope = (IDisposable)Call(bridge, "BeginLookupScope");
            foreach (var document in Documents(rows, profile == "Editor")) {
                var owner = Field<string>(document, "Owner");
                var assembly = Assembly.Load(owner);
                Assert.AreNotEqual(selection, assembly);
                var publisher = assembly.GetType("ME.BECS.SourceGenerated.ConfigFragment_" + profile, true);
                var bodies = assembly.GetType("ME.BECS.SourceGenerated.ConfigCallbacks_" + profile, true);
                Assert.IsNull(bodies.TypeInitializer);
                Assert.IsEmpty(bodies.GetFields(Static | BindingFlags.Public));
                Assert.IsTrue(Attribute.IsDefined(bodies, typeof(Unity.Burst.BurstCompileAttribute)));
                Assert.IsFalse(Attribute.IsDefined(publisher, typeof(Unity.Burst.BurstCompileAttribute)));
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallConfigFragment") }, Calls(publish));
                Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEngine.Scripting.PreserveAttribute)));
                if (profile == "Editor") Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var entries = Field<KeyValuePair<int, string>[]>(document, "Entries");
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var i = 0; i < entries.Length; ++i) {
                    var entry = entries[i];
                    Assert.IsTrue(seen.Add(entry.Key));
                    Assert.AreEqual(expected[entry.Key], entry.Value);
                    var parts = entry.Value.Split('|');
                    var component = Type.GetType(Decode(parts[1]), true);
                    var register = Calls(callbacks[i].Method).Single();
                    if (parts[0] == "Counts") {
                        Assert.AreEqual("SetCollectionsCount", register.Name);
                        Assert.AreEqual(component, register.DeclaringType.GetGenericArguments().Single());
                    } else {
                        Assert.AreEqual(typeof(WorldStaticCallbacks), register.DeclaringType);
                        Assert.AreEqual(parts[0] == "Masks" ? "RegisterConfigComponentMaskCallback" : "RegisterConfigComponentCallback", register.Name);
                        Assert.AreEqual(component, register.GetGenericArguments().Single());
                        var callback = bodies.GetMethod("Apply_" + entry.Key);
                        Assert.IsFalse(callback.ContainsGenericParameters);
                        Assert.IsTrue(Attribute.IsDefined(callback, typeof(Unity.Burst.BurstCompileAttribute)));
                        Assert.IsTrue(Attribute.IsDefined(callback, typeof(UnityEngine.Scripting.PreserveAttribute)));
                        Assert.AreEqual(parts[0] == "Masks" ? typeof(UnsafeEntityConfig.MethodMaskCallerDelegate) : typeof(UnsafeEntityConfig.MethodCallerDelegate),
                            callback.GetCustomAttributesData().Single(attribute => attribute.AttributeType == typeof(AOT.MonoPInvokeCallbackAttribute)).ConstructorArguments.Single().Value);
                    }
                    var args = parts[0] == "Masks" ? new object[] { component, profile == "Editor", null, null } :
                        new object[] { component, parts[0] == "Counts", profile == "Editor", null, null };
                    Assert.IsTrue((bool)Call(bridge, parts[0] == "Masks" ? "TryGetConfigMaskRegistration" : "TryGetConfigCollectionsRegistration", args), component.FullName);
                }
                Assert.IsTrue(UnityEditor.Compilation.CompilationPipeline.GetAssemblies(profile == "Editor" ? UnityEditor.Compilation.AssembliesType.Editor : UnityEditor.Compilation.AssembliesType.Player)
                    .Single(item => item.name == owner).compilerOptions.AllowUnsafeCode);
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, expected.Length), seen);
            Assert.IsNotEmpty(expected);
            var validation = new object[] { null };
            Assert.IsTrue((bool)Call(Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true), "ValidateConfigs", validation), (string)validation[0]);
        }

        [Test]
        public void CompilerCatalogsKeepSerializedMaskAndCollectionFieldOrder() {
            var tests = new Tests_SourceGeneratorContracts();
            tests.CompilerConfigMasksRetainSerializedBitPositions();
            tests.CompilerConfigCollectionFieldsRetainReflectionOrder();
            tests.ConfigCollectionCountsAreNoLongerExportedByEditor();
        }

        [Test]
        public void RepeatedBootstrapRestoresCompilerCollectionCounts() {
            for (var repeat = 0; repeat < 2; ++repeat) {
                AllTests.Start();
                try {
                    Assert.AreEqual(1u, StaticTypes.collectionsCount.Get(StaticTypes<Tests_EntityConfig.TestArrayComponent>.typeId));
                    Assert.AreEqual(1u, StaticTypes.collectionsCount.Get(StaticTypes<Tests_EntityConfig.TestArrayComponentShared>.typeId));
                    Assert.AreEqual(1u, StaticTypes.collectionsCount.Get(StaticTypes<Tests_EntityConfig.TestArrayComponentStatic>.typeId));
                    Assert.AreEqual(1u, StaticTypes.collectionsCount.Get(StaticTypes<Tests_EntityConfig.TestListComponent>.typeId));
                    Assert.AreEqual(0u, StaticTypes.collectionsCount.Get(StaticTypes<Tests_EntityConfig.TestConfigMaskComponent>.typeId));
                    StaticTypes.collectionsCount.Get(StaticTypes<Tests_EntityConfig.TestListComponent>.typeId) = 0u;
                } finally { AllTests.Dispose(); }
            }
        }
    }
}
