using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Format = ME.BECS.CodeGeneration.SourceGeneratorJobDebugFragmentFormat;
using Envelope = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

namespace ME.BECS.SourceGenerator;

internal sealed class JobDebugRegistrationOwners {
    private readonly List<string> rows = new();
    private readonly List<string> entries = new();
    internal bool Distributed { get; private set; }
    internal string Plan => Format.Plan(this.rows);
    internal int Count => this.entries.Count;
    internal bool Read(string[] fields) {
        if (fields[0] == "jobdebug-publication-schema") {
            if (Distributed || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            Distributed = true; return true;
        }
        if (!Distributed || fields.Length != 4 || fields[1] != Count.ToString(CultureInfo.InvariantCulture)) return false;
        try {
            var entry = Envelope.Decode(fields[2]);
            var owner = Envelope.Decode(fields[3]);
            if (!Format.ValidEntry(entry) || string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) return false;
            entries.Add(entry); rows.Add(string.Join("\t", fields)); return true;
        } catch (FormatException) { return false; }
        catch (System.Text.DecoderFallbackException) { return false; }
    }
    internal bool Matches(IEnumerable<string> records) => !Distributed || entries.SequenceEqual(records
        .Where(row => row.StartsWith("job-debug\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
        .OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture))
        .Select(row => Format.EntryValue(Envelope.Decode(row[2]))), StringComparer.Ordinal);
}
