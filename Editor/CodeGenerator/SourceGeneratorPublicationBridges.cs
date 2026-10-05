namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Names = ME.BECS.CodeGeneration.SourceGeneratorNames;

    // Project-owned compilation hosts, not a second source emitter. Only a fixed
    // comment creates the Unity compilation; Roslyn owns every executable body.
    internal static class SourceGeneratorPublicationBridges {
        internal const string Prefix = "ME.BECS.SourceInputs.Bridge.";
        internal const string DirectoryName = SourceGeneratorInputTransport.ProjectDirectory + "/Bridges";
        internal const string Anchor = "// ME.BECS publication bridge. Executable code is emitted by the source generator.\n";
        private const string Schema = "ME.BECS.PublicationBridge.v1";
        internal static bool IsBridge(string name) => name != null && name.StartsWith(Prefix, StringComparison.Ordinal);

        [Serializable] internal sealed class VersionDefine {
            public string name, expression, define;
        }
        [Serializable] internal sealed class Definition {
            public string name;
            public string[] references = Array.Empty<string>();
            public string[] includePlatforms = Array.Empty<string>(), excludePlatforms = Array.Empty<string>();
            public bool allowUnsafeCode = true, overrideReferences = true, autoReferenced = false, noEngineReferences = false;
            public string[] precompiledReferences = Array.Empty<string>(), defineConstraints = Array.Empty<string>();
            public string[] optionalUnityReferences = Array.Empty<string>();
            public VersionDefine[] versionDefines = Array.Empty<VersionDefine>();
        }
        [Serializable] internal sealed class Receipt {
            public string schema, name, contentHash;
            public bool editor;
            public string[] required;
        }
        internal sealed class Plan {
            internal string Name, Content;
            internal bool Editor;
            internal string[] Required;
            internal string Folder => DirectoryName + "/" + this.Name.Substring(Prefix.Length);
        }

        private sealed class Planning : IDisposable {
            internal readonly Dictionary<string, Plan> plans = new Dictionary<string, Plan>(StringComparer.Ordinal);
            internal readonly Dictionary<bool, (string[] definitions, string[] implicitReferences)> inventories =
                new Dictionary<bool, (string[], string[])>();
            private readonly Planning previous;
            internal Planning() { this.previous = current; if (current == null) current = this; }
            public void Dispose() { if (current == this) current = this.previous; }
        }
        private static Planning current;
        internal static IDisposable BeginPlanning() => new Planning();
        private static string[] Sorted(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static string PlanName(IEnumerable<string> required, bool editor) => Prefix + (editor ? "Editor_" : "Runtime_") +
            Names.Hash(Schema + "\n" + string.Join("\n", Sorted(required)));

        internal static string Select(string[] required, bool editor) => GetPlan(required, editor).Name;

        private static Plan GetPlan(string[] required, bool editor) {
            var name = PlanName(required, editor);
            if (current != null && current.plans.TryGetValue(name, out var cached)) return cached;
            if (current == null || !current.inventories.TryGetValue(editor, out var inventory)) {
                inventory = Inventory(editor);
                if (current != null) current.inventories.Add(editor, inventory);
            }
            var plan = CreatePlan(required, editor, inventory.definitions, inventory.implicitReferences);
            if (current != null) current.plans.Add(name, plan);
            return plan;
        }

        private static (string[] definitions, string[] implicitReferences) Inventory(bool editor) {
            var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(editor
                ? UnityEditor.Compilation.AssembliesType.Editor : UnityEditor.Compilation.AssembliesType.Player)
                .Where(assembly => !SourceGeneratorCodeIdentity.IsConsumer(assembly.name)).ToArray();
            var plugins = new HashSet<string>(UnityEditor.PluginImporter.GetAllImporters()
                .Select(importer => Path.GetFileName(importer.assetPath)).Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
            var definitions = new System.Collections.Generic.List<string>();
            var implicitReferences = new HashSet<string>(StringComparer.Ordinal);
            var scripts = new HashSet<string>(assemblies.Select(assembly => assembly.name), StringComparer.Ordinal);
            foreach (var assembly in assemblies) {
                var path = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                // A named asmdef cannot reference predefined Assembly-CSharp. Do
                // not invent one or mutate the user's compilation boundaries.
                if (string.IsNullOrEmpty(path)) continue;
                var definition = UnityEngine.JsonUtility.FromJson<Definition>(File.ReadAllText(path));
                if (definition == null || definition.name != assembly.name) throw new InvalidOperationException("Invalid publication dependency asmdef: " + path);
                definition.references = assembly.assemblyReferences.Select(reference => reference.name).ToArray();
                var compiled = assembly.compiledAssemblyReferences.Where(reference => !scripts.Contains(Path.GetFileNameWithoutExtension(reference))).ToArray();
                definition.precompiledReferences = Sorted((definition.precompiledReferences ?? Array.Empty<string>())
                    .Concat(compiled.Where(reference => plugins.Contains(Path.GetFileName(reference))).Select(Path.GetFileName)));
                foreach (var reference in compiled.Where(reference => !plugins.Contains(Path.GetFileName(reference))))
                    implicitReferences.Add(Path.GetFileNameWithoutExtension(reference));
                definitions.Add(UnityEngine.JsonUtility.ToJson(definition));
            }
            return (definitions.ToArray(), Sorted(implicitReferences));
        }

        // Pure planner: no AssetDatabase calls or writes. Dependencies are already
        // resolved from GUIDs to actual compiler assembly names by Inventory.
        internal static Plan CreatePlan(string[] required, bool editor, string[] definitions, string[] implicitReferences) {
            var byName = definitions.Select(UnityEngine.JsonUtility.FromJson<Definition>).ToDictionary(item => item.name, StringComparer.Ordinal);
            var implicitNames = new HashSet<string>(implicitReferences, StringComparer.Ordinal);
            var plugins = new HashSet<string>(byName.Values.SelectMany(item => item.precompiledReferences ?? Array.Empty<string>()), StringComparer.Ordinal);
            var selected = new Dictionary<string, Definition>(StringComparer.Ordinal);
            void Visit(string name) {
                if (SourceGeneratorCodeIdentity.IsConsumer(name)) throw new InvalidOperationException("Publication bridges cannot depend on generated consumers: " + name);
                if (selected.ContainsKey(name)) return;
                if (!byName.TryGetValue(name, out var definition)) {
                    if (implicitNames.Contains(name) || plugins.Contains(name + ".dll")) return;
                    throw new InvalidOperationException("No active asmdef or compiler reference for publication bridge dependency: " + name);
                }
                selected.Add(name, definition);
                // C# references are not transitive. Include the concrete surface
                // needed to bind imported generic constraints, bases and wrappers.
                foreach (var reference in definition.references ?? Array.Empty<string>()) Visit(reference);
            }
            foreach (var name in Sorted(required)) Visit(name);
            if (selected.Count == 0) throw new InvalidOperationException("A publication bridge needs at least one script dependency.");
            var output = new Definition { name = PlanName(required, editor), references = Sorted(selected.Keys) };
            HashSet<string> platforms = editor ? new HashSet<string>(new[] { "Editor" }, StringComparer.Ordinal) : null;
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            var constraints = new HashSet<string>(StringComparer.Ordinal);
            var versions = new System.Collections.Generic.List<VersionDefine>();
            foreach (var definition in selected.Values.OrderBy(item => item.name, StringComparer.Ordinal)) {
                var includes = definition.includePlatforms ?? Array.Empty<string>();
                if (includes.Length != 0) {
                    if (platforms == null) platforms = new HashSet<string>(includes, StringComparer.Ordinal);
                    else platforms.IntersectWith(includes);
                }
                excluded.UnionWith(definition.excludePlatforms ?? Array.Empty<string>());
                var localVersions = (definition.versionDefines ?? Array.Empty<VersionDefine>()).GroupBy(item => item.define)
                    .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
                var used = new HashSet<string>(StringComparer.Ordinal);
                foreach (var constraint in definition.defineConstraints ?? Array.Empty<string>()) {
                    constraints.Add(Regex.Replace(constraint, "[A-Za-z_][A-Za-z0-9_]*", match => {
                        if (!localVersions.ContainsKey(match.Value)) return match.Value;
                        used.Add(match.Value);
                        // Version defines belong to their own asmdef. The same
                        // symbol in another dependency may mean another package.
                        return "BECS_BRIDGE_" + Names.Hash(definition.name + "\n" + match.Value);
                    }));
                }
                foreach (var name in Sorted(used)) foreach (var version in localVersions[name])
                    versions.Add(new VersionDefine { name = version.name, expression = version.expression,
                        define = "BECS_BRIDGE_" + Names.Hash(definition.name + "\n" + name) });
            }
            if (platforms != null) {
                platforms.ExceptWith(excluded);
                if (platforms.Count == 0) throw new InvalidOperationException("Publication bridge dependencies have no common platform.");
                if (!editor && platforms.SetEquals(new[] { "Editor" })) throw new InvalidOperationException("A Runtime publication cannot depend on Editor-only assemblies.");
                output.includePlatforms = Sorted(platforms);
            } else output.excludePlatforms = Sorted(excluded);
            output.defineConstraints = Sorted(constraints);
            output.versionDefines = versions.OrderBy(item => item.define, StringComparer.Ordinal).ThenBy(item => item.name, StringComparer.Ordinal)
                .ThenBy(item => item.expression, StringComparer.Ordinal).ToArray();
            output.optionalUnityReferences = Sorted(selected.Values.SelectMany(item => item.optionalUnityReferences ?? Array.Empty<string>()));
            output.precompiledReferences = Sorted(selected.Values.SelectMany(item => item.precompiledReferences ?? Array.Empty<string>())
                .Concat(required.Where(name => plugins.Contains(name + ".dll")).Select(name => name + ".dll")));
            return new Plan { Name = output.name, Content = UnityEngine.JsonUtility.ToJson(output, true) + "\n", Editor = editor, Required = Sorted(required) };
        }

        internal static void PublishUsed(string manifest) {
            var owners = manifest.Split('\n').Select(row => row.Split('\t'))
                .Where(fields => fields.Length == 4 && fields[0].EndsWith("-registration-owner", StringComparison.Ordinal))
                .Select(fields => CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(fields[3])).Where(IsBridge).Distinct(StringComparer.Ordinal);
            var plans = new System.Collections.Generic.List<Plan>();
            foreach (var owner in owners.OrderBy(name => name, StringComparer.Ordinal)) {
                if (current == null || !current.plans.TryGetValue(owner, out var plan)) {
                    // A reused feeder did not execute the planner this time. Its
                    // owned receipt retains the requirements, not generated code.
                    var folder = DirectoryName + "/" + owner.Substring(Prefix.Length);
                    var receiptPath = folder + "/Bridge.becs-owner";
                    if (!File.Exists(receiptPath)) throw new InvalidOperationException("Missing publication bridge plan: " + owner);
                    var receipt = UnityEngine.JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath));
                    if (receipt?.schema != Schema || receipt.name != owner || receipt.required == null)
                        throw new InvalidOperationException("Invalid publication bridge receipt: " + receiptPath);
                    plan = GetPlan(receipt.required, receipt.editor);
                    if (plan.Name != owner) throw new InvalidOperationException("Publication bridge identity changed; regenerate inputs: " + owner);
                }
                ValidateOwned(plan);
                plans.Add(plan);
            }
            if (plans.Count == 0) return;
            UnityEditor.AssetDatabase.StartAssetEditing();
            try {
                foreach (var plan in plans) {
                    Directory.CreateDirectory(plan.Folder);
                    WriteChanged(plan.Folder + "/AssemblyMarker.cs", Anchor);
                    WriteChanged(plan.Folder + "/" + plan.Name + ".asmdef", plan.Content);
                    WriteChanged(plan.Folder + "/Bridge.becs-owner", UnityEngine.JsonUtility.ToJson(new Receipt {
                        schema = Schema, name = plan.Name, contentHash = Names.Hash(plan.Content), editor = plan.Editor, required = plan.Required,
                    }, true) + "\n");
                }
            } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
        }

        private static void ValidateOwned(Plan plan) {
            var files = new[] { plan.Folder + "/AssemblyMarker.cs", plan.Folder + "/" + plan.Name + ".asmdef", plan.Folder + "/Bridge.becs-owner" };
            if (!files.Any(File.Exists)) {
                if (Directory.Exists(plan.Folder) && Directory.EnumerateFileSystemEntries(plan.Folder).Any())
                    throw new InvalidOperationException("Publication bridge destination is not empty; preserving its contents: " + plan.Folder);
                // A user-defined assembly with the same name in another folder
                // must never be shadowed by a generated project asset.
                if (!string.IsNullOrEmpty(UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(plan.Name)))
                    throw new InvalidOperationException("Publication bridge assembly name already belongs to another asset: " + plan.Name);
                return;
            }
            if (!files.All(File.Exists) || File.ReadAllText(files[0]) != Anchor)
                throw new InvalidOperationException("Modified or incomplete publication bridge; preserving existing files: " + plan.Folder);
            var receipt = UnityEngine.JsonUtility.FromJson<Receipt>(File.ReadAllText(files[2]));
            if (receipt?.schema != Schema || receipt.name != plan.Name || receipt.contentHash != Names.Hash(File.ReadAllText(files[1])))
                throw new InvalidOperationException("Publication bridge asmdef was modified outside its exporter; preserving it: " + files[1]);
        }

        private static void WriteChanged(string path, string text) {
            if (File.Exists(path) && File.ReadAllText(path) == text) return;
            File.WriteAllText(path, text, new UTF8Encoding(false));
            UnityEditor.AssetDatabase.ImportAsset(path);
        }
    }
}
