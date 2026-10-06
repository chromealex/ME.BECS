using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorEntityPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorEntityFragmentFormat", true);
        private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, Static).Invoke(null, args);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static T Field<T>(object doc, string name) => (T)doc.GetType().GetField(name, Instance).GetValue(doc);
        private static void Set(object doc, string name, object value) => doc.GetType().GetField(name, Instance).SetValue(doc, value);
        private static object[] Documents(IEnumerable<string> rows, bool editor) => ((Array)Call(Format, "Documents", rows, editor)).Cast<object>().ToArray();
        private static string Serialize(object doc) => (string)Call(Format, "Serialize", doc);
        private static string Row(int id, string entity, string owner) => "entity-registration-owner\t" + id + "\t" + Encode(entity) + "\t" + Encode(owner);
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(item => item.OpCode == OpCodes.Call || item.OpCode == OpCodes.Callvirt).Select(item => (MethodInfo)item.Operand).ToArray();

        [TestCase(false)]
        [TestCase(true)]
        public void SelectionUsesGlobalIdsNotOwnerOrInputEnumerationOrder(bool editor) {
            var rows = new[] { Row(0, "E0, Types", "Z"), Row(1, "E1, Types", "A"), Row(2, "E2, Types", "Z") };
            var documents = Documents(rows, editor);
            CollectionAssert.AreEqual(documents.Select(Serialize), Documents(rows.Reverse(), editor).Select(Serialize));
            CollectionAssert.AreEqual(documents.Select(Serialize), Documents(rows.Concat(new[] { "graph-input-snapshot\t0\tchanged" }), editor).Select(Serialize));
            CollectionAssert.AreEqual(new[] { 1 }, Field<KeyValuePair<int, string>[]>(documents[0], "Entries").Select(item => item.Key));
            CollectionAssert.AreEqual(new[] { 0, 2 }, Field<KeyValuePair<int, string>[]>(documents[1], "Entries").Select(item => item.Key));
            foreach (var doc in documents) {
                var args = new object[] { Serialize(doc), null };
                Assert.IsTrue((bool)Call(Format, "TryParse", args));
                Assert.AreEqual(Serialize(doc), Serialize(args[1]));
                Assert.AreEqual(3, Field<int>(doc, "Count"));
            }
            Assert.AreNotEqual(Field<string>(documents[0], "Plan"), Field<string>(Documents(new[] { Row(0, "E1, Types", "A"), Row(1, "E0, Types", "Z"), rows[2] }, editor)[0], "Plan"));
        }

        [TestCase("gap")]
        [TestCase("duplicate-id")]
        [TestCase("duplicate-type")]
        [TestCase("aggregate-owner")]
        [TestCase("empty-owner")]
        public void InvalidEntitySelectionsDoNotPublishPartialCoverage(string change) {
            var rows = new[] { Row(0, "E0, Types", "Owner"), Row(1, "E1, Types", "Owner") };
            switch (change) {
                case "gap": rows[1] = Row(2, "E1, Types", "Owner"); break;
                case "duplicate-id": rows[1] = Row(0, "E1, Types", "Owner"); break;
                case "duplicate-type": rows[1] = Row(1, "E0, Types", "Other"); break;
                case "aggregate-owner": rows[1] = Row(1, "E1, Types", "ME.BECS.Gen.Runtime"); break;
                case "empty-owner": rows[1] = Row(1, "E1, Types", ""); break;
            }
            Assert.Throws<TargetInvocationException>(() => Documents(rows, false));
        }

        [Test]
        public void FullUshortRangeIsRepresentableButOverflowAndDamagedInputsAreNot() {
            var doc = Documents(new[] { Row(0, "Entity, Types", "Owner") }, false).Single();
            Set(doc, "Count", 65536);
            Set(doc, "Entries", new[] { new KeyValuePair<int, string>(65535, "Entity, Types") });
            var args = new object[] { Serialize(doc), null };
            Assert.IsTrue((bool)Call(Format, "TryParse", args));
            Set(doc, "Count", 65537);
            args = new object[] { Serialize(doc), null };
            Assert.IsFalse((bool)Call(Format, "TryParse", args));
            Set(doc, "Count", 1);
            Set(doc, "Entries", Array.Empty<KeyValuePair<int, string>>());
            args = new object[] { Serialize(doc), null };
            Assert.IsTrue((bool)Call(Format, "TryParse", args), "Retired owners carry no initializer.");
            args = new object[] { Serialize(doc).TrimEnd('\n'), null };
            Assert.IsFalse((bool)Call(Format, "TryParse", args));
        }

        [TestCase(-1)]
        [TestCase(65537)]
        public void InvalidGroupCountIsRejectedBeforeTouchingTheInstalledPlan(int count) {
            Assert.Throws<ArgumentOutOfRangeException>(() => BootstrapRuntime.InstallEntityFragment("invalid", "invalid", count,
                Array.Empty<int>(), Array.Empty<Action>(), editor: true));
            Assert.DoesNotThrow(() => BootstrapRuntime.RequireInstalledPlan(editor: true));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void TypedEntityRegistrationsLiveInOwnersAndRetainExactSelectedIds(string profile) {
            var catalog = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var selection = Tests_SourceGeneratorBootstrapPublications.Owner(catalog).GetType("ME.BECS.SourceGenerated.BootstrapProfile_" + profile, true);
            Assert.IsEmpty(selection.GetFields(Static));
            Assert.AreEqual(1, Calls(selection.GetMethod("Publish", Static)).Count(method => method == typeof(BootstrapRuntime).GetMethod("ExpectEntityPlan")));
            Tests_SourceGeneratorBootstrapPhases.AssertFeederSequence(catalog);
            var rows = Tests_SourceGeneratorInputCatalog.Rows(profile == "Editor");
            var selected = rows.Where(row => row.StartsWith("entity-registration\t", StringComparison.Ordinal))
                .Select(row => row.Split('\t')).OrderBy(row => int.Parse(row[1])).Select(row => Type.GetType(Decode(row[2]), true)).ToArray();
            Assert.IsNotEmpty(selected);
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var document in Documents(rows, profile == "Editor")) {
                var owner = Field<string>(document, "Owner");
                var publisher = Assembly.Load(owner).GetType("ME.BECS.SourceGenerated.EntityFragment_" + profile, true);
                Assert.IsFalse(publisher.Assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                Assert.AreEqual(selected.Length, Field<int>(document, "Count"));
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallEntityFragment") }, Calls(publish));
                Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEngine.Scripting.PreserveAttribute)));
                if (profile == "Editor") Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var entries = Field<KeyValuePair<int, string>[]>(document, "Entries");
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var i = 0; i < entries.Length; ++i) {
                    var id = entries[i].Key;
                    Assert.IsTrue(seen.Add(id));
                    var method = callbacks[i].Method;
                    Assert.AreEqual(publisher, method.DeclaringType);
                    var register = Calls(method).Single();
                    Assert.AreEqual(typeof(EntityTypes), register.DeclaringType);
                    Assert.AreEqual("Register", register.Name);
                    Assert.AreEqual(selected[id], register.GetGenericArguments().Single());
                    Assert.AreEqual(selected[id], Type.GetType(entries[i].Value, true));
                    var constant = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).Single(item => item.OpCode.Name.StartsWith("ldc.i4", StringComparison.Ordinal));
                    var actual = constant.OpCode == OpCodes.Ldc_I4 || constant.OpCode == OpCodes.Ldc_I4_S
                        ? Convert.ToInt32(constant.Operand) : int.Parse(constant.OpCode.Name.Substring("ldc.i4.".Length));
                    Assert.AreEqual(id, actual);
                }
                var compiler = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor).Single(item => item.name == owner);
                var name = (string)Call(Format, "FileName", owner, profile == "Editor");
                Assert.IsTrue(compiler.compilerOptions.RoslynAdditionalFilePaths.Any(path => System.IO.Path.GetFileName(path) == name));
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, selected.Length), seen);
            var transport = Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true);
            var check = new object[] { null };
            Assert.IsTrue((bool)Call(transport, "ValidateEntities", check), (string)check[0]);
        }

        [Test]
        public void EntityPhaseValidatesCompleteCoverageBeforeClearingGroupRegistration() {
            var calls = Calls(typeof(BootstrapRuntime).GetMethod("RegisterInstalledEntities"));
            Assert.AreEqual("get_Count", calls[0].Name);
            Assert.AreEqual("ME.BECS.BootstrapTypeRegistry", calls[0].DeclaringType.FullName);
            Assert.AreEqual(typeof(EntityTypes).GetMethod("Init"), calls[1]);
            Assert.AreEqual("Execute", calls[2].Name);
            var registry = Activator.CreateInstance(calls[0].DeclaringType, true);
            var missing = Assert.Throws<TargetInvocationException>(() => calls[0].Invoke(registry, null));
            Assert.IsInstanceOf<InvalidOperationException>(missing.InnerException);
            CollectionAssert.Contains(Calls(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan")),
                calls[0].DeclaringType.GetMethod("RequireComplete", Instance));
        }

        [Test]
        public void RepeatedEditorBootstrapKeepsTheSameGroupIdsAndCount() {
            var selected = Tests_SourceGeneratorInputCatalog.Rows(true).Where(row => row.StartsWith("entity-registration\t", StringComparison.Ordinal))
                .Select(row => row.Split('\t')).OrderBy(row => int.Parse(row[1])).Select(row => Type.GetType(Decode(row[2]), true)).ToArray();
            for (var repeat = 0; repeat < 2; ++repeat) {
                AllTests.Start();
                try {
                    Assert.AreEqual((uint)selected.Length, EntityTypes.groupsCount);
                    var registry = typeof(EntityTypes).Assembly.GetType("ME.BECS.EntityTypesManaged", true);
                    var actual = (Dictionary<ushort, Type>)registry.GetField("typeByGroupId").GetValue(null);
                    CollectionAssert.AreEqual(Enumerable.Range(0, selected.Length).Select(id => (ushort)id), actual.Keys.OrderBy(id => id));
                    CollectionAssert.AreEqual(selected, actual.OrderBy(item => item.Key).Select(item => item.Value));
                } finally { AllTests.Dispose(); }
            }
        }
    }
}
