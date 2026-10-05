using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Format = ME.BECS.CodeGeneration.SourceGeneratorTypeFragmentFormat;
using Envelope = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

namespace ME.BECS.SourceGenerator;

internal sealed class TypeRegistrationOwners {
    private readonly List<string> rows = new();
    private readonly List<Format.Entry> entries = new();
    internal bool Distributed { get; private set; }
    internal int Count => this.entries.Count;
    internal string Plan => Format.Plan(this.rows);

    internal bool Read(string[] fields) {
        if (fields[0] == "type-publication-schema") {
            if (this.Distributed || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            this.Distributed = true;
            return true;
        }
        if (!this.Distributed || fields.Length != 4 || fields[1] != this.Count.ToString(CultureInfo.InvariantCulture)) return false;
        try {
            if (!Format.TryEntry(Envelope.Decode(fields[2]), out var entry)) return false;
            var owner = Envelope.Decode(fields[3]);
            if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) return false;
            this.entries.Add(entry);
            this.rows.Add(string.Join("\t", fields));
            return true;
        } catch (FormatException) { return false; }
        catch (System.Text.DecoderFallbackException) { return false; }
    }

    internal bool Matches(IReadOnlyList<(INamedTypeSymbol Component, INamedTypeSymbol Group)> groups,
        IReadOnlyList<(string Identity, INamedTypeSymbol Type, int Flags)> components, InputManifestTypes resolver) {
        if (!this.Distributed) return true; // A compiled old snapshot can recover through normal export.
        var ordinal = 0;
        foreach (var group in groups) {
            if (ordinal >= this.Count) return false;
            var entry = this.entries[ordinal++];
            var selectedGroup = resolver.ResolveDefinition(entry.Group, out _);
            if (selectedGroup != null && MethodSummaryType.From(selectedGroup).IsOpen) selectedGroup = selectedGroup.ConstructUnboundGenericType();
            if (entry.Phase != "Group" || !SymbolEqualityComparer.Default.Equals(group.Component, resolver.ResolveDefinition(entry.Component, out _)) ||
                !SymbolEqualityComparer.Default.Equals(group.Group, selectedGroup)) return false;
        }
        foreach (var phase in new[] { (Flag: 0, Name: "Register"), (Flag: 8, Name: "RegisterShared"),
                     (Flag: 2, Name: "RegisterStatic"), (Flag: 32, Name: "RegisterConfig") })
            foreach (var component in components.Where(item => phase.Flag == 0 || (item.Flags & phase.Flag) != 0)) {
                if (ordinal >= this.Count) return false;
                var entry = this.entries[ordinal++];
                if (entry.Phase != phase.Name || entry.Component != component.Identity || entry.Group.Length != 0) return false;
            }
        return ordinal == this.Count;
    }
}
