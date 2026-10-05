using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Format = ME.BECS.CodeGeneration.SourceGeneratorJobInitFragmentFormat;
using Envelope = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

namespace ME.BECS.SourceGenerator;

internal sealed class JobInitRegistrationOwners {
    private readonly List<string> rows = new();
    private readonly List<string> entries = new();
    internal bool Distributed { get; private set; }
    internal string Plan => Format.Plan(this.rows);
    internal bool Read(string[] fields) {
        if (fields[0] == "jobinit-publication-schema") {
            if (this.Distributed || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            this.Distributed = true; return true;
        }
        if (!this.Distributed || fields.Length != 4 || fields[1] != this.entries.Count.ToString(CultureInfo.InvariantCulture)) return false;
        try {
            var entry = Envelope.Decode(fields[2]);
            var owner = Envelope.Decode(fields[3]);
            if (!Format.ValidEntry(entry) || string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) return false;
            this.entries.Add(entry); this.rows.Add(string.Join("\t", fields)); return true;
        } catch (FormatException) { return false; }
        catch (System.Text.DecoderFallbackException) { return false; }
    }
    internal bool Matches(JobBootstrapInputEmitter jobs) => !this.Distributed || jobs.HasSchema &&
        this.entries.SequenceEqual(jobs.Slots.Select(slot => Format.EntryValue(slot.Payload)), StringComparer.Ordinal);
}
