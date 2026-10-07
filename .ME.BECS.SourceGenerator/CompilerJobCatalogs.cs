using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// One compiler snapshot shared by safety, counts and weights. No reflection,
// generated getter invocation, IL traversal or persistent Editor cache.
internal sealed class CompilerJobCatalogs {
    internal enum Status { Missing, Incomplete, Complete, Invalid }
    private readonly Dictionary<(string Kind, string Assembly, string Job), (IAssemblySymbol Producer, string[]? Rows)> rows = new();
    private readonly Dictionary<string, IAssemblySymbol> assemblies;
    internal System.Threading.CancellationToken Cancellation { get; }
    private readonly Dictionary<string, (Dictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> Methods,
        HashSet<(string Assembly, string Id)> Conflicts)> interfaceMaps = new(StringComparer.Ordinal);

    internal CompilerJobCatalogs(IEnumerable<IAssemblySymbol> assemblies, System.Threading.CancellationToken cancellation) {
        this.Cancellation = cancellation;
        this.assemblies = assemblies.ToDictionary(assembly => assembly.Identity.ToString(), StringComparer.Ordinal);
        foreach (var assembly in this.assemblies.Values.OrderBy(value => value.Identity.ToString(), StringComparer.Ordinal))
            foreach (var attribute in assembly.GetAttributes()) {
                cancellation.ThrowIfCancellationRequested();
                if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" || attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value is not string kind || attribute.ConstructorArguments[1].Value is not string text ||
                    (kind != JobSafetySummary.MetadataKey && kind != JobEntitySummary.MetadataKey && kind != JobWeightSummary.MetadataKey)) continue;
                var record = text.Split('\n');
                if (record[0].Length == 0) continue;
                var generic = record[0].Contains("[[");
                var key = (kind, generic ? "" : assembly.Identity.ToString(), record[0]);
                if (this.rows.TryGetValue(key, out var previous)) {
                    if (!generic || previous.Rows == null || !previous.Rows.SequenceEqual(record)) this.rows[key] = (assembly, null);
                } else this.rows.Add(key, (assembly, record));
            }
    }

    internal Status Read(INamedTypeSymbol job, string kind, out string[] data, out IAssemblySymbol producer, out string error) {
        data = Array.Empty<string>();
        producer = job.ContainingAssembly;
        var qualified = JobSafetySummary.ReflectionIdentity(job);
        var generic = MethodSummaryType.TypeOwners(job).Any(owner => owner.Arity != 0);
        var name = qualified == null ? null : generic ? qualified : qualified.Substring(0, qualified.IndexOf(", ", StringComparison.Ordinal));
        var label = kind == JobSafetySummary.MetadataKey ? "safety" : kind == JobEntitySummary.MetadataKey ? "entity-count" : "weight";
        error = "Missing source " + label + " catalog for " + job.ToDisplayString();
        if (name == null || !this.rows.TryGetValue((kind, generic ? "" : job.ContainingAssembly.Identity.ToString(), name), out var entry)) return Status.Missing;
        error = "Invalid or conflicting source " + label + " catalog for " + qualified;
        if (entry.Rows == null || entry.Rows.Length < 3) return Status.Invalid;
        data = entry.Rows;
        producer = entry.Producer;
        if (data[0] != name || !data[1].StartsWith("M:", StringComparison.Ordinal) || !Number(data[2], out var gapCount)) return Status.Invalid;
        var gaps = data.Skip(3).Where(row => row.StartsWith("G\t", StringComparison.Ordinal)).ToArray();
        if ((gapCount == 0) != (gaps.Length == 0) || gaps.Any(row => row.Length == 2)) return Status.Invalid;
        if (gapCount != 0) {
            error = "Incomplete source " + label + " for " + qualified + ": " + string.Join("; ", gaps.Take(4).Select(row => row.Substring(2)));
            return Status.Incomplete;
        }
        var rootId = data[1];
        var roots = job.OriginalDefinition.GetMembers().OfType<IMethodSymbol>().Where(method => method.GetDocumentationCommentId() == rootId).ToArray();
        if (roots.Length != 1 || roots[0].IsStatic || roots[0].Arity != 0 || !roots[0].ReturnsVoid ||
            !job.OriginalDefinition.AllInterfaces.Any(contract =>
                (contract.ContainingNamespace.ToDisplayString() is "ME.BECS.Jobs" or "Unity.Jobs") && contract.Name.StartsWith("IJob", StringComparison.Ordinal) &&
                contract.GetMembers("Execute").OfType<IMethodSymbol>().Any(method => this.IsExecuteRoot(job.OriginalDefinition, contract, method, roots[0])))) {
            error += ": unavailable or mismatched source Execute root " + data[1];
            return Status.Invalid;
        }
        return Status.Complete;
    }

    internal IAssemblySymbol? Producer(INamedTypeSymbol job, IAssemblySymbol ordinaryProducer, string identity) =>
        this.assemblies.TryGetValue(identity, out var producer) &&
        (MethodSummaryType.TypeOwners(job).Any(owner => owner.Arity != 0) || SymbolEqualityComparer.Default.Equals(producer, ordinaryProducer)) ? producer : null;

    internal static bool Number(string text, out uint number) => uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
        text == number.ToString(CultureInfo.InvariantCulture);

    private bool IsExecuteRoot(INamedTypeSymbol job, INamedTypeSymbol contract, IMethodSymbol member, IMethodSymbol root) {
        if (SymbolEqualityComparer.Default.Equals(job.FindImplementationForInterfaceMember(member), root)) return true;
        return this.SourceInterfaceBody(job, contract, member) == root.GetDocumentationCommentId();
    }

    internal string? SourceInterfaceBody(INamedTypeSymbol job, INamedTypeSymbol contract, IMethodSymbol member) {
        // Imported `in` modreq adapters can hide the original implementation.
        // Only an exact exported source interface map can prove that body.
        var identity = job.ContainingAssembly.Identity.ToString();
        if (!this.interfaceMaps.TryGetValue(identity, out var map)) {
            map = (new Dictionary<(string Assembly, string Id), MethodSummaryGraph.Summary>(), new HashSet<(string Assembly, string Id)>());
            foreach (var attribute in job.ContainingAssembly.GetAttributes()) {
                this.Cancellation.ThrowIfCancellationRequested();
                if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" || attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value as string != MethodSummaryGenerator.MetadataKey || attribute.ConstructorArguments[1].Value is not string text) continue;
                var fields = text.Split(new[] { '\n' }, 3);
                if (fields.Length < 3 || !fields[0].StartsWith("M:", StringComparison.Ordinal)) continue;
                var key = (identity, fields[0]);
                if (map.Methods.ContainsKey(key)) map.Conflicts.Add(key);
                else map.Methods.Add(key, new MethodSummaryGraph.Summary { Id = fields[0], Flags = fields[1].Split(',') });
            }
            this.interfaceMaps.Add(identity, map);
        }
        return MethodSummaryInterfaceMap.FindSourceBody(MethodSummaryType.From(job), MethodSummaryType.From(contract),
            member.ContainingAssembly.Identity.ToString(), MethodSummaryIdentity.Get(member) ?? "", map.Methods, map.Conflicts);
    }
}
