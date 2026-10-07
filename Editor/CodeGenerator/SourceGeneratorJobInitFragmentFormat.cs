#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    internal static class SourceGeneratorJobInitFragmentFormat {
        internal const string MetadataKey = "ME.BECS.JobInitFragment.v1";
        internal static string EntryValue(string payload) => string.Join("|", payload.Split('\n').Select(Envelope.Encode));
        internal static string Payload(string entry) => string.Join("\n", entry.Split('|').Select(Envelope.Decode));
        internal static bool ValidEntry(string entry) {
            try {
                var values = entry.Split('|').Select(Envelope.Decode).ToArray();
                return values.Length >= 4 && values[0] == "v1" && !string.IsNullOrWhiteSpace(values[1]) &&
                    values.All(value => !value.Any(char.IsControl)) && EntryValue(string.Join("\n", values)) == entry &&
                    (values[2].Length == 0 ? values.Length == 4 && values[3].Length == 0 :
                        values.Skip(2).All(value => !string.IsNullOrWhiteSpace(value)));
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }
        internal static string Plan(IEnumerable<string> rows) => SourceGeneratorNames.Hash("ME.BECS.JobInitSelection.v1\n" + string.Join("\n", rows) + "\n");
        internal static string FileName(string owner, bool editor) => "JobInitFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("JobInitFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            // Repeated jobs/calls and stat-only entries are intentional. Only
            // their global ordinals must be unique (checked by the envelope).
            if (document.Entries.Any(entry => !ValidEntry(entry.Value))) { document = null; return false; }
            return true;
        }
        internal static Envelope.Document[] Documents(IEnumerable<string> rows, bool editor) {
            var selection = rows.Where(row => row.StartsWith("jobinit-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], CultureInfo.InvariantCulture)).ToArray();
            var plan = Plan(selection);
            return selection.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Invalid ordered job initialization selection.");
                var entry = Envelope.Decode(fields[2]);
                var owner = Envelope.Decode(fields[3]);
                if (!ValidEntry(entry) || string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid job initialization owner/entry.");
                return (Owner: owner, Entry: new KeyValuePair<int, string>(ordinal, entry));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Envelope.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = selection.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }
    }
}
