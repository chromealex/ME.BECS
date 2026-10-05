using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Presence filters are dependencies, not direct reads/writes of component data.
internal static class SystemQueryFilterContracts {
    internal const string Schema = "query-filter-schema=1";
    internal const string AspectMetadataKey = "ME.BECS.AspectQuery.v1";

    internal static void Append(StringBuilder rows, IMethodSymbol method, Compilation compilation) {
        if (method.IsStatic || method.Parameters.Length != 0 || method.Arity == 0) return;
        var compose = compilation.GetTypeByMetadataName("ME.BECS.ArchetypeQueries+QueryCompose");
        var dynamicBuilder = compilation.GetTypeByMetadataName("ME.BECS.QueryBuilder");
        var staticBuilder = compilation.GetTypeByMetadataName("ME.BECS.QueryBuilderStatic");
        var isCompose = SymbolEqualityComparer.Default.Equals(method.ContainingType, compose);
        var isDynamic = SymbolEqualityComparer.Default.Equals(method.ContainingType, dynamicBuilder);
        var isStatic = SymbolEqualityComparer.Default.Equals(method.ContainingType, staticBuilder);
        if (!isCompose && !isDynamic && !isStatic) return;
        if (isCompose ? !method.ReturnsVoid : !SymbolEqualityComparer.Default.Equals(method.ReturnType, method.ContainingType)) return;
        string? kind = method.Name switch {
            "With" when method.Arity == 1 => "with",
            "Without" when method.Arity == 1 => "without",
            "WithAspect" when method.Arity == 1 => "aspect",
            "WithAll" when !isCompose && method.Arity >= 2 && method.Arity <= (isDynamic ? 4 : 2) => "with",
            "WithAny" when method.Arity >= 2 && method.Arity <= (isDynamic ? 4 : 2) => "any",
            _ => null,
        };
        if (kind == null) return;
        rows.Append("\t!query-filter=").Append(kind).Append("\t!query-count=").Append(method.Arity);
        for (var index = 0; index < method.Arity; ++index)
            rows.Append("\t!query-type-").Append(index).Append('=').Append(MethodSummaryType.From(method.TypeArguments[index]).Encode());
    }

    internal static string? DescribeAspect(INamedTypeSymbol aspect, Compilation compilation) {
        var componentContract = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
        var dataContract = compilation.GetTypeByMetadataName("ME.BECS.IAspectData");
        var queryAttribute = compilation.GetTypeByMetadataName("ME.BECS.QueryWithAttribute");
        if (componentContract == null || dataContract == null || queryAttribute == null) return null;
        var components = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var field in aspect.GetMembers().OfType<IFieldSymbol>().Where(static field => !field.IsStatic)) {
            if (!field.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, queryAttribute))) continue;
            if (field.Type is not INamedTypeSymbol data || !data.AllInterfaces.Contains(dataContract, SymbolEqualityComparer.Default)) return null;
            var arguments = MethodSummaryType.TypeOwners(data).SelectMany(owner => owner.TypeArguments).ToArray();
            if (arguments.Length == 0 || arguments[0] is not INamedTypeSymbol component || !component.IsUnmanagedType ||
                !component.AllInterfaces.Contains(componentContract, SymbolEqualityComparer.Default) || MethodSummaryType.From(component).IsOpen) return null;
            components.Add(MethodSummaryType.From(component).Encode());
        }
        return "v1\n" + MethodSummaryType.From(aspect).Encode() + "\n" + string.Join("\n", components);
    }

    internal static bool Collect(string[] operation, Compilation compilation, IReadOnlyDictionary<string, MethodSummaryType> environment,
        Func<string, MethodSummaryType?> decode, ISet<string> filters, ISet<string> gaps) {
        var kind = MethodSummaryContracts.Value(operation, "query-filter");
        if (kind == null) return false;
        var countText = MethodSummaryContracts.Value(operation, "query-count");
        if (!(kind is "with" or "without" or "any" or "aspect") ||
            !int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1 || count > 4 ||
            (kind == "aspect" && count != 1)) {
            gaps.Add("MalformedQueryFilter: " + operation[3]); return true;
        }
        for (var index = 0; index < count; ++index) {
            var token = MethodSummaryContracts.Value(operation, "query-type-" + index.ToString(CultureInfo.InvariantCulture));
            var expression = token == null ? null : decode(token)?.Substitute(environment);
            var type = expression == null || expression.IsOpen || expression.IsUnsupported ? null :
                MethodSummaryTypeResolver.Resolve(expression, compilation) as INamedTypeSymbol;
            if (type == null || !type.IsUnmanagedType) { gaps.Add("UnresolvedQueryFilter: " + operation[3]); continue; }
            if (kind != "aspect") { AddComponent(type, kind, compilation, filters, gaps); continue; }
            var aspectContract = compilation.GetTypeByMetadataName("ME.BECS.IAspect");
            if (aspectContract == null || !type.AllInterfaces.Contains(aspectContract, SymbolEqualityComparer.Default)) {
                gaps.Add("InvalidQueryAspect: " + type); continue;
            }
            var header = "v1\n" + MethodSummaryType.From(type).Encode() + "\n";
            string? payload;
            if (type.DeclaringSyntaxReferences.Length > 0) payload = DescribeAspect(type, compilation);
            else {
                // Imported private fields may not exist in Roslyn's symbol view. Never infer an empty filter from that.
                var candidates = type.ContainingAssembly.GetAttributes().Where(attribute =>
                        attribute.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyMetadataAttribute" &&
                        attribute.ConstructorArguments.Length == 2 && attribute.ConstructorArguments[0].Value as string == AspectMetadataKey)
                    .Select(attribute => attribute.ConstructorArguments[1].Value as string)
                    .Where(value => value != null && value.StartsWith(header, StringComparison.Ordinal)).ToArray();
                payload = candidates.Length == 1 ? candidates[0] : null;
            }
            if (payload == null || !payload.StartsWith(header, StringComparison.Ordinal)) {
                gaps.Add("MissingAspectQueryCatalog: " + type); continue;
            }
            var entries = payload.Substring(header.Length);
            if (entries.Length == 0) continue; // An explicitly empty catalog is valid.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries.Split('\n')) {
                var componentExpression = decode(entry);
                var component = componentExpression == null || componentExpression.IsOpen || componentExpression.IsUnsupported ? null :
                    MethodSummaryTypeResolver.Resolve(componentExpression, compilation) as INamedTypeSymbol;
                if (!seen.Add(entry) || component == null) { gaps.Add("InvalidAspectQueryCatalog: " + type); continue; }
                AddComponent(component, kind, compilation, filters, gaps);
            }
        }
        return true;
    }

    private static void AddComponent(INamedTypeSymbol component, string kind, Compilation compilation, ISet<string> filters, ISet<string> gaps) {
        if (kind == "any" && SymbolEqualityComparer.Default.Equals(component, compilation.GetTypeByMetadataName("ME.BECS.TNull"))) return;
        var contract = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
        var identity = JobSafetySummary.ReflectionIdentity(component);
        if (!component.IsUnmanagedType || contract == null || !component.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) || identity == null)
            gaps.Add("InvalidQueryComponent: " + component);
        else filters.Add("Q\t" + kind + "\t" + identity);
    }
}
