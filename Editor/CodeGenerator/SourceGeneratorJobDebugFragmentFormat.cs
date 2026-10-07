#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    internal static class SourceGeneratorJobDebugFragmentFormat {
        internal const string MetadataKey = "ME.BECS.JobDebugFragment.v1";
        internal static string EntryValue(string payload) => Envelope.Encode(payload);
        internal static string Payload(string entry) => Envelope.Decode(entry);
        internal static bool ValidEntry(string entry) {
            try {
                var payload = Payload(entry);
                var rows = payload.Split('\n');
                return rows.Length >= 5 && (rows[0] == "v1" || rows[0] == "v2") &&
                    !string.IsNullOrWhiteSpace(rows[1]) && !string.IsNullOrWhiteSpace(rows[2]) &&
                    rows.Take(5).All(row => !row.Any(char.IsControl)) && (rows[4] == "0" || rows[4] == "1") &&
                    EntryValue(payload) == entry;
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }
        private static string Key(string entry) => string.Join("\n", Payload(entry).Split('\n').Skip(1).Take(2));
        internal static string Plan(IEnumerable<string> rows) => SourceGeneratorNames.Hash("ME.BECS.JobDebugSelection.v1\n" + string.Join("\n", rows) + "\n");
        internal static string FileName(string owner, bool editor) => "JobDebugFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("JobDebugFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (document.Entries.Any(entry => !ValidEntry(entry.Value)) ||
                document.Entries.Select(entry => Key(entry.Value)).Distinct(StringComparer.Ordinal).Count() != document.Entries.Length) {
                document = null; return false;
            }
            return true;
        }
        internal static Envelope.Document[] Documents(IEnumerable<string> rows, bool editor) {
            var selection = rows.Where(row => row.StartsWith("jobdebug-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], CultureInfo.InvariantCulture)).ToArray();
            var plan = Plan(selection);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return selection.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Invalid ordered job debug selection.");
                var entry = Envelope.Decode(fields[2]);
                var owner = Envelope.Decode(fields[3]);
                if (!ValidEntry(entry) || !seen.Add(Key(entry)) || string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) ||
                    owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid job debug owner/entry.");
                return (Owner: owner, Entry: new KeyValuePair<int, string>(ordinal, entry));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Envelope.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = selection.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }
    }
}
