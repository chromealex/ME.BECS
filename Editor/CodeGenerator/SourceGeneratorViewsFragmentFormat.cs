#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    internal static class SourceGeneratorViewsFragmentFormat {
        internal const string MetadataKey = "ME.BECS.ViewsFragment.v1";
        internal static readonly string[] Phases = { "Component", "ViewTracker", "ModuleTracker", "ViewType" };
        internal sealed class Entry { internal string Phase; internal string Component; }
        internal static string EntryValue(string phase, string component) => phase + "|" + Envelope.Encode(component);
        internal static bool TryEntry(string value, out Entry entry) {
            entry = null;
            try {
                var fields = value.Split('|');
                if (fields.Length != 2 || !Phases.Contains(fields[0])) return false;
                var component = Envelope.Decode(fields[1]);
                if (string.IsNullOrWhiteSpace(component) || component.Any(char.IsControl)) return false;
                entry = new Entry { Phase = fields[0], Component = component };
                return true;
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }
        internal static string Plan(IEnumerable<string> rows) => SourceGeneratorNames.Hash("ME.BECS.ViewsSelection.v1\n" + string.Join("\n", rows) + "\n");
        internal static string FileName(string owner, bool editor) => "ViewsFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("ViewsFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            var previous = -1;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in document.Entries) {
                if (!TryEntry(value.Value, out var entry) || Array.IndexOf(Phases, entry.Phase) < previous || !seen.Add(value.Value)) { document = null; return false; }
                previous = Array.IndexOf(Phases, entry.Phase);
            }
            return true;
        }
        internal static Envelope.Document[] Documents(IEnumerable<string> rows, bool editor) {
            var selection = rows.Where(row => row.StartsWith("views-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], CultureInfo.InvariantCulture)).ToArray();
            var plan = Plan(selection);
            var previous = -1;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return selection.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(CultureInfo.InvariantCulture) ||
                    !TryEntry(Envelope.Decode(fields[2]), out var entry) || Array.IndexOf(Phases, entry.Phase) < previous || !seen.Add(entry.Phase + "\n" + entry.Component))
                    throw new InvalidOperationException("Invalid ordered views publication selection.");
                previous = Array.IndexOf(Phases, entry.Phase);
                var owner = Envelope.Decode(fields[3]);
                if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid views publication owner.");
                return (Owner: owner, Entry: new KeyValuePair<int, string>(ordinal, Envelope.Decode(fields[2])));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Envelope.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = selection.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }
    }
}
