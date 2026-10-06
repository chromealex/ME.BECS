using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorDependencyPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Catalog => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SystemDependencyCatalog", true);
        private static Type Publisher => (Type)Catalog.GetMethod("GetPublisherType").Invoke(null, null);
        private static Type Format => Catalog.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemDependencyFragmentFormat", true);
        private static object Call(string method, params object[] args) => Format.GetMethod(method, Static).Invoke(null, args);
        private static string[] Rows() => Tests_SourceGeneratorInputCatalog.Rows(true);

        [Test]
        public void GraphDiagnosticsHaveAnIndependentEditorOwnerAndNoStartupHooks() {
            var publisher = Publisher;
            Assert.IsFalse(publisher.Assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
            Assert.IsFalse(publisher.Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
            Assert.IsNull(publisher.TypeInitializer, "Diagnostic tables must not install a bootstrap plan.");
            foreach (var method in publisher.GetMethods(Static)) {
                Assert.IsFalse(method.IsDefined(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute), false));
                Assert.IsFalse(method.IsDefined(typeof(UnityEditor.InitializeOnLoadMethodAttribute), false));
            }
            var selected = publisher.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.SystemDependencySelection.v1").Select(attribute => Type.GetType(attribute.Value.Split('\n')[1], true)).ToArray();
            Assert.IsNotEmpty(selected);
            foreach (var type in selected) {
                Assert.IsTrue((bool)Catalog.GetMethod("ContainsSystem").Invoke(null, new object[] { type }));
                foreach (var name in new[] { "GetSystemDependencies", "GetSystemComponentsDependencies", "GetSystemDependenciesErrors" })
                    Assert.AreSame(publisher.GetMethod(name).Invoke(null, new object[] { type }), Catalog.GetMethod(name).Invoke(null, new object[] { type }));
            }
            Assert.IsFalse((bool)Catalog.GetMethod("ContainsSystem").Invoke(null, new object[] { typeof(string) }));
            Assert.IsFalse((bool)Catalog.GetMethod("ContainsSystem").Invoke(null, new object[] { null }));
            new Tests_SourceGeneratorContracts().EditorSystemDependencyTablesMatchTypedPlans();
        }

        [Test]
        public void DiagnosticTransportMatchesCompiledReceiptAndIgnoresUnrelatedInputs() {
            var rows = Rows();
            var document = ((Array)Call("Documents", rows, true)).GetValue(0);
            var content = (string)Call("Serialize", document);
            var args = new object[] { content, null };
            Assert.IsTrue((bool)Call("TryParse", args));
            Assert.AreEqual(content, Call("Serialize", args[1]));
            var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
            var receipt = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { document, content });
            Assert.AreEqual(1, Publisher.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Count(attribute => attribute.Key == "ME.BECS.SystemDependencyFragment.v1" && attribute.Value == receipt));
            var changed = rows.Reverse().Concat(new[] { "unrelated\t0\tdjE=" }).ToArray();
            Assert.AreEqual(content, Call("Serialize", ((Array)Call("Documents", changed, true)).GetValue(0)));
            document.GetType().GetField("Entries", Hidden).SetValue(document, Array.Empty<System.Collections.Generic.KeyValuePair<int, string>>());
            Assert.IsTrue((bool)Call("TryParse", Call("Serialize", document), null));
            Assert.IsEmpty((Array)Call("Documents", Array.Empty<string>(), false));
        }
    }
}
