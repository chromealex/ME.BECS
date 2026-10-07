namespace ME.BECS.Editor {
    using System;
    using System.IO;
    using System.Linq;
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
            var publication = Prepare(editor, content, out var path);
            UnityEditor.AssetDatabase.StartAssetEditing();
            try { SourceGeneratorSystemFragments.ApplyPublication(publication); }
            finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
            return path;
        }

        // Resumable variant for the sliced background publication: the Editor
        // thread is released (yield) while the workers prepare fragments/catalog.
        internal static System.Collections.IEnumerator PrepareSteps(SourceGeneratorInputManifest.StepResult<System.Collections.Generic.KeyValuePair<string, string>[]> output,
            bool editor, string content) {
            var path = InputPath(editor);
            var directory = Path.GetDirectoryName(path).Replace('\\', '/');
            using (CodeGeneratorTimings.Measure("Prepare: migration")) SourceGeneratorInputMigration.MoveFrameworkInputs(directory);
            var fragmentsTask = System.Threading.Tasks.Task.Run(() => SourceGeneratorSystemFragments.PrepareProfile(content, editor, directory));
            var catalogTask = System.Threading.Tasks.Task.Run(() => PrepareCatalog(content, editor, directory));
            System.Collections.Generic.KeyValuePair<string, string>[] bridges;
            using (CodeGeneratorTimings.Measure("Prepare: publication bridges")) bridges = SourceGeneratorPublicationBridges.PrepareUsed(content);
            while (!fragmentsTask.IsCompleted || !catalogTask.IsCompleted) yield return null;
            var files = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
            files.AddRange(bridges);
            files.AddRange(fragmentsTask.GetAwaiter().GetResult());
            files.AddRange(catalogTask.GetAwaiter().GetResult());
            if (!File.Exists(path) || File.ReadAllText(path) != content)
                files.Add(new System.Collections.Generic.KeyValuePair<string, string>(path, content));
            output.value = files.ToArray();
        }

        internal static System.Collections.Generic.KeyValuePair<string, string>[] Prepare(bool editor, string content, out string path) {
            path = InputPath(editor);
            var directory = Path.GetDirectoryName(path).Replace('\\', '/');
            using (CodeGeneratorTimings.Measure("Prepare: migration")) SourceGeneratorInputMigration.MoveFrameworkInputs(directory);
            // Owner fragments and the input catalog are pure text/filesystem work over
            // the ~20 MB manifest (no Unity API): prepare them on workers while the
            // Editor thread plans publication bridges, which need CompilationPipeline.
            var fragmentsTask = System.Threading.Tasks.Task.Run(() => SourceGeneratorSystemFragments.PrepareProfile(content, editor, directory));
            var catalogTask = System.Threading.Tasks.Task.Run(() => PrepareCatalog(content, editor, directory));
            System.Collections.Generic.KeyValuePair<string, string>[] bridges;
            using (CodeGeneratorTimings.Measure("Prepare: publication bridges")) bridges = SourceGeneratorPublicationBridges.PrepareUsed(content);
            System.Collections.Generic.KeyValuePair<string, string>[] fragments, catalog;
            using (CodeGeneratorTimings.Measure("Prepare: wait for fragments and catalog")) {
                // GetResult rethrows the original exception, not an AggregateException.
                fragments = fragmentsTask.GetAwaiter().GetResult();
                catalog = catalogTask.GetAwaiter().GetResult();
            }
            var files = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
            files.AddRange(bridges);
            files.AddRange(fragments);
            files.AddRange(catalog);
            if (!File.Exists(path) || File.ReadAllText(path) != content)
                files.Add(new System.Collections.Generic.KeyValuePair<string, string>(path, content));
            // Finish all preparation before the first write. A publication worker
            // can consume this data without resolving Unity assets or assemblies.
            return files.ToArray();
        }

        private static System.Collections.Generic.KeyValuePair<string, string>[] PrepareCatalog(string content, bool editor, string directory) {
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
            return pending.Where(pair => !File.Exists(pair.Key) || File.ReadAllText(pair.Key) != pair.Value).ToArray();
        }
    }
}
