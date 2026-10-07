#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    // Explicit, phase-major selection. No flags, generated C#, graph fingerprint
    // or method-body analysis: layout and Default are still compiler decisions.
    internal static class SourceGeneratorTypeFragmentFormat {
        internal const string Extension = ".becs-type-fragment";
        internal const string MetadataKey = "ME.BECS.TypeFragment.v1";
        internal static readonly string[] Phases = { "Group", "Register", "RegisterShared", "RegisterStatic", "RegisterConfig" };
        internal sealed class Entry {
            internal string Phase;
            internal string Component;
            internal string Group;
        }

        internal static string EntryValue(string phase, string component, string group) =>
            phase + "|" + Envelope.Encode(component) + "|" + Envelope.Encode(group ?? "");

        internal static bool TryEntry(string value, out Entry entry) {
            entry = null;
            try {
                var fields = value.Split('|');
                if (fields.Length != 3 || !Phases.Contains(fields[0])) return false;
                var component = Envelope.Decode(fields[1]);
                var group = Envelope.Decode(fields[2]);
                if (string.IsNullOrWhiteSpace(component) || component.Any(char.IsControl) || group.Any(char.IsControl) ||
                    (fields[0] == "Group" ? string.IsNullOrWhiteSpace(group) : group.Length != 0)) return false;
                entry = new Entry { Phase = fields[0], Component = component, Group = group };
                return true;
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }

        internal static string Plan(IEnumerable<string> rows) => SourceGeneratorNames.Hash("ME.BECS.TypeSelection.v1\n" + string.Join("\n", rows) + "\n");
        internal static string FileName(string owner, bool editor) => "TypeFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("TypeFragment", StringComparison.Ordinal) &&
            path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            var previous = -1;
            foreach (var value in document.Entries) {
                if (!TryEntry(value.Value, out var entry) || Array.IndexOf(Phases, entry.Phase) < previous) { document = null; return false; }
                previous = Array.IndexOf(Phases, entry.Phase);
            }
            return true;
        }

        internal static Envelope.Document[] Documents(IEnumerable<string> rows, bool editor) {
            var selection = rows.Where(row => row.StartsWith("type-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], CultureInfo.InvariantCulture)).ToArray();
            var plan = Plan(selection);
            var previous = -1;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return selection.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(CultureInfo.InvariantCulture) ||
                    !TryEntry(Envelope.Decode(fields[2]), out var entry) || Array.IndexOf(Phases, entry.Phase) < previous ||
                    !seen.Add(entry.Phase + "\n" + entry.Component))
                    throw new InvalidOperationException("Invalid ordered component/group publication selection.");
                previous = Array.IndexOf(Phases, entry.Phase);
                var owner = Envelope.Decode(fields[3]);
                if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid component/group publication owner.");
                return (Owner: owner, Entry: new KeyValuePair<int, string>(ordinal, Envelope.Decode(fields[2])));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Envelope.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = selection.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }
    }
}
