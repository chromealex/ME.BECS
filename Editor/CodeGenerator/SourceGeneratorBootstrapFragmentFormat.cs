#nullable disable
namespace ME.BECS.CodeGeneration {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Envelope = SourceGeneratorSystemFragmentFormat;

    // A type-free composition root. Only plan identities/counts, feeder kinds
    // and job ordinals cross this boundary; no project types or executable C#.
    internal static class SourceGeneratorBootstrapFragmentFormat {
        internal const string MetadataKey = "ME.BECS.BootstrapFragment.v1";
        private static readonly string[] Kinds = { "System", "Type", "Entity", "Aspect", "Destroy", "Config", "Network", "JobInit", "JobSetup", "JobDebug", "Graph" };
        private static readonly string[] Initializers = { "none", "aspects", "entities", "config-counts", "views", "jobs" };
        private static readonly string[] Registrations = { "none", "aspect-construction", "config-callbacks", "destroy-callbacks", "network-methods", "view-types" };
        internal sealed class Selection { internal string Kind, Plan; internal int[] Counts; }
        internal sealed class Profile {
            internal Selection[] Selections;
            internal string[] Initialize, Register;
            internal int[] Jobs;
        }
        internal static string FileName(string owner, bool editor) => "BootstrapFragment" + SourceGeneratorNames.Hash(owner) + (editor ? "Editor" : "Runtime") + Envelope.NativeSuffix;
        internal static bool IsInput(string path) => System.IO.Path.GetFileName(path.Replace('\\', '/')).StartsWith("BootstrapFragment", StringComparison.Ordinal) && path.EndsWith(Envelope.NativeSuffix, StringComparison.Ordinal);
        internal static string Serialize(Envelope.Document document) => Envelope.SerializeEnvelope(document, MetadataKey);
        internal static string EntryValue(Profile profile) => string.Join("|", new[] {
            "v1", string.Join("\n", profile.Selections.Select(item => item.Kind + "\t" + item.Plan + "\t" + string.Join(",", item.Counts.Select(Number)))),
            string.Join(",", profile.Initialize), string.Join(",", profile.Register), string.Join(",", profile.Jobs.Select(Number)),
        }.Select(Envelope.Encode));
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static bool Canonical(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && text == Number(value);
        private static bool Hash(string value) => value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
        internal static bool TryEntry(string value, bool editor, out Profile profile) {
            profile = null;
            try {
                var fields = value.Split('|').Select(Envelope.Decode).ToArray();
                if (fields.Length != 5 || fields[0] != "v1") return false;
                var selections = new List<Selection>();
                foreach (var row in fields[1].Split('\n')) {
                    var columns = row.Split('\t');
                    if (columns.Length != 3 || !Kinds.Contains(columns[0]) || !Hash(columns[1])) return false;
                    var counts = new List<int>();
                    foreach (var count in columns[2].Split(',')) { if (!Canonical(count, out var parsed)) return false; counts.Add(parsed); }
                    if (counts.Count != (columns[0] == "Config" ? 3 : 1) || counts.Sum(count => (long)count) > int.MaxValue) return false;
                    selections.Add(new Selection { Kind = columns[0], Plan = columns[1], Counts = counts.ToArray() });
                }
                var expected = Kinds.Where(kind => (kind != "Graph" || !editor) && (kind != "Network" || selections.Any(item => item.Kind == "Network")));
                if (!selections.Select(item => item.Kind).SequenceEqual(expected)) return false;
                var initialize = fields[2].Split(',');
                var register = fields[3].Split(',');
                if (initialize.Length != register.Length || initialize.Any(kind => !Initializers.Contains(kind)) || register.Any(kind => !Registrations.Contains(kind)) ||
                    register.Contains("network-methods") != selections.Any(item => item.Kind == "Network") || initialize.Contains("views") != register.Contains("view-types")) return false;
                var jobs = new List<int>();
                foreach (var ordinal in fields[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { if (!Canonical(ordinal, out var index)) return false; jobs.Add(index); }
                var setupCount = selections.Single(item => item.Kind == "JobSetup").Counts[0];
                if (jobs.Count != selections.Single(item => item.Kind == "JobInit").Counts[0] || jobs.Any(index => index >= setupCount) || jobs.Distinct().Count() != setupCount) return false;
                profile = new Profile { Selections = selections.ToArray(), Initialize = initialize, Register = register, Jobs = jobs.ToArray() };
                if (EntryValue(profile) != value) { profile = null; return false; }
                return true;
            } catch (FormatException) { return false; }
            catch (System.Text.DecoderFallbackException) { return false; }
        }

        internal static Profile Create(IEnumerable<string> records, bool editor) {
            var rows = records.Select(row => row.Split('\t')).ToArray();
            string[][] Ordered(string kind) {
                var selected = rows.Where(row => row[0] == kind).OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture)).ToArray();
                for (var i = 0; i < selected.Length; ++i) if (selected[i][1] != Number(i)) throw new InvalidOperationException("Invalid bootstrap selection ordinals: " + kind);
                return selected;
            }
            var selections = new List<Selection>();
            foreach (var kind in Kinds) {
                if (kind == "Graph" && editor || kind == "Network" && !rows.Any(row => row[0] == "network-method-schema")) continue;
                var prefix = kind.ToLowerInvariant();
                if (rows.Count(row => row.Length == 3 && row[0] == prefix + "-publication-schema" && row[1] == "0" && row[2] == "djE=") != 1)
                    throw new InvalidOperationException("Missing bootstrap publication schema: " + prefix);
                var selected = Ordered(prefix + "-registration-owner");
                var raw = selected.Select(row => string.Join("\t", row)).ToArray();
                var plan = kind == "System" ? Envelope.Plan(raw) : SourceGeneratorNames.Hash("ME.BECS." + kind + "Selection.v1\n" + string.Join("\n", raw) + "\n");
                var counts = new[] { selected.Length };
                if (kind == "Config") {
                    var phases = selected.Select(row => {
                        if (!SourceGeneratorConfigFragmentFormat.TryEntry(Envelope.Decode(row[2]), out var entry)) throw new InvalidOperationException("Invalid config phase in bootstrap.");
                        return entry.Phase;
                    }).ToArray();
                    counts = SourceGeneratorConfigFragmentFormat.Phases.Select(phase => phases.Count(value => value == phase)).ToArray();
                }
                selections.Add(new Selection { Kind = kind, Plan = plan, Counts = counts });
            }
            var feeders = Ordered("bootstrap-feeder");
            if (feeders.Any(row => row.Length != 5)) throw new InvalidOperationException("Bootstrap requires compiler-owned feeder kinds.");
            var setups = Ordered("jobsetup-registration-owner").ToDictionary(row => SourceGeneratorJobSetupFragmentFormat.Unpack(Envelope.Decode(row[2]))[1], row => int.Parse(row[1], CultureInfo.InvariantCulture), StringComparer.Ordinal);
            var jobs = Ordered("jobinit-registration-owner").Select(row => setups[SourceGeneratorJobInitFragmentFormat.Payload(Envelope.Decode(row[2])).Split('\n')[1]]).ToArray();
            var result = new Profile { Selections = selections.ToArray(), Initialize = feeders.Select(row => row[3]).ToArray(), Register = feeders.Select(row => row[4]).ToArray(), Jobs = jobs };
            if (!TryEntry(EntryValue(result), editor, out _)) throw new InvalidOperationException("Incomplete bootstrap composition data.");
            return result;
        }

        internal static Envelope.Document[] Documents(IEnumerable<string> records, bool editor) {
            var rows = records.ToArray();
            var selected = rows.Where(row => row.StartsWith("bootstrap-registration-owner\t", StringComparison.Ordinal)).ToArray();
            var schemas = rows.Where(row => row.StartsWith("bootstrap-publication-schema\t", StringComparison.Ordinal)).ToArray();
            if (selected.Length != 1 || schemas.Length != 1 || schemas[0] != "bootstrap-publication-schema\t0\tdjE=")
                throw new InvalidOperationException("Bootstrap must select exactly one composition owner and schema.");
            var fields = selected[0].Split('\t');
            if (fields.Length != 4 || fields[1] != "0") throw new InvalidOperationException("Invalid bootstrap composition owner.");
            var owner = Envelope.Decode(fields[3]);
            var payload = Envelope.Decode(fields[2]);
            if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) ||
                payload != EntryValue(Create(rows, editor))) throw new InvalidOperationException("Bootstrap composition differs from its complete ordered selection.");
            return new[] { new Envelope.Document { Owner = owner, Editor = editor, Count = 1,
                Plan = SourceGeneratorNames.Hash("ME.BECS.BootstrapSelection.v1\n" + selected[0] + "\n"), Entries = new[] { new KeyValuePair<int, string>(0, payload) } } };
        }
        internal static bool TryParse(string content, out Envelope.Document document) {
            if (!Envelope.TryParseEnvelope(content, MetadataKey, out document)) return false;
            if (document.Entries.Length == 0) return true; // retired owner
            if (document.Count != 1 || document.Entries.Length != 1 || document.Entries[0].Key != 0 || !TryEntry(document.Entries[0].Value, document.Editor, out _)) { document = null; return false; }
            return true;
        }
    }
}
