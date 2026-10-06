using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorInputTransport {
        private static Type Files => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorInputFiles", true);
        private static Type Transport => Files.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputTransport", true);
        private static T Call<T>(Type type, string method, params object[] arguments) =>
            (T)type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);

        [TestCase(false)]
        [TestCase(true)]
        public void BootstrapUsesProjectOwnedNativeInputsAndCurrentContent(bool editor) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(editor);
            var values = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.InputTransport.v1").Select(attribute => attribute.Value).ToArray();
            CollectionAssert.AreEqual(new[] { "native-additionalfile" }, values);
            var path = Call<string>(Transport, "InputPath", editor);
            StringAssert.StartsWith("Assets/ME.BECS.SourceInputs/", path);
            var names = Files.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true);
            var hash = Call<string>(names, "Hash", System.IO.File.ReadAllText(path));
            var compiled = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.InputContentHash.v1").Select(attribute => attribute.Value).ToArray();
            CollectionAssert.AreEqual(new[] { hash }, compiled, "The compiled input must match the project asset without a define in csc.rsp.");
        }

        [TestCase("ME.BECS")]
        [TestCase("ME.BECS.Tests")]
        public void UnityDiscoversProjectInputsWithoutInjectedResponseArguments(string assemblyName) {
            var compilation = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor)
                .Single(assembly => assembly.name == assemblyName);
            var files = compilation.compilerOptions.RoslynAdditionalFilePaths;
            Assert.IsTrue((files ?? Array.Empty<string>()).Any(path => Call<bool>(Files, "IsNative", path)), assemblyName);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CatalogOwnerReceivesNativeInputsWithoutAnAggregateHost(bool editor) {
            UnityDiscoversProjectInputsWithoutInjectedResponseArguments(Tests_SourceGeneratorInputCatalog.Owner(editor).GetName().Name);
        }

        [Test]
        public void ExportPublishesInputsWithoutWritingAggregateHostsOrCompilerResponses() {
            var exporter = Files.Assembly.GetType("ME.BECS.Editor.CodeGenerator", true)
                .GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic);
            var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(exporter)
                .Where(instruction => instruction.Operand is MethodInfo).Select(instruction => (MethodInfo)instruction.Operand).ToArray();
            Assert.AreEqual(1, calls.Count(method => method.DeclaringType == Transport && method.Name == "Publish"));
            Assert.IsFalse(calls.Any(method => method.DeclaringType == typeof(System.IO.File) || method.DeclaringType == typeof(System.IO.Directory)));
            Assert.IsFalse(calls.Any(method => method.Name == "CompilerResponse" || method.Name == "GetRetiredFileNames" || method.Name == "GetAssemblyReferenceNames"));
            Assert.IsFalse(exporter.GetParameters().Any(parameter => parameter.Name == "dir" || parameter.Name == "asms"));
        }

        [TestCase("ME.BECS")]
        [TestCase("ME.BECS.Tests")]
        [TestCase("Gameplay")]
        public void UnrelatedAssembliesDoNotParseAggregateInputs(string assemblyName) {
            foreach (var editor in new[] { false, true }) {
                var name = Call<string>(Transport, "FileName", editor);
                Assert.IsFalse(Call<bool>(Files, "TargetsCompilation", "Assets/Inputs/" + name, assemblyName));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NativeInputRoutingIsSharedAndProfileSpecific(bool editor) {
            var path = "Assets/Inputs/" + Call<string>(Transport, "FileName", editor);
            Assert.IsFalse(Call<bool>(Files, "IsScoped", path));
            Assert.IsTrue(Call<bool>(Files, "IsNative", path));
            Assert.IsTrue(Call<bool>(Files, "IsInput", path));
            Assert.IsTrue(Call<bool>(Files, "TargetsCompilation", path, "ME.BECS.Gen." + (editor ? "Editor" : "Runtime")));
            Assert.IsFalse(Call<bool>(Files, "TargetsCompilation", path, "ME.BECS.Gen." + (editor ? "Runtime" : "Editor")));
            Assert.IsTrue(Call<bool>(Files, "IsInput", "Temp/Export.becs-inputs"), "Read compatibility for diagnostics/upgrade remains.");
            Assert.IsFalse(Call<bool>(Files, "IsInput", "Assets/Other.ME.BECS.SourceGenerator.additionalfile"));
        }

        [TestCase("Assets/ME.BECS/SourceGenerator/ME.BECS.SourceGenerator.dll", "Assets/ME.BECS.SourceInputs")]
        [TestCase("Assets/Vendor/Framework/ME.BECS.SourceGenerator.dll", "Assets/ME.BECS.SourceInputs")]
        [TestCase("Packages/framework/ME.BECS.SourceGenerator.dll", "Assets/ME.BECS.SourceInputs")]
        [TestCase("Library/PackageCache/framework/ME.BECS.SourceGenerator.dll", "Assets/ME.BECS.SourceInputs")]
        public void InputLocationSupportsRelocatedFrameworkAndReadOnlyPackages(string analyzer, string expected) {
            Assert.AreEqual(expected, Call<string>(Transport, "DirectoryForAnalyzer", analyzer));
        }

        [Test]
        public void CompilerArgumentsContainNeitherProjectInputsNorHashes() {
            const string path = "Assets/Framework With Spaces/RuntimeInputs.becs-inputs";
            var first = Call<string>(Transport, "CompilerResponse", path, "first contents");
            Assert.AreEqual(first, Call<string>(Transport, "CompilerResponse", path, "first contents"));
            Assert.AreEqual(first, Call<string>(Transport, "CompilerResponse", path, "changed contents"));
            Assert.AreEqual(first, Call<string>(Transport, "CompilerResponse", "/different/machine/RenamedFramework/input", "other project"));
            Assert.AreEqual("@Assets/csc.rsp\n", first);
        }
    }
}
