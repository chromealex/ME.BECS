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
            SourceGeneratorSystemFragments.PublishJobInit(content, editor, directory);
            SourceGeneratorSystemFragments.PublishJobSetup(content, editor, directory);
            SourceGeneratorSystemFragments.PublishJobDebug(content, editor, directory);
            SourceGeneratorSystemFragments.PublishGraphs(content, editor, directory);
            Directory.CreateDirectory(directory);
            if (!File.Exists(path) || File.ReadAllText(path) != content) {
                File.WriteAllText(path, content, new UTF8Encoding(false));
                UnityEditor.AssetDatabase.ImportAsset(path);
            }
            return path;
        }
    }
}
