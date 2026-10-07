using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ME.BECS.SourceGenerator;

internal static class MethodSummaryIdentity {
    // Local/anonymous methods have no portable CLR/documentation ID. Use their containing
    // method plus lexical ordinal, independent of absolute paths, whitespace and line numbers.
    internal static string? Get(ISymbol symbol) {
        symbol = symbol.OriginalDefinition;
        if (symbol is not IMethodSymbol method || method.MethodKind is not (MethodKind.LocalFunction or MethodKind.AnonymousFunction))
            return symbol.GetDocumentationCommentId();
        var owner = method.ContainingSymbol as IMethodSymbol;
        while (owner != null && owner.MethodKind is MethodKind.LocalFunction or MethodKind.AnonymousFunction)
            owner = owner.ContainingSymbol as IMethodSymbol;
        var ownerId = owner?.GetDocumentationCommentId();
        var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (ownerId == null || syntax == null) return null;
        var root = syntax.Ancestors().FirstOrDefault(static n => n is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or
            PropertyDeclarationSyntax or IndexerDeclarationSyntax);
        if (root == null) return null;
        var ordinal = 0;
        foreach (var nested in root.DescendantNodes().Where(static n => n is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)) {
            if (nested.Span == syntax.Span && nested.RawKind == syntax.RawKind)
                return ownerId + "~nested:" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ++ordinal;
        }
        return null;
    }
}
