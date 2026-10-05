#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    internal static class SourceGeneratorJobSetupFragmentFormat {
        internal const string MetadataKey = "ME.BECS.JobSetupFragment.v1";
        internal static string Pack(params string[] fields) => string.Join("|", fields.Select(Envelope.Encode));
        internal static string[] Unpack(string entry) => entry.Split('|').Select(Envelope.Decode).ToArray();
        internal static bool ValidEntry(string entry) {
            try {
                var fields = Unpack(entry);
                return fields.Length == 7 && fields[0] == "v1" && !string.IsNullOrWhiteSpace(fields[1]) &&
                    !fields[1].Any(char.IsControl) && uint.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count) &&
                    count <= ushort.MaxValue + 1u && fields[2] == count.ToString(CultureInfo.InvariantCulture) &&
                    fields[3].StartsWith("job-weight\t", StringComparison.Ordinal) && fields[3].Split('\t').Length == 5 &&
                    Envelope.Decode(fields[3].Split('\t')[2]) == fields[1] &&
                    (fields[4].StartsWith("job-entity-il\t", StringComparison.Ordinal) || fields[4].StartsWith("job-entity-fallback\t", StringComparison.Ordinal)) &&
                    fields[4].Split('\t').Length == 3 && Envelope.Decode(fields[4].Split('\t')[2]).Split('\n').Length >= 5 &&
                    fields[5].Length != 0 && fields[5].Split('\n').All(row => row.StartsWith("job-debug\t", StringComparison.Ordinal) &&
                        row.Split('\t').Length == 3 && Envelope.Decode(row.Split('\t')[2]).Split('\n').Length >= 5) &&
                    fields[6].Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).All(row => row.Split('\t').Length == 4) && Pack(fields) == entry;
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }

        // Bind only used entity groups, but retain their global IDs/count. Passing
        // every entity type to every owner would introduce unrelated references.
        internal static string[] Selection(IEnumerable<string> records, IReadOnlyDictionary<string, KeyValuePair<int, string>> groups, int groupCount) {
            var rows = records.Select(row => row.Split('\t')).ToArray();
            var weights = rows.Where(row => row[0] == "job-weight").OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture)).ToArray();
            var entities = rows.Where(row => row[0] == "job-entity-il" || row[0] == "job-entity-fallback")
                .ToDictionary(row => Envelope.Decode(row[2]).Split('\n')[1], row => row, StringComparer.Ordinal);
            var debug = rows.Where(row => row[0] == "job-debug").GroupBy(row => Envelope.Decode(row[2]).Split('\n')[1])
                .ToDictionary(group => group.Key, group => string.Join("\n", group.Select(row => string.Join("\t", row))), StringComparer.Ordinal);
            if (weights.Length != entities.Count || weights.Length != debug.Count) throw new InvalidOperationException("Incomplete job statistics selection.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return weights.Select((weight, ordinal) => {
                var identity = Envelope.Decode(weight[2]);
                if (weight[1] != ordinal.ToString(CultureInfo.InvariantCulture) || !seen.Add(identity) ||
                    !entities.TryGetValue(identity, out var entity) || !debug.TryGetValue(identity, out var layout))
                    throw new InvalidOperationException("Mismatched ordered job statistics selection.");
                var bindings = Envelope.Decode(entity[2]).Split('\n').Skip(5).Select(row => {
                    var parts = row.Split('\t');
                    var key = parts[0] + "\t" + parts[1];
                    if (!groups.TryGetValue(key, out var binding) || binding.Key < 0 || binding.Key >= groupCount)
                        throw new InvalidOperationException("Unregistered entity group in job statistics: " + key);
                    return (binding.Key, Row: binding.Key.ToString(CultureInfo.InvariantCulture) + "\t" + binding.Value + "\t" + key);
                }).OrderBy(item => item.Key).Select(item => item.Row);
                return Pack("v1", identity, groupCount.ToString(CultureInfo.InvariantCulture), string.Join("\t", weight),
                    string.Join("\t", entity), layout, string.Join("\n", bindings));
            }).ToArray();
        }

        internal static string Plan(IEnumerable<string> rows) => SourceGeneratorNames.Hash("ME.BECS.JobSetupSelection.v1\n" + string.Join("\n", rows) + "\n");
        internal static string FileName(string owner, bool editor) => "JobSetupFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("JobSetupFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (document.Entries.Any(entry => !ValidEntry(entry.Value)) ||
                document.Entries.Select(entry => Unpack(entry.Value)[1]).Distinct(StringComparer.Ordinal).Count() != document.Entries.Length) {
                document = null; return false;
            }
            return true;
        }
        internal static Envelope.Document[] Documents(IEnumerable<string> records, bool editor) {
            var rows = records.Where(row => row.StartsWith("jobsetup-registration-owner\t", StringComparison.Ordinal))
                .OrderBy(row => int.Parse(row.Split('\t')[1], CultureInfo.InvariantCulture)).ToArray();
            var plan = Plan(rows);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return rows.Select((row, ordinal) => {
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[1] != ordinal.ToString(CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Invalid ordered job statistics selection.");
                var entry = Envelope.Decode(fields[2]);
                var owner = Envelope.Decode(fields[3]);
                if (!ValidEntry(entry) || !seen.Add(Unpack(entry)[1]) || string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) ||
                    owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid job statistics owner/entry.");
                return (Owner: owner, Entry: new KeyValuePair<int, string>(ordinal, entry));
            }).GroupBy(item => item.Owner, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Envelope.Document { Owner = group.Key, Editor = editor, Plan = plan, Count = rows.Length,
                    Entries = group.Select(item => item.Entry).ToArray() }).ToArray();
        }
    }
}
