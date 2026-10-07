#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    // Editor UI selection only. No world/bootstrap data or gameplay references.
    internal static class SourceGeneratorThemeMenuFragmentFormat {
        internal const string MetadataKey = "ME.BECS.ThemeMenuFragment.v1";
        internal static bool IsRecord(string kind) => kind == "theme-menu-schema" || kind == "theme-menu";
        internal static string FileName(string owner, bool editor) => "ThemeMenuFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("ThemeMenuFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static string[] Rows(string entry) => entry.Split('|').Select(Envelope.Decode).ToArray();
        internal static string EntryValue(IEnumerable<string> records) {
            var rows = records.Select(row => row.Split('\t')).Where(row => IsRecord(row[0])).ToArray();
            var schema = rows.Where(row => row[0] == "theme-menu-schema").ToArray();
            if (schema.Length != 1 || schema[0].Length != 3 || schema[0][1] != "0") throw new InvalidOperationException("Theme menus require one schema.");
            var header = Envelope.Decode(schema[0][2]).Split('\n');
            if (header.Length != 3 || header[0] != "v1" || !int.TryParse(header[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                count < 1 || header[1] != count.ToString(CultureInfo.InvariantCulture) || !int.TryParse(header[2], NumberStyles.None, CultureInfo.InvariantCulture, out var builtIns) ||
                builtIns < 1 || builtIns > count || header[2] != builtIns.ToString(CultureInfo.InvariantCulture)) throw new InvalidOperationException("Invalid theme menu schema.");
            var selected = rows.Where(row => row[0] == "theme-menu").OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture)).ToArray();
            if (selected.Length != count) throw new InvalidOperationException("Incomplete theme menu selection.");
            for (var index = 0; index < selected.Length; ++index) {
                if (selected[index].Length != 3 || selected[index][1] != index.ToString(CultureInfo.InvariantCulture)) throw new InvalidOperationException("Invalid theme menu ordinal.");
                var theme = Envelope.Decode(selected[index][2]).Split('\n');
                if (theme.Length != 2 || theme.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))) throw new InvalidOperationException("Invalid theme menu entry.");
            }
            return string.Join("|", schema.Concat(selected).Select(row => Envelope.Encode(string.Join("\t", row))));
        }
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (!document.Editor) { document = null; return false; }
            if (document.Entries.Length == 0) return true;
            try {
                if (document.Count == 1 && document.Entries.Length == 1 && document.Entries[0].Key == 0 &&
                    EntryValue(Rows(document.Entries[0].Value)) == document.Entries[0].Value) return true;
            } catch (Exception error) when (error is FormatException || error is System.Text.DecoderFallbackException || error is InvalidOperationException || error is IndexOutOfRangeException || error is OverflowException) { }
            document = null; return false;
        }
        internal static Envelope.Document[] Documents(IEnumerable<string> records, bool editor) {
            var rows = records.ToArray();
            var selected = rows.Where(row => row.StartsWith("thememenu-registration-owner\t", StringComparison.Ordinal)).ToArray();
            var schemas = rows.Where(row => row.StartsWith("thememenu-publication-schema\t", StringComparison.Ordinal)).ToArray();
            if (!editor && selected.Length == 0 && schemas.Length == 0) return Array.Empty<Envelope.Document>();
            if (!editor || selected.Length != 1 || schemas.Length != 1 || schemas[0] != "thememenu-publication-schema\t0\tdjE=") throw new InvalidOperationException("Theme menus need one Editor owner.");
            var fields = selected[0].Split('\t');
            if (fields.Length != 4 || fields[1] != "0") throw new InvalidOperationException("Invalid theme menu owner.");
            var owner = Envelope.Decode(fields[3]);
            var entry = Envelope.Decode(fields[2]);
            if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) || entry != EntryValue(rows))
                throw new InvalidOperationException("Theme menus differ from their selected input slice.");
            return new[] { new Envelope.Document { Owner = owner, Editor = true, Count = 1,
                Plan = SourceGeneratorNames.Hash("ME.BECS.ThemeMenuSelection.v1\n" + selected[0] + "\n"), Entries = new[] { new KeyValuePair<int, string>(0, entry) } } };
        }
    }
}
