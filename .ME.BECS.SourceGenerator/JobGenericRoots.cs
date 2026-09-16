using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

// Analysis only. Scheduling/parallel mode and the legacy selection remain unchanged.
// Each component's assembly owns its specializations, including jobs from referenced asmdefs.
internal static class JobGenericRoots {
    internal static IEnumerable<MethodSummaryGraph.Summary> Create(SourceProductionContext output, Compilation compilation,
        Dictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods) {
        var componentBase = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
        if (componentBase == null) yield break;
        var candidates = Types(compilation.Assembly.GlobalNamespace)
            .Where(t => t.TypeKind == TypeKind.Struct && t.IsUnmanagedType &&
                MethodSummaryType.TypeOwners(t).All(p => p.Arity == 0 && p.DeclaredAccessibility == Accessibility.Public) &&
                t.AllInterfaces.Contains(componentBase, SymbolEqualityComparer.Default))
            .OrderBy(t => t.ToDisplayString(), StringComparer.Ordinal).ToArray();
        foreach (var entry in methods.OrderBy(e => e.Key.Assembly, StringComparer.Ordinal).ThenBy(e => e.Key.Id, StringComparer.Ordinal)) {
            output.CancellationToken.ThrowIfCancellationRequested();
            var summary = entry.Value;
            var systemRoot = summary.Flags.Contains("system-root");
            if ((!summary.Flags.Contains("job-root") && !systemRoot) || summary.Environment.Length != 1) continue;
            var method = DocumentationCommentId.GetSymbolsForDeclarationId(summary.Id, compilation).OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.ContainingAssembly.Identity.ToString() == entry.Key.Assembly);
            if (method == null || method.Arity != 0) continue;
            var owners = MethodSummaryType.TypeOwners(method.ContainingType).ToArray();
            if (owners.Any(t => t.DeclaredAccessibility != Accessibility.Public)) continue;
            var parameters = owners.SelectMany(t => t.TypeParameters).ToArray();
            if (parameters.Length != 1 || !parameters[0].ConstraintTypes.Any(t => t.TypeKind == TypeKind.Interface)) continue;
            foreach (var candidate in candidates) {
                output.CancellationToken.ThrowIfCancellationRequested();
                if (!Satisfies(compilation, parameters[0], candidate)) continue;
                if (systemRoot && IsExcluded(compilation, method.ContainingType, parameters[0], candidate)) continue;
                var typePrefix = systemRoot ? "system-type=" : "job-type=";
                var name = summary.Flags.FirstOrDefault(f => f.StartsWith(typePrefix, StringComparison.Ordinal))?.Substring(typePrefix.Length);
                if (name == null) continue;
                // Reflection FullName's generic argument syntax includes the argument assembly.
                var argumentName = string.Join("+", MethodSummaryType.TypeOwners(candidate).Select(t => t.MetadataName));
                if (!candidate.ContainingNamespace.IsGlobalNamespace) argumentName = candidate.ContainingNamespace.ToDisplayString() + "." + argumentName;
                var closedName = name + "[[" + argumentName + ", " + candidate.ContainingAssembly.Identity + "]], " + entry.Key.Assembly;
                var root = new MethodSummaryGraph.Summary {
                    Id = summary.Id, Environment = summary.Environment, Unresolved = summary.Unresolved,
                    RootAssembly = entry.Key.Assembly, RootArguments = new[] { MethodSummaryType.From(candidate) },
                    Flags = summary.Flags.Select(f => f.StartsWith(typePrefix, StringComparison.Ordinal) ? typePrefix + closedName : f).ToArray(),
                };
                root.Operations.AddRange(summary.Operations);
                yield return root;
            }
        }
    }

    private static bool IsExcluded(Compilation compilation, INamedTypeSymbol system, ITypeParameterSymbol parameter, INamedTypeSymbol candidate) {
        var marker = compilation.GetTypeByMetadataName("ME.BECS.IGenericWithout");
        if (marker == null) return false;
        var bindings = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal) {
            [MethodSummaryType.From(parameter).Identity] = MethodSummaryType.From(candidate),
        };
        foreach (var contract in system.AllInterfaces) {
            if (!contract.AllInterfaces.Contains(marker, SymbolEqualityComparer.Default)) continue;
            var argument = MethodSummaryType.TypeOwners(contract).SelectMany(t => t.TypeArguments).FirstOrDefault();
            if (argument == null) continue;
            var excluded = MethodSummaryTypeResolver.Resolve(MethodSummaryType.From(argument).Substitute(bindings), compilation);
            if (excluded == null || ((CSharpCompilation)compilation).ClassifyConversion(candidate, excluded).IsImplicit) return true;
        }
        return false;
    }

    private static bool Satisfies(Compilation compilation, ITypeParameterSymbol parameter, INamedTypeSymbol candidate) {
        if (parameter.HasReferenceTypeConstraint) return false;
        var bindings = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal) {
            [MethodSummaryType.From(parameter).Identity] = MethodSummaryType.From(candidate),
        };
        foreach (var constraint in parameter.ConstraintTypes) {
            var closed = MethodSummaryTypeResolver.Resolve(MethodSummaryType.From(constraint).Substitute(bindings), compilation);
            if (closed == null || !((CSharpCompilation)compilation).ClassifyConversion(candidate, closed).IsImplicit) return false;
        }
        return true;
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceOrTypeSymbol scope) {
        foreach (var member in scope.GetMembers()) {
            if (member is INamedTypeSymbol type) {
                yield return type;
                foreach (var nested in Types(type)) yield return nested;
            } else if (member is INamespaceSymbol ns) {
                foreach (var nested in Types(ns)) yield return nested;
            }
        }
    }
}
