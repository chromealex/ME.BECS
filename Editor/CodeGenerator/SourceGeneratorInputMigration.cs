namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ME.BECS.CodeGeneration;
    using Format = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

    // One-way upgrade of the previous transport. Production export never writes
    // project data to a framework/package directory or injects compiler options.
    internal static class SourceGeneratorInputMigration {
        internal static string WithoutManagedOptions(string response) {
            response = SourceGeneratorSystemFragments.RemoveManagedResponse(response,
                "#\"ME.BECS system fragments begin\"", "#\"ME.BECS system fragments end\"");
            return SourceGeneratorSystemFragments.RemoveManagedResponse(response,
                "#\"ME.BECS type fragments begin\"", "#\"ME.BECS type fragments end\"");
        }

        internal static void MoveFrameworkInputs(string destination) {
            var analyzers = UnityEditor.AssetDatabase.FindAssets("l:RoslynAnalyzer").Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path) == "ME.BECS.SourceGenerator.dll").ToArray();
            if (analyzers.Length != 1) throw new InvalidOperationException("Expected one BECS analyzer for input migration.");
            var origin = Path.GetDirectoryName(analyzers[0]).Replace('\\', '/');
            if (!origin.StartsWith("Assets/", StringComparison.Ordinal) || origin == destination) return;
            var moves = new System.Collections.Generic.List<KeyValuePair<string, string>>();
            foreach (var editor in new[] { false, true }) {
                var old = origin + "/" + (editor ? SourceGeneratorInputFiles.ScopedEditor : SourceGeneratorInputFiles.ScopedRuntime);
                if (File.Exists(old)) moves.Add(new KeyValuePair<string, string>(old, destination + "/" + SourceGeneratorInputTransport.FileName(editor)));
            }
            foreach (var types in new[] { false, true }) {
                var folder = origin + (types ? "/TypeFragments" : "/SystemFragments");
                if (!Directory.Exists(folder)) continue;
                foreach (var path in Directory.EnumerateFiles(folder, "*" + (types ? SourceGeneratorTypeFragmentFormat.Extension : Format.Extension))) {
                    var content = File.ReadAllText(path);
                    Format.Document doc;
                    if (!(types ? SourceGeneratorTypeFragmentFormat.TryParse(content, out doc) : Format.TryParse(content, out doc)))
                        throw new InvalidOperationException("Cannot migrate unrecognized registration data: " + path);
                    moves.Add(new KeyValuePair<string, string>(path.Replace('\\', '/'), destination + (types ? "/TypeFragments/" : "/SystemFragments/") +
                        (types ? SourceGeneratorTypeFragmentFormat.FileName(doc.Owner, doc.Editor) : Format.FileName(doc.Owner, doc.Editor))));
                }
            }
            if (moves.Count == 0) return;
            // Resolve every target and response patch before touching any asset.
            foreach (var move in moves) if (File.Exists(move.Value))
                throw new InvalidOperationException("Input migration destination already exists; preserving both copies: " + move.Value);
            var responses = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor)) {
                // Do not inspect generated consumers. Their ordinary producer
                // retires the old aggregate response arguments after publication.
                if (assembly.name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) continue;
                var folder = SourceGeneratorSystemFragments.ResponseDirectory(assembly.name);
                if (folder == null) continue;
                var path = folder + "/csc.rsp";
                if (!File.Exists(path)) continue;
                var previous = File.ReadAllText(path);
                var updated = WithoutManagedOptions(previous);
                if (updated != previous) responses[path] = updated;
            }
            foreach (var folder in moves.Select(move => Path.GetDirectoryName(move.Value).Replace('\\', '/')).Distinct()) EnsureFolder(folder);
            UnityEditor.AssetDatabase.StartAssetEditing();
            try {
                // Moves preserve the existing data and Unity GUIDs, not delete/recreate.
                foreach (var move in moves) {
                    var error = UnityEditor.AssetDatabase.MoveAsset(move.Key, move.Value);
                    if (error.Length != 0) throw new InvalidOperationException("Cannot migrate " + move.Key + ": " + error);
                }
                foreach (var pair in responses) {
                    File.WriteAllText(pair.Key, pair.Value, new System.Text.UTF8Encoding(false));
                    UnityEditor.AssetDatabase.ImportAsset(pair.Key);
                }
            } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
            UnityEngine.Debug.Log("[ME.BECS] Moved " + moves.Count + " project compiler inputs out of the framework; preserved asset GUIDs and removed managed options from " + responses.Count + " response files.");
        }

        private static void EnsureFolder(string path) {
            if (UnityEditor.AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidOperationException("Not a project input folder: " + path);
            EnsureFolder(parent);
            if (Directory.Exists(path)) UnityEditor.AssetDatabase.ImportAsset(path);
            else if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new InvalidOperationException("Cannot create project input folder: " + path);
        }
    }
}
