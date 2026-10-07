using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Bind engine lifecycle interfaces, including private explicit implementations
// hidden or wrapped by Unity's imported in/modreq metadata. Never run GetRoot.
internal static class CompilerSystemRoots {
    internal static (string Id, string Name, bool Selected)[]? Read(INamedTypeSymbol owner, InputManifestTypes resolver, Compilation compilation) {
        var system = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
        if (system == null) return null;
        var roots = new List<(string Id, string Name, bool Selected)>();
        foreach (var phase in CompilerSystemDependencies.Phases.Concat(new[] { "DrawGizmos" })) {
            var contract = system.ContainingAssembly.GetTypeByMetadataName("ME.BECS.I" + phase);
            if (contract == null) return null;
            if (!owner.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)) continue;
            var members = contract.GetMembers("On" + phase).OfType<IMethodSymbol>().ToArray();
            if (members.Length != 1) return null;
            var member = members[0];
            var sourceId = resolver.JobCatalogs.SourceInterfaceBody(owner.OriginalDefinition, contract, member);
            var root = owner.FindImplementationForInterfaceMember(member) as IMethodSymbol;
            if (sourceId == null && (root == null || root.IsStatic || root.Arity != 0 || !root.ReturnsVoid || root.Parameters.Length != 1 ||
                root.Parameters[0].RefKind != RefKind.Ref || !SymbolEqualityComparer.Default.Equals(root.Parameters[0].Type,
                    compilation.GetTypeByMetadataName("ME.BECS.SystemContext")))) return null;
            var id = sourceId ?? root!.OriginalDefinition.GetDocumentationCommentId();
            var prefix = "M:" + owner.OriginalDefinition.GetDocumentationCommentId()!.Substring(2) + ".";
            const string suffix = "(ME.BECS.SystemContext@)";
            if (id == null || !id.StartsWith(prefix, StringComparison.Ordinal) || !id.EndsWith(suffix, StringComparison.Ordinal)) return null;
            roots.Add((id, id.Substring(prefix.Length, id.Length - prefix.Length - suffix.Length).Replace('#', '.'), phase != "DrawGizmos"));
        }
        return roots.ToArray();
    }

    internal static bool Getter(INamedTypeSymbol? owner, string name, ITypeSymbol? result, bool array, Compilation compilation) {
        if (owner == null || result == null || !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly)) return false;
        var methods = owner.GetMembers(name).OfType<IMethodSymbol>().ToArray();
        if (methods.Length != 1 || !methods[0].IsStatic || methods[0].Arity != 0 || methods[0].Parameters.Length != 0 ||
            methods[0].DeclaredAccessibility != Accessibility.Public) return false;
        var returned = methods[0].ReturnType;
        if (array) {
            if (returned is not IArrayTypeSymbol { Rank: 1 } element) return false;
            returned = element.ElementType;
        }
        return SymbolEqualityComparer.Default.Equals(returned, result);
    }
}
