using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Format = ME.BECS.CodeGeneration.SourceGeneratorConfigFragmentFormat;
using Envelope = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

namespace ME.BECS.SourceGenerator;

internal sealed class ConfigRegistrationOwners {
    private readonly List<string> rows = new();
    private readonly List<Format.Entry> entries = new();
    internal bool Distributed { get; private set; }
    internal string Plan => Format.Plan(this.rows);
    internal int PhaseCount(string phase) => this.entries.Count(entry => entry.Phase == phase);
    internal bool Read(string[] fields) {
        if (fields[0] == "config-publication-schema") {
            if (this.Distributed || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            this.Distributed = true;
            return true;
        }
        if (!this.Distributed || fields.Length != 4 || fields[1] != this.entries.Count.ToString(CultureInfo.InvariantCulture)) return false;
        try {
            if (!Format.TryEntry(Envelope.Decode(fields[2]), out var entry)) return false;
            var owner = Envelope.Decode(fields[3]);
            if (string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) ||
                this.entries.Any(item => item.Phase == entry.Phase && item.Component == entry.Component)) return false;
            this.entries.Add(entry);
            this.rows.Add(string.Join("\t", fields));
            return true;
        } catch (FormatException) { return false; }
        catch (System.Text.DecoderFallbackException) { return false; }
    }
    internal bool Matches(IReadOnlyList<ConfigMaskInputEmitter> masks, IReadOnlyList<INamedTypeSymbol> collections, InputManifestTypes resolver) {
        if (!this.Distributed) return true;
        var ordinal = 0;
        foreach (var phase in Format.Phases)
            foreach (var type in phase == "Masks" ? masks.Select(mask => mask.Type) : collections) {
                if (ordinal >= this.entries.Count) return false;
                var entry = this.entries[ordinal++];
                if (entry.Phase != phase || !SymbolEqualityComparer.Default.Equals(type, resolver.ResolveDefinition(entry.Component, out _))) return false;
            }
        return ordinal == this.entries.Count;
    }
}
