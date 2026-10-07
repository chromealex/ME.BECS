using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Status = ME.BECS.SourceGenerator.CompilerJobCatalogs.Status;

namespace ME.BECS.SourceGenerator;

// Dependency union and synchronization are independent proof domains. Neither
// reads IL, runs lifecycle methods nor invokes generated reflection getters.
internal sealed class CompilerSystemDependencies {
    private readonly Dictionary<(string Assembly, string Owner, bool Synchronization), List<(IAssemblySymbol Publisher, string[] Rows)>> records = new();
    private readonly System.Threading.CancellationToken cancellation;
    internal static readonly string[] Phases = { "Update", "Awake", "Start", "Destroy" };

    internal CompilerSystemDependencies(IEnumerable<IAssemblySymbol> assemblies, System.Threading.CancellationToken cancellation) {
        this.cancellation = cancellation;
        foreach (var assembly in assemblies.OrderBy(assembly => assembly.Identity.ToString(), StringComparer.Ordinal))
            foreach (var attribute in assembly.GetAttributes()) {
                cancellation.ThrowIfCancellationRequested();
                if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" || attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value is not string key || attribute.ConstructorArguments[1].Value is not string value ||
                    (key != SystemDependencySummary.MetadataKey && key != SystemSynchronizationSummary.MetadataKey)) continue;
                var rows = value.Split('\n');
                if (rows[0].Length == 0) continue;
                var identity = (rows[0].Contains("[[") ? "" : assembly.Identity.ToString(), rows[0], key == SystemSynchronizationSummary.MetadataKey);
                if (!this.records.TryGetValue(identity, out var bucket)) this.records.Add(identity, bucket = new());
                bucket.Add((assembly, rows));
            }
    }

    internal Status Read(INamedTypeSymbol owner, bool synchronization, InputManifestTypes resolver, Compilation compilation,
        out (INamedTypeSymbol Type, byte Mode)[] operations, out (int Code, string Message)[] errors, out string error) {
        operations = Array.Empty<(INamedTypeSymbol, byte)>();
        errors = Array.Empty<(int, string)>();
        var qualified = JobSafetySummary.ReflectionIdentity(owner);
        error = "Compiler system dependency selection requires an accessible closed system: " + owner;
        var systemContract = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
        if (qualified == null || systemContract == null || !owner.IsValueType || owner.IsRefLikeType ||
            !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly) || !owner.AllInterfaces.Contains(systemContract, SymbolEqualityComparer.Default)) return Status.Invalid;
        var generic = MethodSummaryType.TypeOwners(owner).Any(type => type.Arity != 0);
        var identity = generic ? qualified : qualified.Substring(0, qualified.Length - owner.ContainingAssembly.Identity.ToString().Length - 2);
        var domain = synchronization ? "synchronization" : "dependencies";
        error = "Invalid compiler system " + domain + " for " + qualified;
        var roots = CompilerSystemRoots.Read(owner, resolver, compilation);
        if (roots == null) return Status.Invalid;
        if (!this.records.TryGetValue((generic ? "" : owner.ContainingAssembly.Identity.ToString(), identity, synchronization), out var bucket)) {
            if (!roots.Any(root => root.Selected)) { error = ""; return Status.Complete; }
            error = "Missing compiler system " + domain + " for " + qualified;
            return Status.Missing;
        }
        var seenRoots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in bucket)
            if (record.Rows.Length < 3 || !roots.Any(root => root.Id == record.Rows[1]) || !seenRoots.Add(record.Rows[1])) return Status.Invalid;
        var result = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        var diagnostics = new List<(int Code, string Message)>();
        var incomplete = false;
        foreach (var root in roots.Where(root => root.Selected)) {
            this.cancellation.ThrowIfCancellationRequested();
            var rootId = root.Id;
            var matching = bucket.Where(record => record.Rows.Length >= 2 && record.Rows[1] == rootId).ToArray();
            if (matching.Length == 0) { incomplete = true; continue; }
            // Duplicate publishers are ambiguous even when their text matches,
            // following the independent Editor reader's lifecycle binding rule.
            if (matching.Length != 1) return Status.Invalid;
            var (publisher, rows) = matching[0];
            if (rows.Length < 3 || !CompilerJobCatalogs.Number(rows[2], out var gaps)) return Status.Invalid;
            var gapRows = rows.Skip(3).Where(row => row.StartsWith("G\t", StringComparison.Ordinal)).ToArray();
            if ((gaps == 0) != (gapRows.Length == 0) || gapRows.Any(row => row.Length == 2)) return Status.Invalid;
            if (synchronization) {
                if (!Synchronization(rows, out var status, out var sites)) return Status.Invalid;
                if (status == "incomplete") { incomplete = true; continue; }
                if (!RootBinding(publisher, rows, compilation)) return Status.Invalid;
                if (status == "unproven") {
                    var fullName = qualified.Substring(0, qualified.Length - owner.ContainingAssembly.Identity.ToString().Length - 2);
                    diagnostics.Add((0, "Method " + fullName + "." + root.Name +
                        " may access component data while work is still pending. Review synchronization at these sites; this advisory analysis may miss existing guarantees. Source sites: " + string.Join("; ", sites)));
                }
                continue;
            }
            if (gaps != 0) { incomplete = true; continue; }
            if (!RootBinding(publisher, rows, compilation)) return Status.Invalid;
            var seenComponents = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var seenSystems = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var planSeen = false;
            foreach (var row in rows.Skip(3)) {
                var fields = row.Split('\t');
                if (fields[0] == "R") continue;
                if (fields[0] == "P") {
                    var expected = "ME.BECS.SourceGenerated.SystemDependencyPlan_" +
                        ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(publisher.Identity + "\n" + rows[0] + "\n" + rows[1]);
                    if (planSeen || fields.Length != 4 || fields[1] != publisher.Identity.ToString() || fields[2] != expected || fields[3] != "v1") return Status.Invalid;
                    var type = publisher.GetTypeByMetadataName(expected);
                    if (!CompilerSystemRoots.Getter(type, "GetComponents", compilation.GetTypeByMetadataName("System.Type"), true, compilation) ||
                        !CompilerSystemRoots.Getter(type, "GetModes", compilation.GetSpecialType(SpecialType.System_Byte), true, compilation) ||
                        !CompilerSystemRoots.Getter(type, "GetSystems", compilation.GetTypeByMetadataName("System.Type"), true, compilation)) return Status.Invalid;
                    planSeen = true;
                    continue;
                }
                var system = fields[0] == "Y";
                if (system ? fields.Length != 2 : fields[0] != "C" || fields.Length != 3) return Status.Invalid;
                uint mode = 2;
                if (!system && (!CompilerJobCatalogs.Number(fields[1], out mode) || mode > 2)) return Status.Invalid;
                var symbol = resolver.ResolveDefinition(fields[system ? 1 : 2], out _);
                var contract = compilation.GetTypeByMetadataName(system ? "ME.BECS.ISystem" : "ME.BECS.IComponentBase");
                // Component entries describe types, not direct C# references.
                // Their public plan getter is emitted in the publisher, where
                // internal types are accessible. An external Editor consumer
                // retains them through exact identities in its diagnostic table.
                if (symbol == null || contract == null || !symbol.IsUnmanagedType || symbol.IsRefLikeType || MethodSummaryType.From(symbol).IsOpen ||
                    system && !compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly) || !symbol.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) ||
                    !(system ? seenSystems : seenComponents).Add(symbol)) return Status.Invalid;
                result.TryGetValue(symbol, out var old);
                result[symbol] = old | (mode == 2 ? 3 : (int)mode + 1);
            }
            if (!planSeen) return Status.Invalid;
        }
        if (incomplete) { error = "Incomplete compiler system " + domain + " for " + qualified; return Status.Incomplete; }
        operations = result.OrderBy(pair => JobSafetySummary.ReflectionIdentity(pair.Key), StringComparer.Ordinal)
            .Select(pair => (pair.Key, (byte)(pair.Value == 3 ? 2 : pair.Value - 1))).ToArray();
        errors = diagnostics.Distinct().ToArray();
        error = "";
        return Status.Complete;
    }

    private static bool RootBinding(IAssemblySymbol publisher, string[] rows, Compilation compilation) {
        var records = rows.Skip(3).Where(row => row.StartsWith("R\t", StringComparison.Ordinal)).ToArray();
        if (records.Length != 1) return false;
        var fields = records[0].Split('\t');
        var expected = "ME.BECS.SourceGenerated.SystemDirectRoot_" +
            ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(publisher.Identity + "\n" + rows[0] + "\n" + rows[1]);
        return fields.Length == 5 && fields[1] == publisher.Identity.ToString() && fields[2] == expected && fields[3] == "GetRoot" && fields[4] == "v1" &&
            CompilerSystemRoots.Getter(publisher.GetTypeByMetadataName(expected), "GetRoot", compilation.GetTypeByMetadataName("System.Reflection.MethodInfo"), false, compilation);
    }

    private static bool Synchronization(string[] rows, out string? status, out string[] sites) {
        status = null;
        sites = Array.Empty<string>();
        var counts = new Dictionary<string, uint>(StringComparer.Ordinal);
        var gaps = new HashSet<string>(StringComparer.Ordinal);
        var unsafeSites = new HashSet<string>(StringComparer.Ordinal);
        var orderedSites = new List<string>();
        var roots = 0;
        foreach (var row in rows.Skip(3)) {
            if (row.Length == 0) continue;
            var fields = row.Split('\t');
            if (fields[0] == "R") { if (fields.Length != 5 || ++roots != 1) return false; continue; }
            if (fields.Length != 2) return false;
            if (fields[0] == "S") {
                if (status != null || fields[1] is not ("proven" or "unproven" or "incomplete")) return false;
                status = fields[1];
            } else if (fields[0] is "G" or "E") {
                if (fields[1].Length == 0 || !(fields[0] == "G" ? gaps : unsafeSites).Add(fields[1])) return false;
                if (fields[0] == "E") orderedSites.Add(fields[1]);
            } else if (fields[0] is "A" or "C" or "U") {
                if (counts.ContainsKey(fields[0]) || !CompilerJobCatalogs.Number(fields[1], out var count)) return false;
                counts.Add(fields[0], count);
            } else return false;
        }
        if (!CompilerJobCatalogs.Number(rows[2], out var gapCount) || counts.Count != 3 || gaps.Count != Math.Min(gapCount, 12u) ||
            counts["U"] > counts["A"] || unsafeSites.Count != Math.Min(counts["U"], 12u)) return false;
        sites = orderedSites.ToArray();
        return status == (gapCount != 0 ? "incomplete" : counts["U"] != 0 ? "unproven" : "proven") && (gapCount != 0 || roots == 1);
    }
}
