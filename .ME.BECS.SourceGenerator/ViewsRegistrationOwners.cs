using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Format = ME.BECS.CodeGeneration.SourceGeneratorViewsFragmentFormat;
using Envelope = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

namespace ME.BECS.SourceGenerator;

internal sealed class ViewsRegistrationOwners {
    private readonly List<string> rows = new();
    private readonly List<string> entries = new();
    internal bool Distributed { get; private set; }
    internal string Plan => Format.Plan(this.rows);
    internal bool Read(string[] fields) {
        if (fields[0] == "views-publication-schema") {
            if (this.Distributed || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            this.Distributed = true; return true;
        }
        if (!this.Distributed || fields.Length != 4 || fields[1] != this.entries.Count.ToString(CultureInfo.InvariantCulture)) return false;
        try {
            var value = Envelope.Decode(fields[2]);
            if (!Format.TryEntry(value, out var entry) || this.entries.Contains(value, StringComparer.Ordinal)) return false;
            var owner = Envelope.Decode(fields[3]);
            if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) return false;
            this.entries.Add(value); this.rows.Add(string.Join("\t", fields));
            return true;
        } catch (FormatException) { return false; }
        catch (System.Text.DecoderFallbackException) { return false; }
    }
    internal bool Matches(ViewTrackerInputEmitter trackers, ViewTypeInputEmitter views) => !this.Distributed ||
        trackers.Capacity >= 0 && views.HasSchema && this.entries.SequenceEqual(
            trackers.Tracked.Select(type => Format.EntryValue("Component", JobSafetySummary.ReflectionIdentity(type)))
            .Concat(trackers.Groups.Select(entry => Format.EntryValue(entry.Module ? "ModuleTracker" : "ViewTracker", JobSafetySummary.ReflectionIdentity(entry.Type))))
            .Concat(views.Types.Select(entry => Format.EntryValue("ViewType", entry.Identity))), StringComparer.Ordinal);
}
