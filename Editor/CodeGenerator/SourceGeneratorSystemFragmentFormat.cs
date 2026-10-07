#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    // Shared data contract. Contains selection only: no graph/code fingerprint,
    // project paths, timestamps, registration effects or emitted C#.
    internal static class SourceGeneratorSystemFragmentFormat {
        internal const string Extension = ".becs-system-fragment";
        internal const string NativeSuffix = ".ME.BECS.SourceGenerator.additionalfile";
        internal const string MetadataKey = "ME.BECS.SystemFragment.v1";
        internal sealed class Document {
            internal string Owner;
            internal bool Editor;
            internal string Plan;
            internal int Count;
            internal System.Collections.Generic.KeyValuePair<int, string>[] Entries;
        }
        internal static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        internal static string Decode(string value) => new UTF8Encoding(false, true).GetString(Convert.FromBase64String(value));
        internal static string Plan(System.Collections.Generic.IEnumerable<string> orderedOwnerRows) =>
            SourceGeneratorNames.Hash("ME.BECS.SystemSelection.v1\n" + string.Join("\n", orderedOwnerRows) + "\n");
        internal static string FileName(string owner, bool editor) => "SystemFragment" + SourceGeneratorNames.Hash(owner) +
            (editor ? "Editor" : "Runtime") + NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("SystemFragment", StringComparison.Ordinal) &&
            path.EndsWith(NativeSuffix, StringComparison.Ordinal);
        internal static string Metadata(Document document, string content) =>
            (document.Editor ? "editor" : "runtime") + "\t" + document.Owner + "\t" + SourceGeneratorNames.Hash(content);

        internal static string Serialize(Document document) => SerializeEnvelope(document, MetadataKey);

        // Shared envelope for ordered system and component/group selections.
        // Each selection kind validates its own entry payload separately.
        internal static string SerializeEnvelope(Document document, string schema) {
            var result = new StringBuilder(schema).Append('\t').Append(Encode(document.Owner))
                .Append('\t').Append(document.Editor ? "editor" : "runtime").Append('\t').Append(document.Plan)
                .Append('\t').Append(document.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var entry in document.Entries)
                result.Append(entry.Key.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Encode(entry.Value)).Append('\n');
            var payload = result.ToString();
            return payload + "end\t" + SourceGeneratorNames.Hash(payload) + "\n";
        }

        internal static bool TryParse(string content, out Document document) => TryParseEnvelope(content, MetadataKey, out document);

        internal static bool TryParseEnvelope(string content, string schema, out Document document) {
            document = null;
            if (content == null) return false;
            try {
                var lines = content.Replace("\r\n", "\n").Split('\n');
                if (lines.Length < 3 || lines[lines.Length - 1] != "") return false;
                var header = lines[0].Split('\t');
                var end = lines[lines.Length - 2].Split('\t');
                if (header.Length != 5 || header[0] != schema ||
                    (header[2] != "editor" && header[2] != "runtime") || header[3].Length != 64 ||
                    header[3].Any(c => !(c >= '0' && c <= '9' || c >= 'A' && c <= 'F')) ||
                    !int.TryParse(header[4], NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                    end.Length != 2 || end[0] != "end" || end[1] != SourceGeneratorNames.Hash(string.Join("\n", lines.Take(lines.Length - 2)) + "\n")) return false;
                var owner = Decode(header[1]);
                if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) return false;
                var entries = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, string>>();
                var last = -1;
                foreach (var line in lines.Skip(1).Take(lines.Length - 3)) {
                    var fields = line.Split('\t');
                    if (fields.Length != 2 || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) ||
                        ordinal <= last || ordinal >= count) return false;
                    var identity = Decode(fields[1]);
                    if (string.IsNullOrWhiteSpace(identity) || identity.Any(char.IsControl)) return false;
                    entries.Add(new System.Collections.Generic.KeyValuePair<int, string>(ordinal, identity));
                    last = ordinal;
                }
                document = new Document { Owner = owner, Editor = header[2] == "editor", Plan = header[3], Count = count, Entries = entries.ToArray() };
                return true;
            } catch (FormatException) { return false; }
            catch (DecoderFallbackException) { return false; }
        }
    }
}
