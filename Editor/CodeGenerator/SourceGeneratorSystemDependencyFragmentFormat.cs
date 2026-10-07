#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    internal static class SourceGeneratorSystemDependencyFragmentFormat {
        internal const string MetadataKey = "ME.BECS.SystemDependencyFragment.v1";
        internal static bool IsRecord(string kind) => kind == "system-dependencies-schema" || kind == "system-dependencies";
        internal static string FileName(string owner, bool editor) => "SystemDependencyFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("SystemDependencyFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static string[] Rows(string entry) => entry.Split('|').Select(Envelope.Decode).ToArray();
        internal static string EntryValue(IEnumerable<string> records) {
            var rows = records.Select(row => row.Split('\t')).Where(row => IsRecord(row[0])).ToArray();
            var schema = rows.Where(row => row[0] == "system-dependencies-schema").ToArray();
            if (schema.Length != 1 || schema[0].Length != 3 || schema[0][1] != "0" || schema[0][2] != "djI=")
                throw new InvalidOperationException("System dependency diagnostics require one v2 schema.");
            var selected = rows.Where(row => row[0] == "system-dependencies").OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture)).ToArray();
            for (var i = 0; i < selected.Length; ++i)
                if (selected[i].Length != 3 || selected[i][1] != i.ToString(CultureInfo.InvariantCulture)) throw new InvalidOperationException("Invalid dependency diagnostic ordinal.");
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
            var selected = rows.Where(row => row.StartsWith("systemdependency-registration-owner\t", StringComparison.Ordinal)).ToArray();
            var schemas = rows.Where(row => row.StartsWith("systemdependency-publication-schema\t", StringComparison.Ordinal)).ToArray();
            if (!editor && selected.Length == 0 && schemas.Length == 0) return Array.Empty<Envelope.Document>();
            if (!editor || selected.Length != 1 || schemas.Length != 1 || schemas[0] != "systemdependency-publication-schema\t0\tdjE=")
                throw new InvalidOperationException("System dependency diagnostics need one Editor owner.");
            var fields = selected[0].Split('\t');
            if (fields.Length != 4 || fields[1] != "0") throw new InvalidOperationException("Invalid dependency diagnostic owner.");
            var owner = Envelope.Decode(fields[3]);
            var entry = Envelope.Decode(fields[2]);
            if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) || entry != EntryValue(rows))
                throw new InvalidOperationException("Dependency diagnostics differ from their selected input slice.");
            return new[] { new Envelope.Document { Owner = owner, Editor = true, Count = 1,
                Plan = SourceGeneratorNames.Hash("ME.BECS.SystemDependencySelection.v1\n" + selected[0] + "\n"), Entries = new[] { new KeyValuePair<int, string>(0, entry) } } };
        }
    }
}
