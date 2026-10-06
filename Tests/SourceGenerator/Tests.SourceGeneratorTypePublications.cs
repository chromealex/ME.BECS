using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorTypePublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorTypeFragmentFormat", true);
        private static Type Transport => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true);
        private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, Static).Invoke(null, args);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static T Field<T>(object doc, string name) => (T)doc.GetType().GetField(name, Instance).GetValue(doc);
        private static object[] Documents(IEnumerable<string> rows, bool editor) => ((Array)Call(Format, "Documents", rows, editor)).Cast<object>().ToArray();
        private static string Serialize(object doc) => (string)Call(Format, "Serialize", doc);
        private static string Row(int ordinal, string phase, string component, string owner, string group = "") =>
            "type-registration-owner\t" + ordinal + "\t" + Encode((string)Call(Format, "EntryValue", phase, component, group)) + "\t" + Encode(owner);

        [TestCase(false)]
        [TestCase(true)]
        public void PhaseMajorSelectionRoundTripsAndIgnoresUnrelatedGraphChanges(bool editor) {
            var rows = new[] { Row(0, "Group", "C, Types", "A", "G`1, Types"), Row(1, "Register", "C, Types", "A"),
                Row(2, "Register", "Other, Types", "Z"), Row(3, "RegisterShared", "C, Types", "A"), Row(4, "RegisterStatic", "Other, Types", "Z") };
            var documents = Documents(rows, editor);
            CollectionAssert.AreEqual(documents.Select(Serialize).ToArray(), Documents(rows.Reverse(), editor).Select(Serialize).ToArray());
            CollectionAssert.AreEqual(documents.Select(Serialize).ToArray(), Documents(rows.Concat(new[] { "graph-input-snapshot\t0\tchanged", "system-registration-owner\t0\tignored" }), editor).Select(Serialize).ToArray());
            foreach (var doc in documents) {
                var args = new object[] { Serialize(doc), null };
                Assert.IsTrue((bool)Call(Format, "TryParse", args));
                Assert.AreEqual(Serialize(doc), Serialize(args[1]));
                Assert.AreEqual(5, Field<int>(doc, "Count"));
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, Field<KeyValuePair<int, string>[]>(documents[0], "Entries").Select(item => item.Key));
        }

        [TestCase("phase-order")]
        [TestCase("duplicate")]
        [TestCase("gap")]
        [TestCase("unknown-phase")]
        [TestCase("missing-group")]
        [TestCase("unexpected-group")]
        public void InvalidSelectionsCannotPublishPartialPlans(string change) {
            var first = Row(0, "Register", "C, Types", "Owner");
            var second = Row(1, "RegisterShared", "C, Types", "Owner");
            switch (change) {
                case "phase-order": second = Row(1, "Group", "C, Types", "Owner", "G, Types"); break;
                case "duplicate": second = Row(1, "Register", "C, Types", "Owner"); break;
                case "gap": second = Row(2, "RegisterShared", "C, Types", "Owner"); break;
                case "unknown-phase": second = Row(1, "Execute", "C, Types", "Owner"); break;
                case "missing-group": first = Row(0, "Group", "C, Types", "Owner"); break;
                case "unexpected-group": second = Row(1, "RegisterShared", "C, Types", "Owner", "G, Types"); break;
            }
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { first, second }, false));
        }

        [Test]
        public void StaleGroupAndRetiredOwnerCannotReuseCurrentPublicationEvidence() {
            var first = Documents(new[] { Row(0, "Group", "C, Types", "Owner", "G1, Types") }, false).Single();
            var second = Documents(new[] { Row(0, "Group", "C, Types", "Owner", "G2, Types") }, false).Single();
            Assert.AreNotEqual(Field<string>(first, "Plan"), Field<string>(second, "Plan"));
            first.GetType().GetField("Entries", Instance).SetValue(first, Array.Empty<KeyValuePair<int, string>>());
            var args = new object[] { Serialize(first), null };
            Assert.IsTrue((bool)Call(Format, "TryParse", args), "An empty owner is a valid retirement tombstone.");
            Assert.IsEmpty(Field<KeyValuePair<int, string>[]>(args[1], "Entries"));
            args = new object[] { Serialize(second).TrimEnd('\n'), null };
            Assert.IsFalse((bool)Call(Format, "TryParse", args));
        }

        [Test]
        public void RetiringTypeAndSystemBlocksPreservesUserOptions() {
            const string begin = "#\"ME.BECS type fragments begin\"";
            const string end = "#\"ME.BECS type fragments end\"";
            const string system = "#\"ME.BECS system fragments begin\"\n-additionalfile:system\n#\"ME.BECS system fragments end\"\n";
            const string original = "@\"Assets/csc.rsp\"\r\n-define:USER\r\n" + system;
            var block = begin + "\n-additionalfile:type\n" + end + "\n";
            var patched = (string)Call(Transport, "RemoveManagedResponse", original + block, begin, end);
            Assert.AreEqual(original, patched);
            var migration = Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputMigration", true);
            var cleaned = (string)Call(migration, "WithoutManagedOptions", original + block);
            Assert.AreEqual("@\"Assets/csc.rsp\"\r\n-define:USER\r\n", cleaned);
            Assert.AreEqual(cleaned, Call(migration, "WithoutManagedOptions", cleaned));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void CommonSelectionHasNoCallbacksAndAllOwnersPublishWithoutAssigningIds(string profile) {
            var aggregate = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var selection = Tests_SourceGeneratorBootstrapPublications.Owner(aggregate).GetType("ME.BECS.SourceGenerated.BootstrapProfile_" + profile, true);
            Assert.IsEmpty(selection.GetFields(Static));
            Assert.AreEqual(1, Calls(selection.GetMethod("Publish", Static)).Count(method => method == typeof(BootstrapRuntime).GetMethod("ExpectTypePlan")));
            var rows = Rows(aggregate, profile);
            var documents = Documents(rows, profile == "Editor");
            Assert.IsNotEmpty(documents);
            foreach (var document in documents) {
                var owner = Field<string>(document, "Owner");
                var publisher = Assembly.Load(owner).GetType("ME.BECS.SourceGenerated.TypeFragment_" + profile, true);
                Assert.IsFalse(publisher.Assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                Assert.IsFalse(publisher.Assembly.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallTypeFragment") }, Calls(publish));
                Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEngine.Scripting.PreserveAttribute)));
                if (profile == "Editor") Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var selected = Field<KeyValuePair<int, string>[]>(document, "Entries");
                var ordinals = (int[])publisher.GetField("Ordinals", Static).GetValue(null);
                CollectionAssert.AreEqual(selected.Select(item => item.Key), ordinals);
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                Assert.AreEqual(ordinals.Length, callbacks.Length);
                Assert.IsTrue(callbacks.All(callback => callback.Method.DeclaringType.Assembly == publisher.Assembly));
                for (var i = 0; i < selected.Length; ++i) {
                    var args = new object[] { selected[i].Value, null };
                    Assert.IsTrue((bool)Call(Format, "TryEntry", args));
                    var entry = args[1];
                    if (Field<string>(entry, "Phase") != "Group") continue;
                    var expectedComponent = Type.GetType(Field<string>(entry, "Component"), true);
                    var expectedGroup = Type.GetType(Field<string>(entry, "Group"), true);
                    var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(callbacks[i].Method);
                    // The general disassembler resolves InlineTok with empty generic
                    // argument arrays; Mono rejects unbound generic typeof tokens in
                    // that path. Resolve these type tokens without an invented context.
                    var bytes = callbacks[i].Method.GetMethodBody().GetILAsByteArray();
                    var groups = instructions.Where(item => item.OpCode == System.Reflection.Emit.OpCodes.Ldtoken)
                        .Select(item => callbacks[i].Method.Module.ResolveType(BitConverter.ToInt32(bytes, item.Offset + item.OpCode.Size))).ToArray();
                    CollectionAssert.Contains(groups, expectedGroup);
                    var apply = Calls(callbacks[i].Method).Single(method => method.Name == "ApplyGroup");
                    Assert.AreEqual(typeof(StaticTypes<>).MakeGenericType(expectedComponent), apply.DeclaringType);
                }
                var compiler = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor).Single(item => item.name == owner);
                var name = (string)Call(Format, "FileName", owner, profile == "Editor");
                Assert.IsTrue(compiler.compilerOptions.RoslynAdditionalFilePaths.Any(path => System.IO.Path.GetFileName(path) == name));
            }
            Assert.AreEqual(rows.Count(row => row.StartsWith("type-registration-owner\t")), Tests_SourceGeneratorBootstrapTypePlan.SelectedTypes(aggregate).Length);
            var check = new object[] { true, null };
            Assert.IsTrue((bool)Call(Transport, "ValidateSelection", check), (string)check[1]);
        }

        private static string[] Rows(Assembly assembly, string profile) => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(item => item.Key == "ME.BECS.TypeInput.v1" && item.Value.StartsWith(profile.ToLowerInvariant() + "\t"))
            .Select(item => item.Value.Substring(profile.Length + 1)).ToArray();
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(item => item.OpCode == System.Reflection.Emit.OpCodes.Call || item.OpCode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(item => (MethodInfo)item.Operand).ToArray();
    }
}
