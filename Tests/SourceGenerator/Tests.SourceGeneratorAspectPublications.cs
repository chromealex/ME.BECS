using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorAspectPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorAspectFragmentFormat", true);
        private static Type Registry => typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapAspectRegistry", true);
        private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, Static).Invoke(null, args);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static T Field<T>(object doc, string name) => (T)doc.GetType().GetField(name, Instance).GetValue(doc);
        private static object[] Documents(IEnumerable<string> rows, bool editor) => ((Array)Call(Format, "Documents", rows, editor)).Cast<object>().ToArray();
        private static string Serialize(object doc) => (string)Call(Format, "Serialize", doc);
        private static string Row(int id, string aspect, string owner) => "aspect-registration-owner\t" + id + "\t" + Encode(aspect) + "\t" + Encode(owner);
        private static string[] Rows(Assembly assembly, string profile) => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(item => item.Key == "ME.BECS.TypeInput.v1" && item.Value.StartsWith(profile.ToLowerInvariant() + "\t", StringComparison.Ordinal))
            .Select(item => item.Value.Substring(profile.Length + 1)).ToArray();
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(item => item.OpCode == OpCodes.Call || item.OpCode == OpCodes.Callvirt).Select(item => (MethodInfo)item.Operand).ToArray();
        private static void Install(object registry, string owner, int count, int[] indices, Action[] initialize,
                                    WorldStaticCallbacks.CallbackDelegate<World>[] construct, string plan = "plan") =>
            Registry.GetMethod("Install", Instance).Invoke(registry, new object[] { plan, owner, count, indices, initialize, construct });
        private static void Initialize(object registry) => Registry.GetMethod("Initialize", Instance).Invoke(registry, null);
        private static void Construct(object registry) => Registry.GetMethod("Construct", Instance).Invoke(registry, new object[] { default(World) });
        private static void Incomplete(TestDelegate action) => Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(action).InnerException);

        [TestCase(false)]
        [TestCase(true)]
        public void BothPhasesUseGlobalOrderRegardlessOfOwnerArrival(bool reverse) {
            var registry = Activator.CreateInstance(Registry, true);
            var calls = new System.Collections.Generic.List<int>();
            var initialize = Enumerable.Range(0, 4).Select(index => (Action)(() => calls.Add(index))).ToArray();
            var construct = Enumerable.Range(0, 4).Select(index => (WorldStaticCallbacks.CallbackDelegate<World>)((ref World world) => calls.Add(10 + index))).ToArray();
            construct[1] = null; // An explicitly empty aspect still has a registration ordinal.
            foreach (var owner in reverse ? new[] { 1, 0 } : new[] { 0, 1 }) {
                var indices = new[] { owner, owner + 2 };
                Install(registry, "owner" + owner, 4, indices, indices.Select(i => initialize[i]).ToArray(), indices.Select(i => construct[i]).ToArray());
            }
            Assert.IsEmpty(calls, "Publication must not execute either phase.");
            Initialize(registry);
            Construct(registry);
            Construct(registry);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 10, 12, 13, 10, 12, 13 }, calls);
        }

        [Test]
        public void MissingOwnerPreventsBothPhasesAndEmptySelectionIsExplicit() {
            var registry = Activator.CreateInstance(Registry, true);
            Incomplete(() => Initialize(registry));
            Incomplete(() => Construct(registry));
            var calls = 0;
            Install(registry, "owner", 2, new[] { 1 }, new Action[] { () => ++calls }, new WorldStaticCallbacks.CallbackDelegate<World>[] { (ref World world) => ++calls });
            Incomplete(() => Initialize(registry));
            Incomplete(() => Construct(registry));
            Assert.AreEqual(0, calls);
            registry = Activator.CreateInstance(Registry, true);
            Install(registry, "$selection", 0, Array.Empty<int>(), Array.Empty<Action>(), Array.Empty<WorldStaticCallbacks.CallbackDelegate<World>>());
            Assert.DoesNotThrow(() => Initialize(registry));
            Assert.DoesNotThrow(() => Construct(registry));
            CollectionAssert.Contains(Calls(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan")), Registry.GetMethod("RequireComplete", Instance));
        }

        [Test]
        public void PublicationSnapshotsBothDelegateArraysAndReplayIsIdempotent() {
            var registry = Activator.CreateInstance(Registry, true);
            var calls = 0;
            Action initialize = () => ++calls;
            WorldStaticCallbacks.CallbackDelegate<World> construct = (ref World world) => calls += 10;
            var indices = new[] { 0 };
            var initializers = new[] { initialize };
            var constructors = new[] { construct };
            Install(registry, "owner", 1, indices, initializers, constructors);
            indices[0] = 99;
            initializers[0] = () => calls += 100;
            constructors[0] = null;
            Install(registry, "owner", 1, new[] { 0 }, new[] { initialize }, new[] { construct });
            Initialize(registry);
            Construct(registry);
            Assert.AreEqual(11, calls);
        }

        [TestCase("constructor")]
        [TestCase("initializer")]
        [TestCase("overlap")]
        [TestCase("identity")]
        [TestCase("count")]
        public void ConflictingPublicationCannotInitializeOrConstruct(string change) {
            var registry = Activator.CreateInstance(Registry, true);
            Action initialize = () => { };
            WorldStaticCallbacks.CallbackDelegate<World> construct = (ref World world) => { };
            Install(registry, "owner", 1, new[] { 0 }, new[] { initialize }, new[] { construct });
            Incomplete(() => Install(registry, change == "overlap" ? "other" : "owner", change == "count" ? 2 : 1, new[] { 0 },
                new[] { change == "initializer" ? (Action)(() => throw new System.Exception("must not run")) : initialize },
                new[] { change == "constructor" ? null : construct }, change == "identity" ? "changed" : "plan"));
            Incomplete(() => Initialize(registry));
            Incomplete(() => Construct(registry));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AspectDocumentsPreserveSelectionOrderAndRetirement(bool editor) {
            var rows = new[] { Row(0, "A, Types", "Z"), Row(1, "B, Types", "A"), Row(2, "C, Types", "Z") };
            var docs = Documents(rows, editor);
            CollectionAssert.AreEqual(docs.Select(Serialize), Documents(rows.Reverse(), editor).Select(Serialize));
            CollectionAssert.AreEqual(new[] { 0, 2 }, Field<KeyValuePair<int, string>[]>(docs[1], "Entries").Select(item => item.Key));
            var parse = new object[] { Serialize(docs[1]), null };
            Assert.IsTrue((bool)Call(Format, "TryParse", parse));
            Assert.AreEqual(Serialize(docs[1]), Serialize(parse[1]));
            docs[1].GetType().GetField("Entries", Instance).SetValue(docs[1], Array.Empty<KeyValuePair<int, string>>());
            parse = new object[] { Serialize(docs[1]), null };
            Assert.IsTrue((bool)Call(Format, "TryParse", parse), "Retired owner has no publisher.");
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(2, "B, Types", "A") }, editor));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(1, "A, Types", "Other") }, editor));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { Row(0, "A, Types", "ME.BECS.Gen.Editor") }, editor));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void TypedCallbacksLiveInOwnersAndMatchBothSelections(string profile) {
            var aggregate = Assembly.Load("ME.BECS.Gen." + profile);
            var selection = aggregate.GetType("ME.BECS.SourceGenerated.BootstrapAspectSelection", true);
            Assert.IsEmpty(selection.GetFields(Static));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("ExpectAspectPlan") }, Calls(selection.GetMethod("Publish")));
            var facade = aggregate.GetType("ME.BECS.SourceGenerated.AspectInputs", true);
            CollectionAssert.AreEquivalent(new[] { "Initialize", "RegisterConstruction" }, facade.GetMethods(Static | BindingFlags.Public | BindingFlags.DeclaredOnly).Select(method => method.Name));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("RegisterInstalledAspects") }, Calls(facade.GetMethod("Initialize")));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("RegisterInstalledAspectConstruction") }, Calls(facade.GetMethod("RegisterConstruction")));
            var rows = Rows(aggregate, profile);
            Type[] Selected(string kind) => rows.Where(row => row.StartsWith(kind + "\t", StringComparison.Ordinal))
                .Select(row => row.Split('\t')).OrderBy(row => int.Parse(row[1])).Select(row => Type.GetType(Decode(row[2]), true)).ToArray();
            var selected = Selected("aspect-registration");
            CollectionAssert.AreEqual(selected, Selected("aspect-construction-auto"));
            Assert.IsNotEmpty(selected);
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var document in Documents(rows, profile == "Editor")) {
                var owner = Field<string>(document, "Owner");
                var publisher = Assembly.Load(owner).GetType("ME.BECS.SourceGenerated.AspectFragment_" + profile, true);
                Assert.AreNotEqual(aggregate, publisher.Assembly);
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallAspectFragment") }, Calls(publish));
                Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEngine.Scripting.PreserveAttribute)));
                if (profile == "Editor") Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var entries = Field<KeyValuePair<int, string>[]>(document, "Entries");
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                var constructors = (WorldStaticCallbacks.CallbackDelegate<World>[])publisher.GetField("Constructors", Static).GetValue(null);
                Assert.AreEqual(entries.Length, callbacks.Length);
                Assert.AreEqual(entries.Length, constructors.Length);
                for (var i = 0; i < entries.Length; ++i) {
                    Assert.IsTrue(seen.Add(entries[i].Key));
                    var aspect = selected[entries[i].Key];
                    Assert.AreEqual(aspect, Type.GetType(entries[i].Value, true));
                    var calls = Calls(callbacks[i].Method);
                    Assert.AreEqual(2, calls.Length);
                    Assert.AreEqual(typeof(AspectTypeInfo<>).MakeGenericType(aspect).GetMethod("Validate"), calls[0]);
                    Assert.AreEqual(aspect.Assembly, calls[1].DeclaringType.Assembly);
                    StringAssert.StartsWith("InitializeAspectQuery_", calls[1].Name);
                    var hasData = aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(field => typeof(IAspectData).IsAssignableFrom(field.FieldType));
                    Assert.AreEqual(hasData, constructors[i] != null);
                    if (hasData) {
                        Assert.AreEqual(aspect.Assembly, constructors[i].Method.DeclaringType.Assembly);
                        Assert.AreEqual("ConstructAspect_" + calls[1].Name.Substring("InitializeAspectQuery_".Length), constructors[i].Method.Name);
                    }
                }
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, selected.Length), seen);
            var validation = new object[] { null };
            Assert.IsTrue((bool)Call(Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true), "ValidateAspects", validation), (string)validation[0]);
        }

        [Test]
        public void RepeatedBootstrapConstructsIndependentWorldPointersAndPrivatePartialFields() {
            uint[] ids = null;
            for (var repeat = 0; repeat < 2; ++repeat) {
                AllTests.Start();
                try {
                    var selected = Rows(Assembly.Load("ME.BECS.Gen.Editor"), "Editor").Where(row => row.StartsWith("aspect-registration\t", StringComparison.Ordinal))
                        .Select(row => row.Split('\t')).OrderBy(row => int.Parse(row[1])).Select(row => Type.GetType(Decode(row[2]), true)).ToArray();
                    var current = selected.Select(type => AspectTypeInfoLoadedManaged.typeToId[type]).ToArray();
                    if (ids != null) CollectionAssert.AreEqual(ids, current);
                    ids = current;
                    using var first = World.Create();
                    using var second = World.Create();
                    var a = Ent.New(first);
                    var b = Ent.New(second);
                    a.Set(new TestComponent { data = 17 });
                    b.Set(new TestComponent { data = 91 });
                    var aspectA = a.GetAspect<TestAspect>();
                    var aspectB = b.GetAspect<TestAspect>();
                    Assert.AreEqual(17, aspectA.data.data);
                    Assert.AreEqual(91, aspectB.data.data);
                    aspectA.data.data = 42;
                    Assert.AreEqual(91, aspectB.data.data);
                    foreach (var item in new[] { (Ent: a, Value: 42), (Ent: b, Value: 91) }) {
                        var aspect = item.Ent.GetAspect<Tests_SourceGeneratorContracts.QueryFilterAspect>();
                        var pointer = (AspectDataPtr<TestComponent>)typeof(Tests_SourceGeneratorContracts.QueryFilterAspect).GetField("required", Instance).GetValue(aspect);
                        Assert.AreEqual(item.Value, pointer.Read(item.Ent.id, item.Ent.gen).data);
                    }
                    var query = AspectTypeInfo.with.Get(AspectTypeInfo<Tests_SourceGeneratorContracts.QueryFilterAspect>.typeId);
                    Assert.AreEqual(1u, query.Length);
                    Assert.AreEqual(StaticTypes<TestComponent>.typeId, query.Get(0));
                } finally { AllTests.Dispose(); }
            }
        }
    }
}
