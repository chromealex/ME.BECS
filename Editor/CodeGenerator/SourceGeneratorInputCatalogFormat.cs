#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    // Compiled transport evidence, not proof that typed registrations succeeded.
    // Readiness must also validate every selected owner publication.
    internal static class SourceGeneratorInputCatalogFormat {
        internal const string MetadataKey = "ME.BECS.InputCatalogFragment.v1";
        internal sealed class Manifest {
            internal string Owner, Content, Snapshot;
            internal bool Editor;
            internal string[] Rows;
        }
        internal static string FileName(string owner, bool editor) => "InputCatalogFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("InputCatalogFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
        internal static bool TryManifest(string content, out Manifest manifest) {
            manifest = null;
            if (content == null) return false;
            try {
                var lines = content.Replace("\r\n", "\n").Split('\n');
                if (lines.Length < 6 || lines[lines.Length - 1] != "") return false;
                var header = lines[0].Split('\t');
                var footerIndex = lines.Length - 2;
                var footer = lines[footerIndex].Split('\t');
                if (header.Length != 3 || header[0] != "ME.BECS.TypeInputs.v3" || string.IsNullOrWhiteSpace(Envelope.Decode(header[1])) ||
                    (header[2] != "editor" && header[2] != "runtime") || footer.Length != 3 || footer[0] != "end" ||
                    footer[1] != (footerIndex - 1).ToString(CultureInfo.InvariantCulture) ||
                    footer[2] != SourceGeneratorNames.Hash(string.Join("\n", lines.Take(footerIndex)) + "\n")) return false;
                var rows = lines.Skip(1).Take(footerIndex - 1).ToArray();
                if (rows.Any(row => row.Split('\t').Length < 3)) return false;
                var schema = rows.Where(row => row.StartsWith("inputcatalog-publication-schema\t", StringComparison.Ordinal)).ToArray();
                var owners = rows.Where(row => row.StartsWith("inputcatalog-registration-owner\t", StringComparison.Ordinal)).ToArray();
                var snapshots = rows.Where(row => row.StartsWith("graph-input-snapshot\t", StringComparison.Ordinal)).ToArray();
                if (schema.Length != 1 || schema[0] != "inputcatalog-publication-schema\t0\tdjE=" || owners.Length != 1 || snapshots.Length != 1) return false;
                var owner = owners[0].Split('\t');
                var snapshot = snapshots[0].Split('\t');
                if (owner.Length != 4 || owner[1] != "0" || owner[2] != "djE=" || snapshot.Length != 3 || snapshot[1] != "0") return false;
                var name = Envelope.Decode(owner[3]);
                var identity = Envelope.Decode(snapshot[2]);
                if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl) || name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) || !IsHash(identity)) return false;
                manifest = new Manifest { Owner = name, Editor = header[2] == "editor", Snapshot = identity, Content = content, Rows = rows };
                return true;
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }
        internal const string ContentHashRow = "input-content-hash";
        // v2: compact catalog (hash only). Older compiled catalogs are treated as stale.
        internal const string Transport = "native-additionalfile-v2";

        // The compiled catalog proves which full snapshot was compiled; it does not
        // carry the snapshot. Embedding every row (tens of MB) into one assembly made
        // that compilation, its domain reload and every metadata scan slow. Readers
        // load the rows from the Editor-side snapshot whose hash matches.
        internal static string Compact(string content) {
            if (!TryManifest(content, out var manifest)) throw new InvalidOperationException("Invalid input catalog manifest.");
            return Compact(content, manifest);
        }

        // Callers that already validated the manifest must not parse/hash 20 MB again.
        private static string Compact(string content, Manifest manifest) {
            var newline = content.IndexOf('\n');
            var rows = new List<string> { (newline < 0 ? content : content.Substring(0, newline)).TrimEnd('\r') };
            rows.AddRange(manifest.Rows.Where(row => row.StartsWith("inputcatalog-publication-schema\t", StringComparison.Ordinal) ||
                row.StartsWith("inputcatalog-registration-owner\t", StringComparison.Ordinal) ||
                row.StartsWith("graph-input-snapshot\t", StringComparison.Ordinal)));
            rows.Add(ContentHashRow + "\t0\t" + Envelope.Encode(SourceGeneratorNames.Hash(content)));
            var body = string.Join("\n", rows) + "\n";
            return body + "end\t" + (rows.Count - 1).ToString(CultureInfo.InvariantCulture) + "\t" + SourceGeneratorNames.Hash(body) + "\n";
        }

        // Hash of the full snapshot recorded by Compact; null for a full or invalid manifest.
        internal static string ContentHash(Manifest manifest) {
            var rows = manifest.Rows.Where(row => row.StartsWith(ContentHashRow + "\t", StringComparison.Ordinal)).ToArray();
            if (rows.Length != 1) return null;
            var fields = rows[0].Split('\t');
            if (fields.Length != 3 || fields[1] != "0") return null;
            var hash = Envelope.Decode(fields[2]);
            return IsHash(hash) ? hash : null;
        }

        internal static Envelope.Document Document(string content, bool editor) {
            if (!TryManifest(content, out var manifest) || manifest.Editor != editor) throw new InvalidOperationException("Invalid input catalog manifest.");
            var compact = Compact(content, manifest);
            return new Envelope.Document { Owner = manifest.Owner, Editor = editor, Count = 1,
                Plan = SourceGeneratorNames.Hash(compact), Entries = new[] { new KeyValuePair<int, string>(0, Envelope.Encode(compact)) } };
        }
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (document.Entries.Length == 0) return true;
            try {
                if (document.Count == 1 && document.Entries.Length == 1 && document.Entries[0].Key == 0 &&
                    TryManifest(Envelope.Decode(document.Entries[0].Value), out var manifest) && manifest.Owner == document.Owner &&
                    manifest.Editor == document.Editor && SourceGeneratorNames.Hash(manifest.Content) == document.Plan) return true;
            } catch (FormatException) { }
            catch (System.Text.DecoderFallbackException) { }
            document = null; return false;
        }
    }
}
