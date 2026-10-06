namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;
    using Format = CodeGeneration.SourceGeneratorInputCatalogFormat;

    // Assembly names/locations are selected by the exporter, not the readers.
    // Catalog hashes prove input compilation only. Typed fragment receipts are
    // a separate mandatory readiness check, never inferred from this metadata.
    public static class SourceGeneratorInputCatalog {
        internal sealed class Publication {
            internal Assembly Assembly;
            internal string Profile, ContentHash, Snapshot;
            internal string[] Rows;
        }
        private static readonly System.Collections.Generic.Dictionary<Assembly, Publication> cache = new System.Collections.Generic.Dictionary<Assembly, Publication>();

        internal static bool TryGet(bool editor, Assembly[] assemblies, out Publication publication, out string reason) {
            publication = null;
            var profile = editor ? "editor" : "runtime";
            reason = "Missing, invalid or ambiguous compiled " + profile + " input catalog. Wait for input export and compilation.";
            var root = "ME.BECS.SourceGenerated.InputCatalog_" + (editor ? "Editor" : "Runtime");
            var owners = assemblies.Where(assembly => !assembly.IsDynamic && assembly.GetType(root, false) != null).ToArray();
            if (owners.Length != 1) return false;
            var owner = owners[0];
            if (cache.TryGetValue(owner, out var cached)) {
                if (cached.Profile != profile) return false;
                publication = cached; reason = ""; return true;
            }
            var metadata = owner.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            string Single(string key) {
                var values = metadata.Where(attribute => attribute.Key == key).ToArray();
                return values.Length == 1 ? values[0].Value : null;
            }
            var receipt = Single(Format.MetadataKey)?.Split('\t');
            var hash = Single("ME.BECS.InputContentHash.v1");
            var snapshot = Single("ME.BECS.GraphInputSnapshot.v1");
            if (receipt == null || receipt.Length != 3 || receipt[0] != profile || receipt[1] != owner.GetName().Name || !Format.IsHash(receipt[2]) ||
                Single("ME.BECS.TypeInputProfile.v1") != profile || Single("ME.BECS.InputTransport.v1") != "native-additionalfile" ||
                !Format.IsHash(hash) || !Format.IsHash(snapshot)) return false;
            var records = metadata.Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value).ToArray();
            if (records.Length == 0 || records.Any(row => row == null || !row.StartsWith(profile + "\t", StringComparison.Ordinal))) return false;
            publication = new Publication { Assembly = owner, Profile = profile, ContentHash = hash, Snapshot = snapshot,
                Rows = records.Select(row => row.Substring(profile.Length + 1)).ToArray() };
            cache.Add(owner, publication);
            reason = ""; return true;
        }
        internal static bool TryGet(bool editor, out Publication publication, out string reason) => TryGet(editor, AppDomain.CurrentDomain.GetAssemblies(), out publication, out reason);
        public static Assembly GetAssembly(bool editor) => TryGet(editor, out var publication, out var reason) ? publication.Assembly : throw new InvalidOperationException(reason);
        public static string[] GetRows(bool editor) => TryGet(editor, out var publication, out var reason) ? (string[])publication.Rows.Clone() : throw new InvalidOperationException(reason);
    }
}
