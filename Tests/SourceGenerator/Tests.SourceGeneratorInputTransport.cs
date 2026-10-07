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
        public async System.Threading.Tasks.Task PublicationFileWriterWorksOnWorkerWithoutAssetImport(bool withProgress) {
            var writer = Files.Assembly.GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true)
                .GetMethod(withProgress ? "WritePublicationFilesWithProgress" : "WritePublicationFiles", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(writer);
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BECS-publication-write-" + Guid.NewGuid().ToString("N"));
            var first = System.IO.Path.Combine(directory, "nested", "first.txt");
            var second = System.IO.Path.Combine(directory, "second.txt");
            var files = new[] {
                new System.Collections.Generic.KeyValuePair<string, string>(first, "данные\n"),
                new System.Collections.Generic.KeyValuePair<string, string>(second, "manifest\n"),
            };
            try {
                var completed = new System.Collections.Generic.List<int>();
                Action<int> report = count => {
                    Assert.AreEqual(files[count - 1].Value, System.IO.File.ReadAllText(files[count - 1].Key),
                        "Progress must describe a completed write.");
                    completed.Add(count);
                };
                await System.Threading.Tasks.Task.Run(() => writer.Invoke(null,
                    withProgress ? new object[] { files, report } : new object[] { files }));
                CollectionAssert.AreEqual(withProgress ? new[] { 1, 2 } : Array.Empty<int>(), completed);
                foreach (var file in files) {
                    CollectionAssert.AreEqual(new System.Text.UTF8Encoding(false).GetBytes(file.Value), System.IO.File.ReadAllBytes(file.Key));
                    Assert.IsFalse(System.IO.File.Exists(file.Key + ".meta"));
                }
            } finally {
                if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PublicationPreparationDoesNotCreateDestinationFiles(bool editor) {
            var content = System.IO.File.ReadAllText(Call<string>(Transport, "InputPath", editor));
            var destination = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BECS-publication-plan-" + Guid.NewGuid().ToString("N"));
            var fragments = Files.Assembly.GetType("ME.BECS.Editor.SourceGeneratorSystemFragments", true);
            var first = Call<System.Collections.Generic.KeyValuePair<string, string>[]>(fragments, "PrepareProfile", content, editor, destination);
            var catalog = Call<System.Collections.Generic.KeyValuePair<string, string>[]>(Transport, "PrepareCatalog", content, editor, destination);
            Assert.IsNotEmpty(first);
            Assert.IsNotEmpty(catalog);
            Assert.IsFalse(System.IO.Directory.Exists(destination), "Preparation must not write or import assets.");
            CollectionAssert.AreEqual(first,
                Call<System.Collections.Generic.KeyValuePair<string, string>[]>(fragments, "PrepareProfile", content, editor, destination));
            var paths = first.Concat(catalog).Select(pair => pair.Key).ToArray();
            Assert.AreEqual(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
            foreach (var path in paths) StringAssert.StartsWith(destination.Replace('\\', '/') + "/", path.Replace('\\', '/'));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BootstrapUsesProjectOwnedNativeInputsAndCurrentContent(bool editor) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(editor);
            var values = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.InputTransport.v1").Select(attribute => attribute.Value).ToArray();
            CollectionAssert.AreEqual(new[] { "native-additionalfile-v2" }, values);
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
            Assert.IsTrue((files ?? Array.Empty<string>()).Any(path =>
                path.EndsWith(".ME.BECS.SourceGenerator.additionalfile", StringComparison.Ordinal)), assemblyName);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CatalogOwnerReceivesNativeInputsWithoutAnAggregateHost(bool editor) {
            var owner = Tests_SourceGeneratorInputCatalog.Owner(editor).GetName().Name;
            var format = Files.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorInputCatalogFormat", true);
            var expected = Call<string>(format, "FileName", owner, editor);
            var compilation = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor)
                .Single(assembly => assembly.name == owner);
            CollectionAssert.Contains((compilation.compilerOptions.RoslynAdditionalFilePaths ?? Array.Empty<string>())
                .Select(System.IO.Path.GetFileName).ToArray(), expected);
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
        public void SystemFragmentFileNamesDistinguishCompilationOwners(string assemblyName) {
            var format = Files.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
            foreach (var editor in new[] { false, true }) {
                Assert.AreNotEqual(Call<string>(format, "FileName", assemblyName, editor),
                    Call<string>(format, "FileName", assemblyName + ".Other", editor));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NativeSystemFragmentsAreProfileSpecificAndExcludeFullSnapshots(bool editor) {
            var format = Files.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
            var name = Call<string>(format, "FileName", "Gameplay", editor);
            Assert.AreNotEqual(name, Call<string>(format, "FileName", "Gameplay", !editor));
            Assert.IsTrue(Call<bool>(format, "IsInput", "Assets/Inputs/" + name));
            Assert.IsFalse(Call<bool>(format, "IsInput", "Assets/Inputs/" + Call<string>(Transport, "FileName", editor)));
            Assert.IsFalse(Call<bool>(format, "IsInput", "Temp/Export.becs-inputs"));
            Assert.IsFalse(Call<bool>(format, "IsInput", "Assets/Other.ME.BECS.SourceGenerator.additionalfile"));
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
