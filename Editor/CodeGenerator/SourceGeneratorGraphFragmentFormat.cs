#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    // A graph is an indivisible publication: its slots, topology and IL job
    // selection travel together. No Editor-computed injection fields or C# bodies.
    internal static class SourceGeneratorGraphFragmentFormat {
        internal const string MetadataKey = "ME.BECS.GraphFragment.v1";
        internal static string EntryValue(IEnumerable<string> rows) => Envelope.Encode(string.Join("\n", rows));
        internal static string[] Rows(string entry) => Envelope.Decode(entry).Split('\n');
        internal static bool IsGraphRow(string row) => new[] { "graph-registration\t", "graph-topology\t", "graph-system\t", "graph-job-selection\t" }
            .Any(prefix => row.StartsWith(prefix, StringComparison.Ordinal));
        internal static bool ValidEntry(string entry) {
            try {
                var rows = Rows(entry);
                if (EntryValue(rows) != entry || rows.Length < 2 || rows.Any(row => !IsGraphRow(row))) return false;
                var graph = rows[0].Split('\t');
                if (graph.Length != 5 || graph[0] != "graph-registration" || !CanonicalInt(graph[1], 0) ||
                    !CanonicalInt(graph[3], int.MinValue + 1) || !CanonicalInt(graph[4], 0)) return false;
                var prefix = Envelope.Decode(graph[2]);
                if (!prefix.StartsWith("ME.BECS.", StringComparison.Ordinal) || prefix.Any(char.IsControl)) return false;
                var slots = 0;
                var topology = false;
                var jobs = false;
                var types = new List<string>();
                var owners = new List<string>();
                foreach (var row in rows.Skip(1)) {
                    var fields = row.Split('\t');
                    if (fields.Length < 4 || !CanonicalInt(fields[1], 0) || fields[3] != graph[3]) return false;
                    var identity = Envelope.Decode(fields[2]);
                    if (string.IsNullOrWhiteSpace(identity) || identity.Any(char.IsControl)) return false;
                    if (fields[0] == "graph-topology") {
                        if (topology || slots != 0 || jobs || fields.Length != 5 || identity != "topology") return false;
                        topology = true;
                        Envelope.Decode(fields[4]);
                    } else if (fields[0] == "graph-system") {
                        if (!topology || jobs || fields.Length != 8 || fields[4] != slots.ToString(CultureInfo.InvariantCulture) ||
                            (fields[5] != "0" && fields[5] != "1") || !CanonicalInt(fields[7], 0) ||
                            !uint.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out var source) ||
                            fields[6] != source.ToString(CultureInfo.InvariantCulture) ||
                            (fields[5] == "1" ? source != 0 || fields[7] != "0" : source == 0)) return false;
                        ++slots;
                        if (!types.Contains(identity, StringComparer.Ordinal)) types.Add(identity);
                    } else if (fields[0] == "graph-job-selection") {
                        if (!topology || fields.Length != 5 || !Envelope.Decode(fields[4]).StartsWith("v2\nil\n", StringComparison.Ordinal)) return false;
                        jobs = true;
                        owners.Add(identity);
                    } else return false;
                }
                return topology && slots.ToString(CultureInfo.InvariantCulture) == graph[4] && types.SequenceEqual(owners, StringComparer.Ordinal);
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }
        private static bool CanonicalInt(string value, int min) => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) &&
            number >= min && value == number.ToString(CultureInfo.InvariantCulture);
        internal static string[] Entries(IEnumerable<string> rows) {
            var graphRows = rows.Where(IsGraphRow).Select(row => (Row: row, Fields: row.Split('\t'))).ToArray();
            return graphRows.Where(item => item.Fields[0] == "graph-registration")
                .Select(graph => EntryValue(graphRows.Where(item => item.Fields[3] == graph.Fields[3]).Select(item => item.Row))).ToArray();
        }
        internal static string Plan(IEnumerable<string> rows) => SourceGeneratorNames.Hash("ME.BECS.GraphSelection.v1\n" + string.Join("\n", rows) + "\n");
        internal static string FileName(string owner, bool editor) => "GraphFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("GraphFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (document.Editor && document.Entries.Length != 0 || document.Entries.Any(entry => !ValidEntry(entry.Value)) ||
                document.Entries.Select(entry => Rows(entry.Value)[0].Split('\t')[3]).Distinct(StringComparer.Ordinal).Count() != document.Entries.Length) {
                document = null; return false;
            }
            return true;
        }
        internal static Envelope.Document[] Documents(IEnumerable<string> rows, bool editor) {
            var selection = rows.Where(row => row.StartsWith("graph-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], CultureInfo.InvariantCulture)).ToArray();
            if (editor && selection.Length != 0) throw new InvalidOperationException("Graph callbacks belong to the Runtime profile.");
            var expected = Entries(rows);
            var plan = Plan(selection);
            if (expected.Length != selection.Length) throw new InvalidOperationException("Graph publications do not cover the graph selection.");
            return selection.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(CultureInfo.InvariantCulture)) throw new InvalidOperationException("Invalid graph publication order.");
                var entry = Envelope.Decode(fields[2]);
                var owner = Envelope.Decode(fields[3]);
                if (!ValidEntry(entry) || entry != expected[ordinal] || string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) ||
                    owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid graph owner or snapshot.");
                return (Owner: owner, Entry: new KeyValuePair<int, string>(ordinal, entry));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Envelope.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = selection.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }
    }
}
