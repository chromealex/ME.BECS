using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ME.BECS.SourceGenerator;

// Compiler-owned union of direct accesses, presence filters and deferred job accesses.
// Complete() dominance is a separate contract: this catalog alone must not replace
// the lifecycle synchronization diagnostics.
internal sealed class SystemDependencySummary {
    internal const string MetadataKey = "ME.BECS.SystemDependencies.v1";
    private readonly Compilation compilation;
    private readonly InputManifestTypes resolver;
    private readonly IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods;
    private readonly ISet<(string Assembly, string Id)> conflicts;
    private readonly Func<string, MethodSummaryType?> decode;
    private readonly Dictionary<(string Job, bool Readonly), string[]> jobAccesses = new Dictionary<(string Job, bool Readonly), string[]>();

    internal SystemDependencySummary(Compilation compilation, System.Threading.CancellationToken cancellation,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode) {
        this.compilation = compilation;
        this.resolver = new InputManifestTypes(compilation, cancellation);
        this.methods = methods;
        this.conflicts = conflicts;
        this.decode = decode;
    }

    internal string Analyze(SourceProductionContext output, string directSummary, string modeSummary,
        IReadOnlyDictionary<string, (INamedTypeSymbol Job, string Mode)> scheduled, string? rootBinding) {
        var direct = directSummary.Split('\n');
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        var components = new SortedDictionary<string, (INamedTypeSymbol Type, int Bits)>(StringComparer.Ordinal);
        var systems = new SortedDictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var componentContract = this.compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
        var systemContract = this.compilation.GetTypeByMetadataName("ME.BECS.ISystem");

        void IncludeCoverage(string[] rows, string context) {
            if (rows.Length < 3 || rows[2] != "0") {
                gaps.Add(context + ": " + (rows.Length < 3 ? "missing header" : "gaps=" + rows[2]));
                foreach (var gap in rows.Skip(3).Where(row => row.StartsWith("G\t", StringComparison.Ordinal)))
                    gaps.Add(context + ": " + gap.Substring(2));
            }
        }
        void AddComponent(INamedTypeSymbol? type, int mode) {
            var identity = type == null ? null : JobSafetySummary.ReflectionIdentity(type);
            if (type == null || identity == null || !type.IsUnmanagedType || MethodSummaryType.From(type).IsOpen || componentContract == null ||
                !type.AllInterfaces.Contains(componentContract, SymbolEqualityComparer.Default) || mode < 0 || mode > 2) {
                gaps.Add("InvalidDependencyComponent: " + type); return;
            }
            var bits = mode == 2 ? 3 : mode + 1;
            components[identity] = (type, components.TryGetValue(identity, out var previous) ? previous.Bits | bits : bits);
        }
        void IncludeAccesses(string[] rows) {
            foreach (var row in rows.Skip(3)) {
                var fields = row.Split('\t');
                if (fields[0] == "D") {
                    if (fields.Length != 5 || !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var mode)) {
                        gaps.Add("MalformedDependencyAccess"); continue;
                    }
                    var matches = DocumentationCommentId.GetSymbolsForDeclarationId(fields[2], this.compilation).OfType<INamedTypeSymbol>()
                        .Where(type => type.ContainingAssembly.Identity.ToString() == fields[1]).ToArray();
                    AddComponent(matches.Length == 1 ? matches[0] : null, mode);
                } else if (fields[0] == "Q") {
                    if (fields.Length != 3) { gaps.Add("MalformedDependencyFilter"); continue; }
                    AddComponent(this.resolver.ResolveDefinition(fields[2], out _), 0);
                } else if (fields[0] == "Y") {
                    var type = fields.Length == 2 ? this.resolver.ResolveDefinition(fields[1], out _) : null;
                    if (type == null || !type.IsUnmanagedType || MethodSummaryType.From(type).IsOpen || systemContract == null ||
                        !type.AllInterfaces.Contains(systemContract, SymbolEqualityComparer.Default) || JobSafetySummary.ReflectionIdentity(type) != fields[1])
                        gaps.Add("InvalidSystemDependency");
                    else systems[fields[1]] = type;
                }
            }
        }

        IncludeCoverage(direct, "DirectAccessIncomplete");
        IncludeCoverage(modeSummary.Split('\n'), "ScheduleModesIncomplete");
        IncludeAccesses(direct);
        foreach (var entry in scheduled.OrderBy(item => item.Key, StringComparer.Ordinal)) {
            output.CancellationToken.ThrowIfCancellationRequested();
            // Mixed schedules need BOTH access sets: a WO argument in the ordinary
            // schedule becomes RO in the readonly schedule, producing RW after union.
            foreach (var readOnly in entry.Value.Mode == "1" ? new[] { true } :
                         entry.Value.Mode == "0" ? new[] { false } : new[] { false, true }) {
                var access = this.JobAccess(output, entry.Value.Job, entry.Key, readOnly);
                IncludeCoverage(access, "JobSafetyIncomplete: " + entry.Key + (readOnly ? " [readonly arguments]" : ""));
                IncludeAccesses(access);
            }
            var filters = this.ImplicitFilters(entry.Value.Job, gaps);
            foreach (var filter in filters) AddComponent(this.resolver.ResolveDefinition(filter.Split('\t')[2], out _), 0);
        }
        if (rootBinding == null) gaps.Add("MissingDependencyRootBinding");
        foreach (var type in components.Values.Select(entry => entry.Type).Concat(systems.Values))
            if (!this.compilation.IsSymbolAccessibleWithin(type, this.compilation.Assembly)) gaps.Add("InaccessibleDependencyType: " + type);
        var rowsOut = new List<string> { direct[0], direct[1], gaps.Count.ToString(CultureInfo.InvariantCulture) };
        foreach (var entry in components) rowsOut.Add("C\t" + (entry.Value.Bits == 3 ? 2 : entry.Value.Bits - 1).ToString(CultureInfo.InvariantCulture) + "\t" + entry.Key);
        foreach (var entry in systems) rowsOut.Add("Y\t" + entry.Key);
        if (rootBinding != null) rowsOut.Add(rootBinding);
        foreach (var gap in JobSummaryDiagnostics.Describe(gaps)) rowsOut.Add("G\t" + gap);
        if (gaps.Count == 0) {
            var name = "SystemDependencyPlan_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(this.compilation.Assembly.Identity + "\n" + direct[0] + "\n" + direct[1]);
            var source = new StringBuilder("// <auto-generated/>\nnamespace ME.BECS.SourceGenerated { public static class ").Append(name).Append(" {\n");
            void Types(string method, IEnumerable<INamedTypeSymbol> types) => source.Append("public static global::System.Type[] ").Append(method)
                .Append("() => new global::System.Type[] {")
                .Append(string.Join(", ", types.Select(type => "typeof(" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")"))).Append("};\n");
            Types("GetComponents", components.Values.Select(entry => entry.Type));
            Types("GetSystems", systems.Values);
            source.Append("public static byte[] GetModes() => new byte[] {")
                .Append(string.Join(", ", components.Values.Select(entry => (entry.Bits == 3 ? 2 : entry.Bits - 1).ToString(CultureInfo.InvariantCulture))))
                .Append("};\n} }\n");
            output.AddSource("ME.BECS." + name + ".g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
            rowsOut.Add("P\t" + this.compilation.Assembly.Identity + "\tME.BECS.SourceGenerated." + name + "\tv1");
        }
        return string.Join("\n", rowsOut);
    }

    private string[] JobAccess(SourceProductionContext output, INamedTypeSymbol job, string identity, bool readOnly) {
        if (this.jobAccesses.TryGetValue((identity, readOnly), out var cached)) return cached;
        var targets = new HashSet<(string Assembly, string Id)>();
        var unresolved = false;
        foreach (var contract in job.AllInterfaces.Where(contract => contract.Name.StartsWith("IJob", StringComparison.Ordinal) &&
                     (contract.ContainingNamespace.ToDisplayString() == "ME.BECS.Jobs" || contract.ContainingNamespace.ToDisplayString() == "Unity.Jobs"))) {
            foreach (var member in contract.GetMembers("Execute").OfType<IMethodSymbol>()) {
                var implementation = MethodSummaryInterfaceMap.Implementation(job, member);
                var id = implementation == null ? null : MethodSummaryIdentity.Get(implementation);
                // Metadata may expose a compiler-created modreq forwarding method,
                // or omit a private explicit implementation entirely. Bind its
                // exact source interface map, never a same-named Execute overload.
                if (id == null || !this.methods.ContainsKey((job.ContainingAssembly.Identity.ToString(), id)))
                    id = MethodSummaryIdentity.Get(member) is string memberId ? MethodSummaryInterfaceMap.FindSourceBody(
                        MethodSummaryType.From(job), MethodSummaryType.From(contract), member.ContainingAssembly.Identity.ToString(),
                        memberId, this.methods, this.conflicts) : null;
                if (member.Arity != 0 || implementation?.IsAbstract == true || implementation?.Arity > 0 ||
                    implementation != null && !SymbolEqualityComparer.Default.Equals(implementation.ContainingType, job) || id == null) {
                    unresolved = true; continue;
                }
                targets.Add((job.ContainingAssembly.Identity.ToString(), id));
            }
        }
        var target = targets.Count == 1 ? targets.Single() : default;
        string[] result;
        if (unresolved || targets.Count != 1 || !this.methods.TryGetValue(target, out var original) || !original.Flags.Contains("job-root")) {
            result = new[] { identity, "M:unavailable", "1", "G\tMissingOrAmbiguousJobExecute" };
        } else {
            var root = new MethodSummaryGraph.Summary {
                Id = target.Id, Environment = original.Environment, Unresolved = original.Unresolved,
                RootAssembly = target.Assembly,
                RootArguments = MethodSummaryType.TypeOwners(job).SelectMany(owner => owner.TypeArguments).Select(MethodSummaryType.From).ToArray(),
                Flags = original.Flags.Select(flag => flag.StartsWith("job-type=", StringComparison.Ordinal) ? "job-type=" + identity : flag).ToArray(),
            };
            root.Operations.AddRange(original.Operations);
            result = JobSafetySummary.Analyze(output, this.compilation, root.RootAssembly, root, this.methods, this.conflicts, this.decode,
                emitInitializer: false, emitSizeInitializer: false, readonlyArguments: readOnly).Split('\n');
        }
        this.jobAccesses.Add((identity, readOnly), result);
        return result;
    }

    private IEnumerable<string> ImplicitFilters(INamedTypeSymbol job, ISet<string> gaps) {
        var filters = new SortedSet<string>(StringComparer.Ordinal);
        // The generic Execute interfaces live in ME.BECS.Jobs, but their marker
        // bases live in ME.BECS (Jobs.cs). A missing marker is incomplete coverage,
        // not evidence that scheduling adds no presence filters.
        var engine = this.compilation.GetTypeByMetadataName("ME.BECS.Ent")?.ContainingAssembly;
        var markers = new List<INamedTypeSymbol>();
        foreach (var name in new[] { "ME.BECS.IJobForComponentsBase", "ME.BECS.IJobForAspectsBase", "ME.BECS.IJobForAspectsComponentsBase",
                     "ME.BECS.IJobParallelForComponentsBase", "ME.BECS.IJobParallelForAspectsBase", "ME.BECS.IJobParallelForAspectsComponentsBase" }) {
            var marker = this.compilation.GetTypeByMetadataName(name);
            if (marker == null || !SymbolEqualityComparer.Default.Equals(marker.ContainingAssembly, engine))
                gaps.Add("MissingScheduleFilterContract: " + name);
            else markers.Add(marker);
        }
        foreach (var contract in job.AllInterfaces.Where(contract =>
                     SymbolEqualityComparer.Default.Equals(contract.ContainingAssembly, engine) &&
                     markers.Any(marker => contract.AllInterfaces.Contains(marker, SymbolEqualityComparer.Default)))) {
            foreach (var argument in contract.TypeArguments) {
                var component = MethodSummaryContracts.IsComponentType(argument, this.compilation);
                var operation = new[] { "call", "0", job.ContainingAssembly.Identity.ToString(), "ImplicitScheduleFilter: " + job,
                    MethodSummaryType.From(job).Encode(), "!query-filter=" + (component ? "with" : "aspect"), "!query-count=1",
                    "!query-type-0=" + MethodSummaryType.From(argument).Encode() };
                SystemQueryFilterContracts.Collect(operation, this.compilation, new Dictionary<string, MethodSummaryType>(), this.decode, filters, gaps);
            }
        }
        return filters;
    }
}
