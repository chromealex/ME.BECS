using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class ComponentRegistrationFlags {
    private static bool IsDefaultCandidate(IPropertySymbol property) => property.IsStatic &&
        property.DeclaredAccessibility == Accessibility.Public;

    private static bool IsReadableDefault(IPropertySymbol property, INamedTypeSymbol owner) =>
        property.Parameters.Length == 0 && property.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
        SymbolEqualityComparer.Default.Equals(property.Type, owner);

    internal static bool HasInvalidDefault(INamedTypeSymbol type) => type.GetMembers("Default").OfType<IPropertySymbol>()
        .Any(property => IsDefaultCandidate(property) && !IsReadableDefault(property, type));

    internal static int Get(INamedTypeSymbol type, bool tag) {
        var flags = tag ? 1 : 0;
        if (type.AllInterfaces.Any(contract => contract.ToDisplayString() == "ME.BECS.IConfigComponentStatic")) flags |= 2;
        if (!tag && type.GetMembers("Default").OfType<IPropertySymbol>().Any(property =>
                IsDefaultCandidate(property) && IsReadableDefault(property, type))) flags |= 4;
        var shared = type.AllInterfaces.FirstOrDefault(contract => contract.ToDisplayString() == "ME.BECS.IComponentShared");
        if (shared != null) {
            flags |= 8;
            var hash = shared.GetMembers("GetHash").OfType<IMethodSymbol>().SingleOrDefault(method => method.Parameters.Length == 0);
            // Only the original throwing fallback is "no custom hash". A derived
            // interface can explicitly replace that slot with a real implementation;
            // a same-named new/overloaded method must not count as an override.
            if (hash != null && type.FindImplementationForInterfaceMember(hash) is IMethodSymbol implementation &&
                !SymbolEqualityComparer.Default.Equals(implementation.OriginalDefinition, hash.OriginalDefinition)) flags |= 16;
        }
        if (type.AllInterfaces.Any(contract => contract.ToDisplayString() == "ME.BECS.IConfigInitialize")) flags |= 32;
        return flags;
    }
}
