using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Format = ME.BECS.CodeGeneration.SourceGeneratorJobSetupFragmentFormat;
using Envelope = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

namespace ME.BECS.SourceGenerator;

internal sealed class JobSetupRegistrationOwners {
    private readonly List<string> rows = new();
    private readonly List<string> entries = new();
    private readonly Dictionary<string, int> ordinals = new(StringComparer.Ordinal);
    internal bool Distributed { get; private set; }
    internal string Plan => Format.Plan(this.rows);
    internal int Count => this.entries.Count;
    internal int Ordinal(string job) => this.ordinals[job];
    internal bool Read(string[] fields) {
        if (fields[0] == "jobsetup-publication-schema") {
            if (Distributed || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            Distributed = true; return true;
        }
        if (!Distributed || fields.Length != 4 || fields[1] != Count.ToString(CultureInfo.InvariantCulture)) return false;
        try {
            var entry = Envelope.Decode(fields[2]);
            var owner = Envelope.Decode(fields[3]);
            if (!Format.ValidEntry(entry) || string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) return false;
            var job = Format.Unpack(entry)[1];
            if (ordinals.ContainsKey(job)) return false;
            ordinals.Add(job, Count); entries.Add(entry); rows.Add(string.Join("\t", fields)); return true;
        } catch (FormatException) { return false; }
        catch (System.Text.DecoderFallbackException) { return false; }
    }
    internal bool Matches(IEnumerable<string> records, IReadOnlyList<INamedTypeSymbol> entities) {
        if (!Distributed) return true;
        try {
            var groups = entities.Select((type, id) => (Key: type.ContainingAssembly.Identity + "\t" + type.GetDocumentationCommentId(),
                    Value: new KeyValuePair<int, string>(id, JobSafetySummary.ReflectionIdentity(type) ??
                        throw new InvalidOperationException("Entity group has no reflection identity."))))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            return entries.SequenceEqual(Format.Selection(records, groups, entities.Count), StringComparer.Ordinal);
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
