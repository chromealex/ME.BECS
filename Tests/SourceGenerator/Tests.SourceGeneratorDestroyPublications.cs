using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorDestroyPublications {
        public struct StorageProbe : IComponentDestroy {
            public int value;
            void IComponentDestroy.Destroy(in Ent ent) {
                this.value += 7;
                TestDestroyData.value.Data = this.value;
            }
            public void Destroy(in Ent ent) { this.value = -100; }
        }

        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorDestroyFragmentFormat", true);
        private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, Static).Invoke(null, args);
        private static T Field<T>(object doc, string name) => (T)doc.GetType().GetField(name, Instance).GetValue(doc);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string Row(int ordinal, string component, string owner) => "destroy-registration-owner\t" + ordinal + "\t" + Encode(component) + "\t" + Encode(owner);
        private static string[] Rows(string profile) => Tests_SourceGeneratorInputCatalog.Rows(profile == "Editor");
        private static object[] Documents(IEnumerable<string> rows, bool editor) => ((Array)Call(Format, "Documents", rows, editor)).Cast<object>().ToArray();
        private static string Serialize(object document) => (string)Call(Format, "Serialize", document);
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
        private static MethodInfo Callback(Type component, string profile) {
            var row = Rows(profile).Where(value => value.StartsWith("destroy-registration-owner\t", StringComparison.Ordinal))
                .Select(value => value.Split('\t')).Single(value => Decode(value[2]) == component.AssemblyQualifiedName);
            return Assembly.Load(Decode(row[3])).GetType("ME.BECS.SourceGenerated.DestroyCallbacks_" + profile, true).GetMethod("Destroy_" + row[1]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelectionIsOrderedAndRetiredOwnersHaveNoEntries(bool editor) {
            var rows = new[] { Row(0, "A, Types", "Z"), Row(1, "B, Types", "A"), Row(2, "C, Types", "Z") };
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
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(2, "B, Types", "A") }, editor));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(1, "A, Types", "A") }, editor));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { Row(0, "A, Types", "ME.BECS.Gen.Editor") }, editor));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnsafeOwnerUsesTheTargetProfilesCompilerOptions(bool editor) {
            var planner = Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorRegistrationOwners", true);
            var candidate = planner.GetNestedType("Candidate", BindingFlags.NonPublic);
            var references = new[] { "Definition", "ME.BECS" };
            object Host(string name, bool editorUnsafe, bool runtimeUnsafe) => Activator.CreateInstance(candidate, Instance, null,
                new object[] { name, false, references, references, editorUnsafe, runtimeUnsafe }, null);
            var hosts = Array.CreateInstance(candidate, 2);
            hosts.SetValue(Host("Definition", true, false), 0);
            hosts.SetValue(Host("Consumer", true, true), 1);
            Assert.AreEqual(editor ? "Definition" : "Consumer", Call(planner, "ChooseUnsafe", "Definition", references, editor, hosts));
            hosts.SetValue(Host("Consumer", false, false), 1);
            if (!editor) Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() =>
                Call(planner, "ChooseUnsafe", "Definition", references, editor, hosts)).InnerException);
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void SelectedBurstBodiesAndRegistrationLiveOutsideTheAggregate(string profile) {
            // Startup selection lives in the independent composition publisher;
            // typed callback coverage does not require an aggregate assembly.
            var rows = Rows(profile);
            var selected = rows.Where(row => row.StartsWith("destroy-registration\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1])).Select(row => Type.GetType(Decode(row[2]), true)).ToArray();
            var seen = new System.Collections.Generic.HashSet<int>();
            var bridge = Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorBridge", true);
            foreach (var document in Documents(rows, profile == "Editor")) {
                var owner = Field<string>(document, "Owner");
                var assembly = Assembly.Load(owner);
                Assert.IsFalse(assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                var publisher = assembly.GetType("ME.BECS.SourceGenerated.DestroyFragment_" + profile, true);
                var bodies = assembly.GetType("ME.BECS.SourceGenerated.DestroyCallbacks_" + profile, true);
                Assert.IsNull(bodies.TypeInitializer, "Burst roots must not evaluate managed publication arrays.");
                Assert.IsEmpty(bodies.GetFields(Static | BindingFlags.Public));
                Assert.IsTrue(Attribute.IsDefined(bodies, typeof(Unity.Burst.BurstCompileAttribute)));
                Assert.IsFalse(Attribute.IsDefined(publisher, typeof(Unity.Burst.BurstCompileAttribute)));
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallDestroyFragment") }, Calls(publish));
                Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEngine.Scripting.PreserveAttribute)));
                if (profile == "Editor") Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var entries = Field<KeyValuePair<int, string>[]>(document, "Entries");
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var index = 0; index < entries.Length; ++index) {
                    var ordinal = entries[index].Key;
                    Assert.IsTrue(seen.Add(ordinal));
                    var component = Type.GetType(entries[index].Value, true);
                    Assert.AreEqual(selected[ordinal], component);
                    var register = Calls(callbacks[index].Method).Single();
                    Assert.AreEqual(typeof(WorldStaticCallbacks), register.DeclaringType);
                    Assert.AreEqual("RegisterAutoDestroyCallback", register.Name);
                    Assert.AreEqual(component, register.GetGenericArguments().Single());
                    var callback = bodies.GetMethod("Destroy_" + ordinal);
                    Assert.IsFalse(callback.ContainsGenericParameters);
                    Assert.IsTrue(Attribute.IsDefined(callback, typeof(Unity.Burst.BurstCompileAttribute)));
                    Assert.IsTrue(Attribute.IsDefined(callback, typeof(UnityEngine.Scripting.PreserveAttribute)));
                    Assert.AreEqual(typeof(AutoDestroyRegistry.DestroyDelegate), callback.GetCustomAttributesData()
                        .Single(attribute => attribute.AttributeType == typeof(AOT.MonoPInvokeCallbackAttribute)).ConstructorArguments.Single().Value);
                    // Burst's IL postprocessor replaces the entry point with a
                    // nongeneric direct-call dispatcher and moves our body here.
                    var managedBody = bodies.GetMethod(callback.Name + "$BurstManaged", Static | BindingFlags.Public) ?? callback;
                    var target = Calls(managedBody).Single();
                    Assert.AreEqual(bodies, target.DeclaringType);
                    Assert.AreEqual("Invoke", target.Name);
                    Assert.AreEqual(component, target.GetGenericArguments().Single());
                    var availability = new object[] { component, profile == "Editor" };
                    Assert.IsTrue((bool)Call(bridge, "HasDestroyRegistration", availability), component.FullName);
                }
                var compiled = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(profile == "Editor"
                    ? UnityEditor.Compilation.AssembliesType.Editor : UnityEditor.Compilation.AssembliesType.Player).Single(item => item.name == owner);
                Assert.IsTrue(compiled.compilerOptions.AllowUnsafeCode, owner);
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, selected.Length), seen);
            Assert.IsNotEmpty(selected);
            var validation = new object[] { null };
            Assert.IsTrue((bool)Call(Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true), "ValidateDestroy", validation), (string)validation[0]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public unsafe void CallbackPreservesStorageAndUsesExplicitInterfaceImplementation(bool functionPointer) {
            AllTests.Start();
            try {
                TestDestroyData.value.Data = 0;
                var component = new StorageProbe { value = 5 };
                var ent = default(Ent);
                if (functionPointer) {
                    var pointer = StaticTypesDestroyRegistry.registry.Data.Get(StaticTypes<StorageProbe>.typeId);
                    Assert.IsTrue(pointer.IsCreated);
                    pointer.Invoke(in ent, (byte*)&component);
                } else {
                    var callback = Callback(typeof(StorageProbe), "Editor");
                    var managedBody = callback.DeclaringType.GetMethod(callback.Name + "$BurstManaged", Static | BindingFlags.Public) ?? callback;
                    ((AutoDestroyRegistry.DestroyDelegate)Delegate.CreateDelegate(typeof(AutoDestroyRegistry.DestroyDelegate), managedBody))(in ent, (byte*)&component);
                }
                Assert.AreEqual(12, component.value, "Do not copy storage or call a same-named public method.");
                Assert.AreEqual(12, TestDestroyData.value.Data);
            } finally { AllTests.Dispose(); }
        }

        [Test]
        public unsafe void TagCallbackAcceptsNullStorageThroughInstalledFunctionPointer() {
            AllTests.Start();
            try {
                Assert.IsTrue(StaticTypes<TestComponentDestroy>.isTag);
                TestDestroyData.value.Data = 0;
                var ent = default(Ent);
                var callback = StaticTypesDestroyRegistry.registry.Data.Get(StaticTypes<TestComponentDestroy>.typeId);
                Assert.IsTrue(callback.IsCreated);
                callback.Invoke(in ent, null);
                Assert.AreEqual(1, TestDestroyData.value.Data);
            } finally { AllTests.Dispose(); }
        }

        [Test]
        public void RepeatedBootstrapKeepsDestroyCallbacksForRemovalAndEntityDestruction() {
            for (var repeat = 0; repeat < 2; ++repeat) {
                AllTests.Start();
                try {
                    using var world = World.Create();
                    var ent = Ent.New(world);
                    ent.Set(new StorageProbe { value = 8 });
                    TestDestroyData.value.Data = 0;
                    ent.Remove<StorageProbe>();
                    Assert.AreEqual(15, TestDestroyData.value.Data);
                    Assert.IsFalse(ent.Has<StorageProbe>());
                    ent.Set(new StorageProbe { value = 20 });
                    ent.Destroy();
                    Assert.AreEqual(27, TestDestroyData.value.Data);
                } finally { AllTests.Dispose(); }
            }
        }
    }
}
