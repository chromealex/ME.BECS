using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Format = ME.BECS.CodeGeneration.SourceGeneratorNetworkFragmentFormat;
using Envelope = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

namespace ME.BECS.SourceGenerator;

internal sealed class NetworkRegistrationOwners {
    private readonly List<string> rows = new();
    private readonly List<string> identities = new();
    internal bool Distributed { get; private set; }
    internal string Plan => Format.Plan(this.rows);
    internal int Count => this.identities.Count;

    internal bool Read(string[] fields) {
        if (fields[0] == "network-publication-schema") {
            if (this.Distributed || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            this.Distributed = true;
            return true;
        }
        if (!this.Distributed || fields.Length != 4 || fields[1] != this.Count.ToString(CultureInfo.InvariantCulture)) return false;
        try {
            var identity = Envelope.Decode(fields[2]);
            var owner = Envelope.Decode(fields[3]);
            if (!Format.ValidIdentity(identity) || this.identities.Contains(identity, StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(owner) || owner.Any(char.IsControl) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)) return false;
            this.identities.Add(identity);
            this.rows.Add(string.Join("\t", fields));
            return true;
        } catch (FormatException) { return false; }
        catch (System.Text.DecoderFallbackException) { return false; }
    }

    internal bool Matches(IReadOnlyList<(string Owner, IMethodSymbol Method)> methods) {
        if (!this.Distributed) return true; // Upgrade an old snapshot through normal Editor export.
        if (methods.Count != this.Count) return false;
        for (var i = 0; i < methods.Count; ++i)
            if (this.identities[i] != Format.EntryValue(methods[i].Owner, methods[i].Method.Name)) return false;
        return true;
    }
}
