#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    internal static class SourceGeneratorNetworkFragmentFormat {
        internal const string MetadataKey = "ME.BECS.NetworkFragment.v1";
        internal static string Plan(IEnumerable<string> rows) => SourceGeneratorNames.Hash("ME.BECS.NetworkSelection.v1\n" + string.Join("\n", rows) + "\n");
        internal static string FileName(string owner, bool editor) => "NetworkFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("NetworkFragment", StringComparison.Ordinal) &&
            path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (document.Entries.Any(entry => !ValidIdentity(entry.Value)) || document.Entries.Select(entry => entry.Value).Distinct(StringComparer.Ordinal).Count() != document.Entries.Length) {
                document = null; return false;
            }
            return true;
        }

        internal static bool ValidIdentity(string identity) {
            try {
                var parts = identity.Split('|');
                return parts.Length == 2 && parts.All(part => {
                    var value = Envelope.Decode(part);
                    return Envelope.Encode(value) == part && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
                });
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }

        internal static string EntryValue(string type, string method) => Envelope.Encode(type) + "|" + Envelope.Encode(method);
        internal static string MethodPayload(string identity) => string.Join("\n", identity.Split('|').Select(Envelope.Decode));

        internal static Envelope.Document[] Documents(IEnumerable<string> rows, bool editor) {
            var selection = rows.Where(row => row.StartsWith("network-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], CultureInfo.InvariantCulture)).ToArray();
            var plan = Plan(selection);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return selection.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Invalid ordered network publication selection.");
                var identity = Envelope.Decode(fields[2]);
                var owner = Envelope.Decode(fields[3]);
                if (!ValidIdentity(identity) || !seen.Add(identity) ||
                    string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid or duplicate network publication owner/identity.");
                return (Owner: owner, Entry: new KeyValuePair<int, string>(ordinal, identity));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Envelope.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = selection.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }
    }
}
