using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorSystemPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
        private static Type Transport => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true);
        private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, Static).Invoke(null, args);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static string Row(int ordinal, string type, string owner) => "system-registration-owner\t" + ordinal + "\t" + Encode(type) + "\t" + Encode(owner);
        private static object[] Documents(string[] rows, bool editor) => ((Array)Call(Transport, "Documents", rows, editor)).Cast<object>().ToArray();
        private static T Field<T>(object doc, string field) => (T)doc.GetType().GetField(field, Instance).GetValue(doc);
        private static string Serialize(object doc) => (string)Call(Format, "Serialize", doc);

        [TestCase(false)]
        [TestCase(true)]
        public void InterleavedSelectionRoundTripsWithoutGraphOrCodeIdentity(bool editor) {
            var rows = new[] { Row(0, "First, Definition", "Z"), Row(1, "Second, Definition", "A"), Row(2, "Third, Definition", "Z") };
            var documents = Documents(rows, editor);
            CollectionAssert.AreEqual(documents.Select(Serialize).ToArray(), Documents(rows.Reverse().ToArray(), editor).Select(Serialize).ToArray());
            CollectionAssert.AreEqual(new[] { "A", "Z" }, documents.Select(doc => Field<string>(doc, "Owner")).ToArray());
            var withUnrelatedGraph = Documents(rows.Concat(new[] { "graph-input-snapshot\t0\tchanged", "component-registration\t0\tchanged" }).ToArray(), editor);
            CollectionAssert.AreEqual(documents.Select(Serialize).ToArray(), withUnrelatedGraph.Select(Serialize).ToArray());
            foreach (var doc in documents) {
                var text = Serialize(doc);
                var args = new object[] { text, null };
                Assert.IsTrue((bool)Call(Format, "TryParse", args));
                Assert.AreEqual(text, Serialize(args[1]));
                Assert.AreEqual(3, Field<int>(args[1], "Count"));
                Assert.AreEqual(editor, Field<bool>(args[1], "Editor"));
            }
            CollectionAssert.AreEqual(new[] { 0, 2 }, Field<KeyValuePair<int, string>[]>(documents[1], "Entries").Select(entry => entry.Key).ToArray());
        }

        [Test]
        public void AlteredOrTruncatedPayloadDoesNotProduceAPartialDocument() {
            var text = Serialize(Documents(new[] { Row(0, "First, Definition", "Owner") }, false).Single());
            foreach (var invalid in new[] { text.TrimEnd('\n'), text.Replace(Encode("First, Definition"), Encode("Changed, Definition")), text + "garbage\n" }) {
                var args = new object[] { invalid, null };
                Assert.IsFalse((bool)Call(Format, "TryParse", args));
                Assert.IsNull(args[1]);
            }
        }

        [Test]
        public void ChangedSelectionOrOwnershipChangesThePlanButNotItsFileName() {
            var first = Documents(new[] { Row(0, "First, Definition", "Owner") }, false).Single();
            var second = Documents(new[] { Row(0, "Second, Definition", "Owner") }, false).Single();
            Assert.AreNotEqual(Field<string>(first, "Plan"), Field<string>(second, "Plan"));
            Assert.AreEqual(Call(Format, "FileName", Field<string>(first, "Owner"), false), Call(Format, "FileName", Field<string>(second, "Owner"), false));
            Assert.AreNotEqual(Field<string>(first, "Plan"), Field<string>(Documents(new[] { Row(0, "First, Definition", "OtherOwner") }, false).Single(), "Plan"));
        }

        [TestCase("@Assets/csc.rsp")]
        [TestCase("-define:USER\r\n-unsafe+\r\n")]
        [TestCase("")]
        public void RetiringManagedResponseBlockPreservesUserOptionsAndIsIdempotent(string original) {
            const string block = "#\"ME.BECS system fragments begin\"\n-additionalfile:\"Assets/With Spaces/selection\"\n#define\n#\"ME.BECS system fragments end\"\n";
            const string begin = "#\"ME.BECS system fragments begin\"";
            const string end = "#\"ME.BECS system fragments end\"";
            var withSuffix = original + block + "-define:AFTER\r\n";
            var changed = (string)Call(Transport, "RemoveManagedResponse", withSuffix, begin, end);
            Assert.AreEqual(original + "-define:AFTER\r\n", changed);
            Assert.AreEqual(changed, Call(Transport, "RemoveManagedResponse", changed, begin, end));
            StringAssert.DoesNotContain(begin, changed);
        }

        [TestCase("#\"ME.BECS system fragments begin\"\n")]
        [TestCase("#\"ME.BECS system fragments end\"\n")]
        [TestCase("#\"ME.BECS system fragments begin\"\n#\"ME.BECS system fragments begin\"\n#\"ME.BECS system fragments end\"\n")]
        public void AmbiguousManagedBlocksAreRejectedInsteadOfOverwritingOptions(string text) {
            Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Call(Transport, "RemoveManagedResponse", text,
                "#\"ME.BECS system fragments begin\"", "#\"ME.BECS system fragments end\"")).InnerException);
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void CommonPublisherOnlyDeclaresSelectionAndOwnersPublishTypedCallbacks(string profile) {
            var catalog = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var selection = Tests_SourceGeneratorBootstrapPublications.Owner(catalog).GetType("ME.BECS.SourceGenerated.BootstrapProfile_" + profile, true);
            Assert.IsEmpty(selection.GetFields(Static), "The composition must not retain system callback arrays.");
            var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(selection.GetMethod("Publish", Static))
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call).Select(instruction => instruction.Operand).ToArray();
            Assert.AreEqual(1, calls.Count(method => Equals(method, typeof(BootstrapRuntime).GetMethod("ExpectSystemPlan"))));
            Assert.IsNotEmpty(Tests_SourceGeneratorBootstrapTypePlan.SelectedSystems(catalog));
            var publishers = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType("ME.BECS.SourceGenerated.SystemFragment_" + profile, false)).Where(type => type != null).ToArray();
            Assert.IsNotEmpty(publishers);
            foreach (var publisher in publishers) {
                Assert.IsFalse(publisher.Assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                Assert.IsFalse(publisher.Assembly.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
                var publish = publisher.GetMethod("Publish", Static);
                Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEngine.Scripting.PreserveAttribute)));
                if (profile == "Editor") Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded,
                    publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var effects = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(publish)
                    .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call).Select(instruction => instruction.Operand).ToArray();
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallSystemFragment") }, effects,
                    "Loading an assembly must not allocate system IDs or execute registrations.");
            }
        }

        [Test]
        public void CompiledFragmentsMatchCurrentSelectionAndSourceInputFingerprints() {
            var args = new object[] { null };
            Assert.IsTrue((bool)Call(Transport, "ValidateCompiled", args), (string)args[0]);
            foreach (var profile in new[] { "Editor", "Runtime" }) {
                var rows = Tests_SourceGeneratorInputCatalog.Rows(profile == "Editor");
                foreach (var doc in Documents(rows, profile == "Editor")) {
                    var owner = Field<string>(doc, "Owner");
                    var compiler = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor).Single(item => item.name == owner);
                    var name = (string)Call(Format, "FileName", owner, profile == "Editor");
                    Assert.IsTrue(compiler.compilerOptions.RoslynAdditionalFilePaths.Any(path => System.IO.Path.GetFileName(path) == name));
                }
            }
        }
    }
}
