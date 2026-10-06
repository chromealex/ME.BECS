namespace ME.BECS.Editor {
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using ME.BECS.CodeGeneration;

    internal static class SourceGeneratorInputTransport {
        internal const string ProjectDirectory = "Assets/ME.BECS.SourceInputs";
        internal static string FileName(bool editor) => editor ? SourceGeneratorInputFiles.Editor : SourceGeneratorInputFiles.Runtime;

        // Only preserve the consumer's existing project-wide compiler settings.
        // Native additional files need neither project paths nor cache hashes here.
        internal static string CompilerResponse(string path, string content) => "@Assets/csc.rsp\n";

        internal static string DirectoryForAnalyzer(string path) {
            // The framework may be renamed, relocated or installed read-only.
            // Compiler inputs always belong to the consuming Unity project.
            return ProjectDirectory;
        }

        internal static string InputPath(bool editor) {
            var analyzers = UnityEditor.AssetDatabase.FindAssets("l:RoslynAnalyzer")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path) == "ME.BECS.SourceGenerator.dll").ToArray();
            if (analyzers.Length != 1) throw new InvalidOperationException("Expected one installed ME.BECS source generator, found " + analyzers.Length + ".");
            return DirectoryForAnalyzer(analyzers[0]) + "/" + FileName(editor);
        }

        internal static string Publish(bool editor, string content) {
            var path = InputPath(editor);
            var directory = Path.GetDirectoryName(path).Replace('\\', '/');
            SourceGeneratorInputMigration.MoveFrameworkInputs(directory);
            SourceGeneratorPublicationBridges.PublishUsed(content);
            SourceGeneratorSystemFragments.Publish(content, editor, directory);
            SourceGeneratorSystemFragments.PublishSelection(content, editor, directory, types: true);
            SourceGeneratorSystemFragments.PublishEntities(content, editor, directory);
            SourceGeneratorSystemFragments.PublishAspects(content, editor, directory);
            SourceGeneratorSystemFragments.PublishDestroy(content, editor, directory);
            SourceGeneratorSystemFragments.PublishConfigs(content, editor, directory);
            SourceGeneratorSystemFragments.PublishNetwork(content, editor, directory);
            SourceGeneratorSystemFragments.PublishViews(content, editor, directory);
            SourceGeneratorSystemFragments.PublishViewSelection(content, editor, directory);
            SourceGeneratorSystemFragments.PublishSystemDependencies(content, editor, directory);
            SourceGeneratorSystemFragments.PublishThemeMenus(content, editor, directory);
            SourceGeneratorSystemFragments.PublishJobInit(content, editor, directory);
            SourceGeneratorSystemFragments.PublishJobSetup(content, editor, directory);
            SourceGeneratorSystemFragments.PublishJobDebug(content, editor, directory);
            SourceGeneratorSystemFragments.PublishGraphs(content, editor, directory);
            SourceGeneratorSystemFragments.PublishBootstrap(content, editor, directory);
            PublishCatalog(content, editor, directory);
            Directory.CreateDirectory(directory);
            if (!File.Exists(path) || File.ReadAllText(path) != content) {
                File.WriteAllText(path, content, new UTF8Encoding(false));
                UnityEditor.AssetDatabase.ImportAsset(path);
            }
            return path;
        }

        private static void PublishCatalog(string content, bool editor, string directory) {
            var document = SourceGeneratorInputCatalogFormat.Document(content, editor);
            directory += "/InputCatalogs";
            var path = directory + "/" + SourceGeneratorInputCatalogFormat.FileName(document.Owner, editor);
            var pending = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal) {
                { path, SourceGeneratorInputCatalogFormat.Serialize(document) },
            };
            if (Directory.Exists(directory)) foreach (var previousPath in Directory.EnumerateFiles(directory, "*" + (editor ? "Editor" : "Runtime") + SourceGeneratorSystemFragmentFormat.NativeSuffix)) {
                var normalized = previousPath.Replace('\\', '/');
                if (pending.ContainsKey(normalized)) continue;
                if (!SourceGeneratorInputCatalogFormat.TryParse(File.ReadAllText(previousPath), out var previous))
                    throw new InvalidOperationException("Cannot identify previous input catalog: " + previousPath);
                previous.Entries = Array.Empty<System.Collections.Generic.KeyValuePair<int, string>>();
                pending.Add(normalized, SourceGeneratorInputCatalogFormat.Serialize(previous));
            }
            foreach (var pair in pending) {
                if (File.Exists(pair.Key) && File.ReadAllText(pair.Key) == pair.Value) continue;
                Directory.CreateDirectory(directory);
                File.WriteAllText(pair.Key, pair.Value, new UTF8Encoding(false));
                UnityEditor.AssetDatabase.ImportAsset(pair.Key);
            }
        }
    }
}
