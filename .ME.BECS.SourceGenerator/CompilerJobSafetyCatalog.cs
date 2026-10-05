using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Status = ME.BECS.SourceGenerator.CompilerJobCatalogs.Status;

namespace ME.BECS.SourceGenerator;

internal sealed class CompilerJobSafetyCatalog {
    private readonly CompilerJobCatalogs catalogs;
    internal CompilerJobSafetyCatalog(CompilerJobCatalogs catalogs) => this.catalogs = catalogs;

    internal Status Read(INamedTypeSymbol job, Compilation compilation,
        out (INamedTypeSymbol Type, string Mode)[] dependencies, out string error) {
        dependencies = Array.Empty<(INamedTypeSymbol, string)>();
        var status = this.catalogs.Read(job, JobSafetySummary.MetadataKey, out var data, out var originalProducer, out error);
        if (status != Status.Complete) return status;
        var parsed = this.ReadRows(job, originalProducer, data, compilation, out dependencies, out var typedError);
        error = parsed == Status.Complete ? "" : error + ": " + typedError;
        return parsed;
    }

    // Views use the same typed dependency schema, but bind callback slots rather
    // than Execute. The caller must validate its owner, root and complete header.
    internal Status ReadRows(INamedTypeSymbol job, IAssemblySymbol originalProducer, string[] data, Compilation compilation,
        out (INamedTypeSymbol Type, string Mode)[] dependencies, out string error) {
        dependencies = Array.Empty<(INamedTypeSymbol, string)>();
        error = "Invalid typed safety dependencies for " + JobSafetySummary.ReflectionIdentity(job);
        var result = new List<(INamedTypeSymbol Type, string Mode)>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var component = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
        var catalogSeen = false;
        var sizeSeen = false;
        foreach (var row in data.Skip(3)) {
            this.catalogs.Cancellation.ThrowIfCancellationRequested();
            var fields = row.Split('\t');
            if (fields.Length != 5) return Status.Invalid;
            if (fields[0] == "A") {
                var expected = "ME.BECS.SourceGenerated.JobSafetyTypes_" +
                    ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(fields[1] + "\n" + data[0] + "\n" + data[1]);
                var producer = this.catalogs.Producer(job, originalProducer, fields[1]);
                if (catalogSeen || producer == null || fields[2] != expected || fields[3] != "GetTypes" || fields[4] != "v1") return Status.Invalid;
                var owner = producer.GetTypeByMetadataName(expected);
                var getters = owner?.GetMembers("GetTypes").OfType<IMethodSymbol>().ToArray();
                if (owner == null || !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly) || getters == null || getters.Length != 1 ||
                    !getters[0].IsStatic || getters[0].DeclaredAccessibility != Accessibility.Public || getters[0].Arity != 0 || getters[0].Parameters.Length != 0 ||
                    getters[0].ReturnType is not IArrayTypeSymbol { Rank: 1 } array ||
                    !SymbolEqualityComparer.Default.Equals(array.ElementType, compilation.GetTypeByMetadataName("System.Type"))) return Status.Invalid;
                catalogSeen = true;
            } else if (fields[0] == "S") {
                if (sizeSeen || fields[3] != "Apply" || fields[4] != "v1") return Status.Invalid;
                sizeSeen = true;
            } else if (fields[0] == "D") {
                if (fields[3] is not ("0" or "1" or "2") || fields[4] is not ("0" or "1") || component == null) return Status.Invalid;
                var matches = DocumentationCommentId.GetSymbolsForDeclarationId(fields[2], compilation).OfType<INamedTypeSymbol>()
                    .Where(type => type.ContainingAssembly.Identity.ToString() == fields[1]).ToArray();
                if (matches.Length != 1) return Status.Invalid;
                var type = matches[0];
                if (!type.IsUnmanagedType || type.IsRefLikeType || MethodSummaryType.From(type).IsOpen || !seen.Add(type) ||
                    !type.AllInterfaces.Contains(component, SymbolEqualityComparer.Default) || !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) return Status.Invalid;
                result.Add((type, fields[3] == "0" ? "ReadOnly" : fields[3] == "1" ? "WriteOnly" : "ReadWrite"));
            } else return Status.Invalid;
        }
        if (!catalogSeen) return Status.Invalid;
        dependencies = result.OrderBy(entry => FullName(entry.Type), StringComparer.Ordinal)
            .ThenBy(entry => entry.Type.ContainingAssembly.Identity.ToString(), StringComparer.Ordinal).ToArray();
        error = "";
        return Status.Complete;
    }

    private static string FullName(INamedTypeSymbol type) => (type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") +
        string.Join("+", MethodSummaryType.TypeOwners(type).Select(owner => owner.MetadataName));
}
