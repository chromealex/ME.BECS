#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    // Only the Views slice. No systems, jobs, graphs or bootstrap execution bodies.
    internal static class SourceGeneratorViewSelectionFragmentFormat {
        internal const string MetadataKey = "ME.BECS.ViewSelectionFragment.v1";
        private static readonly string[] Kinds = { "view-tracker", "view-tracker-view", "view-tracker-module", "view-type-schema", "view-type", "views-publication-schema", "views-registration-owner" };
        internal static bool IsRecord(string kind) => Kinds.Contains(kind);
        internal static string FileName(string owner, bool editor) => "ViewSelectionFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("ViewSelectionFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static string[] Rows(string entry) => entry.Split('|').Select(Envelope.Decode).ToArray();
        internal static string EntryValue(IEnumerable<string> records) {
            var rows = records.Select(row => row.Split('\t')).Where(row => IsRecord(row[0])).ToArray();
            var result = new List<string>();
            foreach (var kind in Kinds) {
                var selected = rows.Where(row => row[0] == kind).OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture)).ToArray();
                if ((kind == "view-tracker" || kind == "view-type-schema" || kind == "views-publication-schema") && selected.Length != 1)
                    throw new InvalidOperationException("Missing or duplicate Views selection header: " + kind);
                for (var i = 0; i < selected.Length; ++i) {
                    var row = selected[i];
                    if (row.Length != (kind == "views-registration-owner" ? 4 : 3) || row[1] != i.ToString(CultureInfo.InvariantCulture))
                        throw new InvalidOperationException("Invalid Views selection row: " + kind);
                    result.Add(string.Join("\t", row));
                }
            }
            return string.Join("|", result.Select(Envelope.Encode));
        }
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (document.Entries.Length == 0) return true;
            try {
                if (document.Count == 1 && document.Entries.Length == 1 && document.Entries[0].Key == 0 &&
                    EntryValue(Rows(document.Entries[0].Value)) == document.Entries[0].Value) return true;
            } catch (Exception error) when (error is FormatException || error is System.Text.DecoderFallbackException || error is InvalidOperationException || error is IndexOutOfRangeException || error is OverflowException) { }
            document = null; return false;
        }
        internal static Envelope.Document[] Documents(IEnumerable<string> records, bool editor) {
            var rows = records.ToArray();
            var selected = rows.Where(row => row.StartsWith("viewselection-registration-owner\t", StringComparison.Ordinal)).ToArray();
            var schemas = rows.Where(row => row.StartsWith("viewselection-publication-schema\t", StringComparison.Ordinal)).ToArray();
            if (!rows.Any(row => row.StartsWith("view-type-schema\t", StringComparison.Ordinal)) && selected.Length == 0 && schemas.Length == 0) return Array.Empty<Envelope.Document>();
            if (selected.Length != 1 || schemas.Length != 1 || schemas[0] != "viewselection-publication-schema\t0\tdjE=")
                throw new InvalidOperationException("Views must select exactly one dependency publisher.");
            var fields = selected[0].Split('\t');
            if (fields.Length != 4 || fields[1] != "0") throw new InvalidOperationException("Invalid Views dependency owner.");
            var owner = Envelope.Decode(fields[3]);
            var entry = Envelope.Decode(fields[2]);
            var feeders = rows.Select(row => row.Split('\t')).Where(row => row[0] == "bootstrap-feeder" && row.Length == 5).ToArray();
            if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) ||
                entry != EntryValue(rows) || feeders.Count(row => row[3] == "views") != 1 || feeders.Count(row => row[4] == "view-types") != 1)
                throw new InvalidOperationException("Views dependency publication differs from its selected inputs/feeders.");
            return new[] { new Envelope.Document { Owner = owner, Editor = editor, Count = 1,
                Plan = SourceGeneratorNames.Hash("ME.BECS.ViewSelection.v1\n" + selected[0] + "\n"), Entries = new[] { new KeyValuePair<int, string>(0, entry) } } };
        }
    }
}
