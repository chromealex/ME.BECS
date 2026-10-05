using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Status = ME.BECS.SourceGenerator.CompilerJobCatalogs.Status;

namespace ME.BECS.SourceGenerator;

// Diagnostic/source-format compatibility reader. Explicit IL graph selections do
// not consult these catalogs, even when source coverage is complete or malformed.
internal sealed class CompilerScheduledJobs {
    private readonly Dictionary<(string Assembly, string Owner), List<(IAssemblySymbol Publisher, string[] Rows)>> records = new();
    private readonly System.Threading.CancellationToken cancellation;

    internal CompilerScheduledJobs(IEnumerable<IAssemblySymbol> assemblies, System.Threading.CancellationToken cancellation) {
        this.cancellation = cancellation;
        foreach (var assembly in assemblies.OrderBy(assembly => assembly.Identity.ToString(), StringComparer.Ordinal))
            foreach (var attribute in assembly.GetAttributes()) {
                cancellation.ThrowIfCancellationRequested();
                if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" || attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value as string != "ME.BECS.SystemScheduledJobs.v1" || attribute.ConstructorArguments[1].Value is not string value) continue;
                var rows = value.Split('\n');
                if (rows[0].Length == 0) continue;
                var key = (rows[0].Contains("[[") ? "" : assembly.Identity.ToString(), rows[0]);
                if (!this.records.TryGetValue(key, out var entries)) this.records.Add(key, entries = new());
                entries.Add((assembly, rows));
            }
    }

    internal Status Read(INamedTypeSymbol owner, InputManifestTypes resolver, Compilation compilation, out INamedTypeSymbol[] jobs, out string error) {
        jobs = Array.Empty<INamedTypeSymbol>();
        var identity = JobSafetySummary.ReflectionIdentity(owner);
        error = "Invalid compiler scheduled-job owner: " + owner;
        var system = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
        if (identity == null || system == null || !owner.IsValueType || owner.IsRefLikeType ||
            !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly) || !owner.AllInterfaces.Contains(system, SymbolEqualityComparer.Default)) return Status.Invalid;
        var roots = CompilerSystemRoots.Read(owner, resolver, compilation);
        if (roots == null) return Status.Invalid;
        var generic = MethodSummaryType.TypeOwners(owner).Any(type => type.Arity != 0);
        var key = generic ? identity : identity.Substring(0, identity.Length - owner.ContainingAssembly.Identity.ToString().Length - 2);
        error = "Invalid compiler scheduled jobs for " + identity;
        if (!this.records.TryGetValue((generic ? "" : owner.ContainingAssembly.Identity.ToString(), key), out var entries)) {
            if (roots.Length == 0) { error = ""; return Status.Complete; }
            error = "Missing compiler scheduled jobs for " + identity;
            return Status.Missing;
        }
        var seenRoots = new HashSet<string>(StringComparer.Ordinal);
        var result = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var incomplete = false;
        foreach (var (publisher, rows) in entries) {
            this.cancellation.ThrowIfCancellationRequested();
            if (rows.Length < 3 || !roots.Any(root => root.Id == rows[1]) || !seenRoots.Add(rows[1]) ||
                !CompilerJobCatalogs.Number(rows[2], out var gapCount)) return Status.Invalid;
            var gapExamples = 0;
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows.Skip(3)) {
                if (row.Length == 0) continue; // v1 writers separate empty job/gap lists with blank lines.
                var fields = row.Split('\t');
                if (fields.Length != 2 || fields[1].Length == 0 || fields[0] is not ("J" or "G")) return Status.Invalid;
                if (fields[0] == "J") { if (!tokens.Add(fields[1])) return Status.Invalid; }
                // Missing instantiations are distinct graph nodes, but v1 diagnostic
                // examples omit their type arguments and may legitimately repeat.
                else ++gapExamples;
            }
            if (gapExamples != Math.Min(gapCount, 12u)) return Status.Invalid;
            if (gapCount != 0) { incomplete = true; continue; }
            var holder = publisher.GetTypeByMetadataName("ME.BECS.SourceGenerated.ScheduledJobs_" +
                ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(key + "\n" + rows[1]));
            // Older source summaries can have zero call-graph gaps but lack typed
            // helpers, e.g. an inaccessible scheduled job. Never certify that set.
            if (holder == null) { incomplete = true; continue; }
            if (!CompilerSystemRoots.Getter(holder, "GetRoot", compilation.GetTypeByMetadataName("System.Reflection.MethodInfo"), false, compilation) ||
                !CompilerSystemRoots.Getter(holder, "GetJobs", compilation.GetTypeByMetadataName("System.Type"), true, compilation)) return Status.Invalid;
            var unique = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var token in tokens) {
                if (!MethodSummaryType.TryDecode(token, out var expression) || expression!.IsOpen || expression.IsUnsupported ||
                    MethodSummaryTypeResolver.Resolve(expression, compilation) is not INamedTypeSymbol job || job.TypeKind != TypeKind.Struct || !job.IsUnmanagedType || job.IsRefLikeType ||
                    !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly) || !unique.Add(job)) return Status.Invalid;
                result.Add(job);
            }
        }
        if (incomplete || seenRoots.Count != roots.Length) { error = "Incomplete compiler scheduled jobs for " + identity; return Status.Incomplete; }
        jobs = result.OrderBy(FullName, StringComparer.Ordinal).ThenBy(job => job.ContainingAssembly.Identity.ToString(), StringComparer.Ordinal).ToArray();
        error = "";
        return Status.Complete;
    }

    // Reflection FullName includes closed generic arguments; preserve the existing
    // per-owner registration order, not MethodSummaryType token/hash enumeration.
    private static string FullName(INamedTypeSymbol type) {
        var identity = JobSafetySummary.ReflectionIdentity(type)!;
        return identity.Substring(0, identity.Length - type.ContainingAssembly.Identity.ToString().Length - 2);
    }
}
