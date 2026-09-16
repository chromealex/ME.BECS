using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class MethodSummaryTypeResolver {
    internal static ITypeSymbol? Resolve(MethodSummaryType expression, Compilation compilation) {
        var budget = 4096;
        return Resolve(expression, compilation, ref budget, 0);
    }

    private static ITypeSymbol? Resolve(MethodSummaryType expression, Compilation compilation, ref int budget, int depth) {
        if (--budget < 0 || depth > 64 || expression.IsOpen || expression.IsUnsupported) return null;
        if (expression.Kind == 'a' && expression.Arguments.Length == 1 &&
            int.TryParse(expression.Identity, NumberStyles.None, CultureInfo.InvariantCulture, out var rank) && rank > 0 && rank <= 32) {
            var element = Resolve(expression.Arguments[0], compilation, ref budget, depth + 1);
            return element == null ? null : compilation.CreateArrayTypeSymbol(element, rank);
        }
        if (expression.Kind == '*' && expression.Arguments.Length == 1) {
            var element = Resolve(expression.Arguments[0], compilation, ref budget, depth + 1);
            return element == null ? null : compilation.CreatePointerTypeSymbol(element);
        }
        var identity = expression.Identity.Split('\n');
        if (expression.Kind != 'n' || identity.Length != 2) return null;
        var definitions = DocumentationCommentId.GetSymbolsForDeclarationId(identity[1], compilation).OfType<INamedTypeSymbol>()
            .Where(t => t.ContainingAssembly.Identity.ToString() == identity[0]).ToArray();
        if (definitions.Length != 1) return null;
        var owners = MethodSummaryType.TypeOwners(definitions[0]).ToArray();
        if (owners.Sum(t => t.Arity) != expression.Arguments.Length) return null;
        INamedTypeSymbol? constructed = null;
        var offset = 0;
        foreach (var owner in owners) {
            // Select nested members from the already constructed parent. Constructing the
            // leaf's OriginalDefinition would discard outer generic arguments.
            var candidates = constructed == null ? new[] { owner } : constructed.GetTypeMembers(owner.Name, owner.Arity)
                .Where(t => SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, owner.OriginalDefinition)).ToArray();
            if (candidates.Length != 1) return null;
            constructed = candidates[0];
            if (owner.Arity == 0) continue;
            var arguments = new ITypeSymbol[owner.Arity];
            for (var i = 0; i < arguments.Length; ++i) {
                var argument = Resolve(expression.Arguments[offset++], compilation, ref budget, depth + 1);
                if (argument == null) return null;
                arguments[i] = argument;
            }
            try { constructed = constructed.Construct(arguments); }
            catch (ArgumentException) { return null; }
        }
        // Verify the complete round trip, including nested owner substitutions.
        return constructed != null && MethodSummaryType.From(constructed).Encode() == expression.Encode() ? constructed : null;
    }
}
