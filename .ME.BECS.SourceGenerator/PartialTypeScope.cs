using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ME.BECS.SourceGenerator;

internal static class PartialTypeScope {
    internal static (string Prefix, string Suffix)? Create(INamedTypeSymbol type) {
        var owners = new System.Collections.Generic.Stack<INamedTypeSymbol>();
        for (var owner = type; owner != null; owner = owner.ContainingType) {
            if ((owner.TypeKind != TypeKind.Struct && owner.TypeKind != TypeKind.Class) || owner.IsRefLikeType ||
                owner.DeclaredAccessibility != Accessibility.Public || owner.DeclaringSyntaxReferences.Length == 0 ||
                owner.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is not TypeDeclarationSyntax declaration ||
                    declaration is RecordDeclarationSyntax || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))) return null;
            owners.Push(owner);
        }
        var prefix = new StringBuilder();
        var closings = 0;
        if (!type.ContainingNamespace.IsGlobalNamespace) {
            prefix.Append("namespace ").Append(type.ContainingNamespace.ToDisplayString()).Append(" {\n");
            ++closings;
        }
        foreach (var owner in owners) {
            prefix.Append("public ");
            if (owner.IsStatic) prefix.Append("static ");
            if (owner.IsReadOnly) prefix.Append("readonly ");
            prefix.Append(owner.TypeKind == TypeKind.Struct ? "partial struct @" : "partial class @").Append(owner.Name);
            if (owner.Arity != 0) prefix.Append('<').Append(string.Join(", ", owner.TypeParameters.Select(static p => "@" + p.Name))).Append('>');
            // Constraints remain on the original partial declaration, with its using/alias scope.
            prefix.Append(" {\n");
            ++closings;
        }
        return (prefix.ToString(), new string('}', closings) + "\n");
    }
}
