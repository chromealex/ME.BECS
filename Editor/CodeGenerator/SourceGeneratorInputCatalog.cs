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
            var transport = Single("ME.BECS.InputTransport.v1");
            if (transport == LegacyTransport) {
                // A generator built before compact catalogs still embeds the catalog
                // rows; for a compact catalog they carry the full snapshot hash.
                hash = LegacyContentHash(metadata, profile);
                transport = hash == null ? null : Format.Transport;
            }
            if (receipt == null || receipt.Length != 3 || receipt[0] != profile || receipt[1] != owner.GetName().Name || !Format.IsHash(receipt[2]) ||
                Single("ME.BECS.TypeInputProfile.v1") != profile || transport != Format.Transport ||
                !Format.IsHash(hash) || !Format.IsHash(snapshot)) return false;
            // The compiled catalog carries only the hash of the snapshot it compiled.
            var records = SnapshotStore.Rows(editor, hash, snapshot);
            if (records == null) {
                reason = "The compiled " + profile + " input catalog refers to a source input snapshot that is not available locally. Regenerate inputs.";
                return false;
            }
            publication = new Publication { Assembly = owner, Profile = profile, ContentHash = hash, Snapshot = snapshot, Rows = records };
            cache.Add(owner, publication);
            reason = ""; return true;
        }
        private const string LegacyTransport = "native-additionalfile";

        private static string LegacyContentHash(AssemblyMetadataAttribute[] metadata, string profile) {
            var prefix = profile + "\t" + Format.ContentHashRow + "\t";
            var rows = metadata.Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null &&
                attribute.Value.StartsWith(prefix, StringComparison.Ordinal)).Select(attribute => attribute.Value.Split('\t')).ToArray();
            if (rows.Length != 1 || rows[0].Length != 4 || rows[0][2] != "0") return null;
            try {
                var hash = CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(rows[0][3]);
                return Format.IsHash(hash) ? hash : null;
            } catch (FormatException) { return null; }
            catch (System.Text.DecoderFallbackException) { return null; }
        }

        // Full snapshots addressed by content hash. The project snapshot may already be
        // newer than the compiled catalog (published, not yet compiled), so the rows
        // a compiled catalog refers to are kept in Library until they are superseded.
        internal static class SnapshotStore {
            private const string Directory = "Library/ME.BECS/InputSnapshots";
            private const int Keep = 6;
            internal const string Extension = ".becs-snapshot";

            private static string PathOf(string hash) => Directory + "/" + hash + Extension;

            // Filesystem only (publication worker thread safe).
            internal static void Remember(string content) {
                try {
                    var hash = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(content);
                    System.IO.Directory.CreateDirectory(Directory);
                    var path = PathOf(hash);
                    if (!System.IO.File.Exists(path)) System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(false));
                    else System.IO.File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                    foreach (var stale in new System.IO.DirectoryInfo(Directory).GetFiles("*" + Extension)
                                 .OrderByDescending(file => file.LastWriteTimeUtc).Skip(Keep)) stale.Delete();
                } catch (System.IO.IOException) { } catch (UnauthorizedAccessException) { }
            }

            // A miss re-reads a large snapshot; retry only after the project snapshot changes.
            private static readonly System.Collections.Generic.Dictionary<string, DateTime> misses = new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.Ordinal);

            internal static string[] Rows(bool editor, string hash, string snapshot) {
                var project = SourceGeneratorInputTransport.InputPath(editor);
                var key = (editor ? "editor\t" : "runtime\t") + hash + "\t" + snapshot;
                var stamp = System.IO.File.Exists(project) ? System.IO.File.GetLastWriteTimeUtc(project) : DateTime.MinValue;
                if (misses.TryGetValue(key, out var missed) && missed == stamp && !System.IO.File.Exists(PathOf(hash))) return null;
                var rows = Load(editor, hash, snapshot, project);
                if (rows == null) misses[key] = stamp; else misses.Remove(key);
                return rows;
            }

            private static string[] Load(bool editor, string hash, string snapshot, string project) {
                string content = null;
                var stored = PathOf(hash);
                if (System.IO.File.Exists(stored)) content = System.IO.File.ReadAllText(stored);
                if (content == null || ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(content) != hash) {
                    content = null;
                    if (System.IO.File.Exists(project)) {
                        var current = System.IO.File.ReadAllText(project);
                        if (ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(current) == hash) {
                            content = current;
                            Remember(current);
                        }
                    }
                }
                if (content == null || !Format.TryManifest(content, out var manifest) || manifest.Editor != editor || manifest.Snapshot != snapshot) return null;
                return manifest.Rows;
            }
        }

        // Rows of the catalog(s) compiled into this assembly, as "profile\tkind\t...".
        internal static string[][] Records(Assembly owner) {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var result = new System.Collections.Generic.List<string[]>();
            foreach (var editor in new[] { false, true }) {
                if (!TryGet(editor, assemblies, out var publication, out _) || publication.Assembly != owner) continue;
                foreach (var row in publication.Rows) result.Add((publication.Profile + "\t" + row).Split('\t'));
            }
            return result.ToArray();
        }

        internal static bool TryGet(bool editor, out Publication publication, out string reason) => TryGet(editor, AppDomain.CurrentDomain.GetAssemblies(), out publication, out reason);
        public static Assembly GetAssembly(bool editor) => TryGet(editor, out var publication, out var reason) ? publication.Assembly : throw new InvalidOperationException(reason);
        public static string[] GetRows(bool editor) => TryGet(editor, out var publication, out var reason) ? (string[])publication.Rows.Clone() : throw new InvalidOperationException(reason);
    }
}
