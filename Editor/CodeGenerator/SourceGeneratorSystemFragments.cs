namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using ME.BECS.CodeGeneration;
    using Format = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

    internal static class SourceGeneratorSystemFragments {
        private enum SelectionKind { System, Type, Entity, Aspect, Destroy, Config, Network, Views, JobInit, JobSetup, JobDebug, Graph }
        private static Format.Document[] SelectionDocuments(IEnumerable<string> rows, bool editor, SelectionKind kind) =>
            kind == SelectionKind.Graph ? SourceGeneratorGraphFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.JobDebug ? SourceGeneratorJobDebugFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.JobSetup ? SourceGeneratorJobSetupFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.JobInit ? SourceGeneratorJobInitFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.Views ? SourceGeneratorViewsFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.Network ? SourceGeneratorNetworkFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.Config ? SourceGeneratorConfigFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.Destroy ? SourceGeneratorDestroyFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.Aspect ? SourceGeneratorAspectFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.Entity ? SourceGeneratorEntityFragmentFormat.Documents(rows, editor) :
            kind == SelectionKind.Type ? SourceGeneratorTypeFragmentFormat.Documents(rows, editor) : Documents(rows, editor);
        private static string SelectionFileName(string owner, bool editor, SelectionKind kind) =>
            kind == SelectionKind.Graph ? SourceGeneratorGraphFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.JobDebug ? SourceGeneratorJobDebugFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.JobSetup ? SourceGeneratorJobSetupFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.JobInit ? SourceGeneratorJobInitFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.Views ? SourceGeneratorViewsFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.Network ? SourceGeneratorNetworkFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.Config ? SourceGeneratorConfigFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.Destroy ? SourceGeneratorDestroyFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.Aspect ? SourceGeneratorAspectFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.Entity ? SourceGeneratorEntityFragmentFormat.FileName(owner, editor) :
            kind == SelectionKind.Type ? SourceGeneratorTypeFragmentFormat.FileName(owner, editor) : Format.FileName(owner, editor);
        private static string SelectionSerialize(Format.Document document, SelectionKind kind) =>
            kind == SelectionKind.Graph ? SourceGeneratorGraphFragmentFormat.Serialize(document) :
            kind == SelectionKind.JobDebug ? SourceGeneratorJobDebugFragmentFormat.Serialize(document) :
            kind == SelectionKind.JobSetup ? SourceGeneratorJobSetupFragmentFormat.Serialize(document) :
            kind == SelectionKind.JobInit ? SourceGeneratorJobInitFragmentFormat.Serialize(document) :
            kind == SelectionKind.Views ? SourceGeneratorViewsFragmentFormat.Serialize(document) :
            kind == SelectionKind.Network ? SourceGeneratorNetworkFragmentFormat.Serialize(document) :
            kind == SelectionKind.Config ? SourceGeneratorConfigFragmentFormat.Serialize(document) :
            kind == SelectionKind.Destroy ? SourceGeneratorDestroyFragmentFormat.Serialize(document) :
            kind == SelectionKind.Aspect ? SourceGeneratorAspectFragmentFormat.Serialize(document) :
            kind == SelectionKind.Entity ? SourceGeneratorEntityFragmentFormat.Serialize(document) :
            kind == SelectionKind.Type ? SourceGeneratorTypeFragmentFormat.Serialize(document) : Format.Serialize(document);
        private static bool SelectionParse(string content, SelectionKind kind, out Format.Document document) =>
            kind == SelectionKind.Graph ? SourceGeneratorGraphFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.JobDebug ? SourceGeneratorJobDebugFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.JobSetup ? SourceGeneratorJobSetupFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.JobInit ? SourceGeneratorJobInitFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.Views ? SourceGeneratorViewsFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.Network ? SourceGeneratorNetworkFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.Config ? SourceGeneratorConfigFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.Destroy ? SourceGeneratorDestroyFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.Aspect ? SourceGeneratorAspectFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.Entity ? SourceGeneratorEntityFragmentFormat.TryParse(content, out document) :
            kind == SelectionKind.Type ? SourceGeneratorTypeFragmentFormat.TryParse(content, out document) : Format.TryParse(content, out document);
        private static string SelectionMetadata(SelectionKind kind) => kind == SelectionKind.Graph ? SourceGeneratorGraphFragmentFormat.MetadataKey :
            kind == SelectionKind.JobDebug ? SourceGeneratorJobDebugFragmentFormat.MetadataKey :
            kind == SelectionKind.JobSetup ? SourceGeneratorJobSetupFragmentFormat.MetadataKey :
            kind == SelectionKind.JobInit ? SourceGeneratorJobInitFragmentFormat.MetadataKey :
            kind == SelectionKind.Views ? SourceGeneratorViewsFragmentFormat.MetadataKey :
            kind == SelectionKind.Network ? SourceGeneratorNetworkFragmentFormat.MetadataKey :
            kind == SelectionKind.Config ? SourceGeneratorConfigFragmentFormat.MetadataKey :
            kind == SelectionKind.Destroy ? SourceGeneratorDestroyFragmentFormat.MetadataKey :
            kind == SelectionKind.Aspect ? SourceGeneratorAspectFragmentFormat.MetadataKey :
            kind == SelectionKind.Entity ? SourceGeneratorEntityFragmentFormat.MetadataKey :
            kind == SelectionKind.Type ? SourceGeneratorTypeFragmentFormat.MetadataKey : Format.MetadataKey;
        internal static Format.Document[] Documents(IEnumerable<string> rows, bool editor) {
            // Compiled assembly attributes need not be enumerated in source order.
            // Only the explicitly exported ordinal determines the selection.
            var selection = rows.Where(row => row.StartsWith("system-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var plan = Format.Plan(selection);
            return selection.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Invalid ordered system publication selection.");
                return (Owner: Format.Decode(fields[3]), Entry: new KeyValuePair<int, string>(ordinal, Format.Decode(fields[2])));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Format.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = selection.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }

        internal static string ResponseDirectory(string assembly) {
            var path = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly)?.Replace('\\', '/');
            // Package owners can publish through an eligible project assembly.
            // Never mutate PackageCache or implicitly use the global Assets rsp
            // (which would invalidate every compilation for a local selection).
            return path != null && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                !path.StartsWith("Assets/ME.BECS.Gen/", StringComparison.Ordinal) ? Path.GetDirectoryName(path).Replace('\\', '/') : null;
        }

        internal static string RemoveManagedResponse(string original, string begin, string end) {
            var first = original.IndexOf(begin, StringComparison.Ordinal);
            var last = original.IndexOf(end, StringComparison.Ordinal);
            if (first < 0 && last < 0) return original;
            if (first < 0 || last < first || original.IndexOf(begin, first + begin.Length, StringComparison.Ordinal) >= 0 ||
                original.IndexOf(end, last + end.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Ambiguous ME.BECS managed response block; preserving the existing response file.");
            var after = last + end.Length;
            if (after < original.Length && original[after] == '\r') ++after;
            if (after < original.Length && original[after] == '\n') ++after;
            return original.Substring(0, first) + original.Substring(after);
        }

        internal static void Publish(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.System);
        internal static void PublishEntities(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.Entity);
        internal static void PublishAspects(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.Aspect);
        internal static void PublishDestroy(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.Destroy);
        internal static void PublishViews(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.Views);
        internal static void PublishJobInit(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.JobInit);
        internal static void PublishJobSetup(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.JobSetup);
        internal static void PublishGraphs(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.Graph);
        internal static void PublishJobDebug(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.JobDebug);
        internal static void PublishNetwork(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.Network);
        internal static void PublishConfigs(string manifest, bool editor, string inputDirectory) => PublishCore(manifest, editor, inputDirectory, SelectionKind.Config);

        internal static void PublishSelection(string manifest, bool editor, string inputDirectory, bool types) =>
            PublishCore(manifest, editor, inputDirectory, types ? SelectionKind.Type : SelectionKind.System);

        private static void PublishCore(string manifest, bool editor, string inputDirectory, SelectionKind kind) {
            var documents = SelectionDocuments(manifest.Split('\n'), editor, kind);
            var directory = inputDirectory.Replace('\\', '/') + "/" + kind + "Fragments";
            var pending = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var document in documents) pending.Add(directory + "/" + SelectionFileName(document.Owner, editor, kind), SelectionSerialize(document, kind));
            // Retire this profile's previous publications without an initializer.
            if (Directory.Exists(directory)) foreach (var path in Directory.EnumerateFiles(directory, "*" + (editor ? "Editor" : "Runtime") + Format.NativeSuffix)) {
                var normalized = path.Replace('\\', '/');
                if (pending.ContainsKey(normalized)) continue;
                if (!SelectionParse(File.ReadAllText(path), kind, out var previous))
                    throw new InvalidOperationException("Cannot identify previous registration fragment: " + path);
                previous.Entries = Array.Empty<KeyValuePair<int, string>>();
                pending.Add(normalized, SelectionSerialize(previous, kind));
            }
            // Framework and user response files are not publication destinations.
            // Unity discovers these project assets without compiler path injection.
            foreach (var pair in pending) {
                if (File.Exists(pair.Key) && File.ReadAllText(pair.Key) == pair.Value) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(pair.Key));
                File.WriteAllText(pair.Key, pair.Value, new UTF8Encoding(false));
                UnityEditor.AssetDatabase.ImportAsset(pair.Key);
            }
        }

        internal static bool ValidateCompiled(out string reason) => ValidateCore(SelectionKind.System, out reason) &&
            ValidateCore(SelectionKind.Type, out reason) && ValidateCore(SelectionKind.Entity, out reason) &&
            ValidateCore(SelectionKind.Aspect, out reason) && ValidateCore(SelectionKind.Destroy, out reason) && ValidateCore(SelectionKind.Config, out reason) && ValidateCore(SelectionKind.Network, out reason) && ValidateCore(SelectionKind.Views, out reason) && ValidateCore(SelectionKind.JobInit, out reason) && ValidateCore(SelectionKind.JobSetup, out reason) && ValidateCore(SelectionKind.JobDebug, out reason) && ValidateCore(SelectionKind.Graph, out reason);

        internal static bool ValidateSelection(bool types, out string reason) => ValidateCore(types ? SelectionKind.Type : SelectionKind.System, out reason);
        internal static bool ValidateEntities(out string reason) => ValidateCore(SelectionKind.Entity, out reason);
        internal static bool ValidateAspects(out string reason) => ValidateCore(SelectionKind.Aspect, out reason);
        internal static bool ValidateDestroy(out string reason) => ValidateCore(SelectionKind.Destroy, out reason);
        internal static bool ValidateConfigs(out string reason) => ValidateCore(SelectionKind.Config, out reason);

        private static bool ValidateCore(SelectionKind kind, out string reason) {
            reason = "";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
                .GroupBy(assembly => assembly.GetName().Name).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            foreach (var editor in new[] { false, true }) {
                var profile = editor ? "editor" : "runtime";
                if (!assemblies.TryGetValue("ME.BECS.Gen." + (editor ? "Editor" : "Runtime"), out var consumers) || consumers.Length != 1) {
                    reason = "Missing unique registration selection consumer."; return false;
                }
                var rows = consumers[0].GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Where(item => item.Key == "ME.BECS.TypeInput.v1" && item.Value.StartsWith(profile + "\t", StringComparison.Ordinal))
                    .Select(item => item.Value.Substring(profile.Length + 1)).ToArray();
                if (kind == SelectionKind.Views && !rows.Any(row => row.StartsWith("view-type-schema\t", StringComparison.Ordinal))) continue;
                if (kind == SelectionKind.Network && !rows.Any(row => row.StartsWith("network-method-schema\t", StringComparison.Ordinal))) continue;
                if (!rows.Contains(kind.ToString().ToLowerInvariant() + "-publication-schema\t0\tdjE=")) { reason = "Registration publications await input export."; return false; }
                foreach (var document in SelectionDocuments(rows, editor, kind)) {
                    var expected = Format.Metadata(document, SelectionSerialize(document, kind));
                    if (!assemblies.TryGetValue(document.Owner, out var owners) || owners.Length != 1 ||
                        owners[0].GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                            .Count(item => item.Key == SelectionMetadata(kind) && item.Value == expected) != 1) {
                        reason = "Missing or stale " + profile + " " + kind + " publication in " + document.Owner + ". Wait for input export and compilation.";
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
