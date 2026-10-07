using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Fresh IL selects graph job identities. Roslyn validates that snapshot and owns
// injection bodies. Old source/legacy selections remain readable during upgrade.
internal sealed class GraphJobSelectionInput {
    internal INamedTypeSymbol Owner = null!;
    internal readonly List<(INamedTypeSymbol Type, string Identity)> Jobs = new();
    private bool sourceOnly;
    internal string Origin = "legacy";

    internal static GraphJobSelectionInput? Create(INamedTypeSymbol? owner, string payload, InputManifestTypes resolver) {
        if (owner == null) return null;
        var result = new GraphJobSelectionInput { Owner = owner };
        if (payload.StartsWith("v2\n", StringComparison.Ordinal)) {
            if (payload == "v2\nsource") { result.sourceOnly = true; return result; }
            if (payload.StartsWith("v2\nil\n", StringComparison.Ordinal)) {
                result.Origin = "il";
                payload = payload.Substring("v2\nil\n".Length);
            } else {
                if (!payload.StartsWith("v2\nlegacy\n", StringComparison.Ordinal)) return null;
                payload = payload.Substring("v2\nlegacy\n".Length);
            }
        }
        if (payload.Length == 0) return result;
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var identity in payload.Split('\n')) {
            if (identity.Length == 0 || identity.Any(char.IsControl)) return null;
            var job = resolver.ResolveDefinition(identity, out _);
            if (job == null || job.TypeKind != TypeKind.Struct || job.IsRefLikeType || MethodSummaryType.From(job).IsOpen || !seen.Add(job)) return null;
            result.Jobs.Add((job, identity));
        }
        return result;
    }

    internal bool Select(InputManifestTypes resolver, Compilation compilation, out string error) {
        if (this.Origin == "il") {
            error = "Invalid IL scheduled-job snapshot for " + this.Owner;
            var contract = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
            if (contract == null || !this.Owner.IsValueType || this.Owner.IsRefLikeType || MethodSummaryType.From(this.Owner).IsOpen ||
                !this.Owner.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) ||
                !compilation.IsSymbolAccessibleWithin(this.Owner, compilation.Assembly)) return false;
            foreach (var entry in this.Jobs) {
                // Discovery includes private framework helpers (e.g. QueryBuilder.DisposeJob).
                // Merely recording their identity emits no C# type reference. CreateJob
                // validates accessibility later, but only when fields need a patch.
                if (!entry.Type.IsUnmanagedType ||
                    entry.Identity != JobSafetySummary.ReflectionIdentity(entry.Type) ||
                    !entry.Type.AllInterfaces.Any(type => type.Name.StartsWith("IJob", StringComparison.Ordinal) &&
                        (type.ContainingNamespace.ToDisplayString() == "Unity.Jobs" || type.ContainingNamespace.ToDisplayString() == "ME.BECS.Jobs"))) {
                    error += ": invalid job " + entry.Identity;
                    return false;
                }
            }
            string FullName(INamedTypeSymbol type) {
                var identity = JobSafetySummary.ReflectionIdentity(type)!;
                return identity.Substring(0, identity.Length - type.ContainingAssembly.Identity.ToString().Length - 2);
            }
            if (!this.Jobs.Select(entry => entry.Type).SequenceEqual(this.Jobs.Select(entry => entry.Type)
                    .OrderBy(FullName, StringComparer.Ordinal).ThenBy(type => type.ContainingAssembly.Identity.ToString(), StringComparer.Ordinal), SymbolEqualityComparer.Default)) {
                error += ": job identities are not in canonical order";
                return false;
            }
            error = "";
            return true; // Explicit IL must never consult or be replaced by source catalogs.
        }
        var status = resolver.ScheduledJobs.Read(this.Owner, resolver, compilation, out var jobs, out error);
        if (status == CompilerJobCatalogs.Status.Invalid || status != CompilerJobCatalogs.Status.Complete && this.sourceOnly) return false;
        if (status == CompilerJobCatalogs.Status.Complete) {
            this.Origin = "source";
            this.Jobs.Clear();
            this.Jobs.AddRange(jobs.Select(job => (job, JobSafetySummary.ReflectionIdentity(job)!)));
        }
        error = "";
        return true;
    }

    internal string Describe(int graph) => "v1\n" + graph.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" +
        JobSafetySummary.ReflectionIdentity(this.Owner) + "\n" + this.Origin + "\n" + string.Join("\n", this.Jobs.Select(job => job.Identity));
}
