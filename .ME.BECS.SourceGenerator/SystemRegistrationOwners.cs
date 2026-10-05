using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ME.BECS.SourceGenerator;

internal sealed class SystemRegistrationOwners {
    private readonly List<(string Identity, string Owner)> entries = new();
    private readonly List<string> rows = new();
    internal bool HasSchema { get; private set; }
    internal bool Distributed { get; private set; }
    internal string Plan => ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat.Plan(this.rows);

    internal bool Read(string[] fields) {
        if (fields[0] == "system-publication-schema") {
            if (!this.HasSchema || this.Distributed || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            this.Distributed = true;
            return true;
        }
        if (fields[0] == "system-registration-owners-schema") {
            if (this.HasSchema || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            this.HasSchema = true;
            return true;
        }
        if (!this.HasSchema || fields.Length != 4 || fields[1] != this.entries.Count.ToString(CultureInfo.InvariantCulture)) return false;
        try {
            var identity = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2]));
            var owner = Encoding.UTF8.GetString(Convert.FromBase64String(fields[3]));
            if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(owner) || owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) ||
                Array.Exists(identity.ToCharArray(), char.IsControl) || Array.Exists(owner.ToCharArray(), char.IsControl)) return false;
            this.entries.Add((identity, owner));
            this.rows.Add(string.Join("\t", fields));
            return true;
        } catch (FormatException) { return false; }
    }

    internal bool Matches(IReadOnlyList<(string Identity, Microsoft.CodeAnalysis.INamedTypeSymbol Type)> systems) {
        if (!this.HasSchema) return true; // An already compiled old snapshot may await automatic export.
        if (systems.Count != this.entries.Count) return false;
        for (var i = 0; i < systems.Count; ++i) if (systems[i].Identity != this.entries[i].Identity) return false;
        return true;
    }

    internal string Owner(int ordinal, string legacyOwner) => this.HasSchema ? this.entries[ordinal].Owner : legacyOwner;
}
