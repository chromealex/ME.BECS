using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorInputCatalog {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Catalog => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorInputCatalog", true);
        private static Type Format => Catalog.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorInputCatalogFormat", true);
        private static Type Envelope => Catalog.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
        private static object Call(Type type, string method, params object[] args) => type.GetMethod(method, Static).Invoke(null, args);
        internal static Assembly Owner(bool editor) => (Assembly)Call(Catalog, "GetAssembly", editor);
        internal static string[] Rows(bool editor) => (string[])Call(Catalog, "GetRows", editor);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static string Hash(string value) => (string)Call(Catalog.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true), "Hash", value);
        private static string Manifest(bool editor, string owner = "Example.Inputs", string extra = "") {
            var payload = "ME.BECS.TypeInputs.v3\t" + Encode("Former.Aggregate") + "\t" + (editor ? "editor" : "runtime") + "\n" +
                "inputcatalog-publication-schema\t0\tdjE=\ninputcatalog-registration-owner\t0\tdjE=\t" + Encode(owner) + "\n" +
                "graph-input-snapshot\t0\t" + Encode(new string('A', 64)) + "\n" + extra;
            return payload + "end\t" + (payload.Count(c => c == '\n') - 1) + "\t" + Hash(payload) + "\n";
        }
        private static bool Parse(string content) => (bool)Call(Format, "TryParse", content, null);
        private static bool Find(bool editor, Assembly[] assemblies, out string reason) {
            var method = Catalog.GetMethods(Static).Single(candidate => candidate.Name == "TryGet" && candidate.GetParameters().Length == 4);
            var args = new object[] { editor, assemblies, null, null };
            var found = (bool)method.Invoke(null, args);
            reason = (string)args[3];
            return found;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompiledCatalogHasIndependentOwnerAndExactInputEvidence(bool editor) {
            var owner = Owner(editor);
            Assert.IsFalse(owner.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
            Assert.IsFalse(owner.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
            var root = owner.GetType("ME.BECS.SourceGenerated.InputCatalog_" + (editor ? "Editor" : "Runtime"), true);
            Assert.IsEmpty(root.GetMethods(Static | BindingFlags.DeclaredOnly));
            Assert.IsNull(root.TypeInitializer, "Input evidence must not initialize or reset a world.");
            var transport = Catalog.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputTransport", true);
            var path = (string)Call(transport, "InputPath", editor);
            StringAssert.StartsWith("Assets/ME.BECS.SourceInputs/", path);
            var content = System.IO.File.ReadAllText(path);
            var document = Call(Format, "Document", content, editor);
            var receipt = (string)Call(Envelope, "Metadata", document, Call(Format, "Serialize", document));
            var metadata = owner.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            CollectionAssert.AreEqual(new[] { receipt }, metadata.Where(attribute => attribute.Key == "ME.BECS.InputCatalogFragment.v1").Select(attribute => attribute.Value).ToArray());
            CollectionAssert.AreEqual(new[] { Hash(content) }, metadata.Where(attribute => attribute.Key == "ME.BECS.InputContentHash.v1").Select(attribute => attribute.Value).ToArray());
            var expected = content.Replace("\r\n", "\n").Split('\n');
            CollectionAssert.AreEqual(expected.Skip(1).Take(expected.Length - 3).ToArray(), Rows(editor));
            var isolated = Rows(editor);
            isolated[0] = "mutated by a diagnostic caller";
            Assert.AreNotEqual(isolated[0], Rows(editor)[0]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingDuplicateAndWrongProfileCatalogsCannotProveReadiness(bool editor) {
            var owner = Owner(editor);
            Assert.IsTrue(Find(editor, new[] { owner }, out var reason), reason);
            Assert.IsFalse(Find(editor, Array.Empty<Assembly>(), out _));
            Assert.IsFalse(Find(editor, new[] { owner, owner }, out _));
            Assert.IsFalse(Find(!editor, new[] { owner }, out _));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InputEnvelopeRoundTripsAndRetiresWithoutReusingItsPayload(bool editor) {
            var document = Call(Format, "Document", Manifest(editor), editor);
            var serialized = (string)Call(Format, "Serialize", document);
            var args = new object[] { serialized, null };
            Assert.IsTrue((bool)Call(Format, "TryParse", args));
            Assert.AreEqual(serialized, Call(Format, "Serialize", args[1]));
            document.GetType().GetField("Entries", Hidden).SetValue(document, Array.Empty<System.Collections.Generic.KeyValuePair<int, string>>());
            Assert.IsTrue(Parse((string)Call(Format, "Serialize", document)));
            Assert.AreNotEqual(serialized, Call(Format, "Serialize", document));
        }

        [Test]
        public void CatalogRejectsChangedProfileOwnerPlanAndInvalidManifest() {
            foreach (var changed in new[] { "Owner", "Editor", "Plan", "Count" }) {
                var document = Call(Format, "Document", Manifest(false), false);
                var field = document.GetType().GetField(changed, Hidden);
                object value = changed == "Owner" ? (object)"Other.Owner" : changed == "Editor" ? true : changed == "Count" ? 2 : (object)new string('B', 64);
                field.SetValue(document, value);
                Assert.IsFalse(Parse((string)Call(Format, "Serialize", document)), changed);
            }
            foreach (var content in new[] {
                Manifest(false).TrimEnd('\n'), Manifest(false) + "extra\n", Manifest(false).Replace("djE=", "djI="),
                Manifest(false, "ME.BECS.Gen.Runtime"), Manifest(false, extra: "inputcatalog-publication-schema\t0\tdjE=\n"),
                Manifest(false, extra: "graph-input-snapshot\t0\t" + Encode(new string('B', 64)) + "\n"),
            }) Assert.IsFalse((bool)Call(Format, "TryManifest", content, null), content);
        }

        [Test]
        public void ReadinessNeedsRealTypedPublicationsButNoAggregateAssembly() {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)).ToArray();
            var fragments = Catalog.Assembly.GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true);
            var args = new object[] { assemblies, null };
            Assert.IsTrue((bool)Call(fragments, "ValidateCompiledAssemblies", args), (string)args[1]);
            var missingOwner = Rows(false).Select(row => row.Split('\t')).First(fields => fields[0] == "type-registration-owner")[3];
            var missingName = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(missingOwner));
            Assert.AreNotEqual(Owner(false).GetName().Name, missingName, "This must remove typed registration, not input evidence.");
            Assert.AreNotEqual(Owner(true).GetName().Name, missingName);
            args[0] = assemblies.Where(assembly => assembly.GetName().Name != missingName).ToArray();
            Assert.IsFalse((bool)Call(fragments, "ValidateCompiledAssemblies", args));
            StringAssert.Contains(missingName, (string)args[1]);

            var receipt = Catalog.Assembly.GetType("ME.BECS.Editor.SourceGeneratorAnalysisReceipt", true);
            var snapshot = Catalog.Assembly.GetType("ME.BECS.Editor.SourceGeneratorGraphSnapshot", true);
            var fingerprint = (string)Call(snapshot, "GetCurrent");
            args = new object[] { fingerprint, assemblies, null, null };
            Assert.IsTrue((bool)Call(receipt, "TryValidateCompiled", args), (string)args[3]);
        }
    }
}
