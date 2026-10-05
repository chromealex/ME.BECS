#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    // Entity ordinals are the actual ushort group IDs, not owner-local indices.
    internal static class SourceGeneratorEntityFragmentFormat {
        internal const string MetadataKey = "ME.BECS.EntityFragment.v1";
        internal static string Plan(IEnumerable<string> rows) => SourceGeneratorNames.Hash("ME.BECS.EntitySelection.v1\n" + string.Join("\n", rows) + "\n");
        internal static string FileName(string owner, bool editor) => "EntityFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("EntityFragment", StringComparison.Ordinal) &&
            path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (document.Count > ushort.MaxValue + 1 || document.Entries.Select(entry => entry.Value).Distinct(StringComparer.Ordinal).Count() != document.Entries.Length) {
                document = null; return false;
            }
            return true;
        }

        internal static Envelope.Document[] Documents(IEnumerable<string> rows, bool editor) {
            var selection = rows.Where(row => row.StartsWith("entity-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], CultureInfo.InvariantCulture)).ToArray();
            if (selection.Length > ushort.MaxValue + 1) throw new InvalidOperationException("Entity group IDs exceed ushort capacity.");
            var plan = Plan(selection);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return selection.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Invalid ordered entity publication selection.");
                var identity = Envelope.Decode(fields[2]);
                var owner = Envelope.Decode(fields[3]);
                if (string.IsNullOrWhiteSpace(identity) || identity.Any(char.IsControl) || !seen.Add(identity) ||
                    string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid or duplicate entity publication owner/identity.");
                return (Owner: owner, Entry: new KeyValuePair<int, string>(ordinal, identity));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Envelope.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = selection.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }
    }
}
