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

    internal static int Get(INamedTypeSymbol type) {
        var explicitSize = type.GetAttributes()
            .Where(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.StructLayoutAttribute")
            .SelectMany(attribute => attribute.NamedArguments)
            .Where(argument => argument.Key == "Size" && argument.Value.Value is int)
            .Select(argument => (int)argument.Value.Value!).DefaultIfEmpty(0).Max();
        var tag = !type.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic) && explicitSize <= 1;
        var flags = tag ? 1 : 0;
        if (type.AllInterfaces.Any(contract => contract.ToDisplayString() == "ME.BECS.IConfigComponentStatic")) flags |= 2;
        if (!tag && type.GetMembers("Default").OfType<IPropertySymbol>().Any(property =>
                IsDefaultCandidate(property) && IsReadableDefault(property, type))) flags |= 4;
        var shared = type.AllInterfaces.FirstOrDefault(contract => contract.ToDisplayString() == "ME.BECS.IComponentShared");
        if (shared != null) {
            flags |= 8;
            var hash = shared.GetMembers("GetHash").OfType<IMethodSymbol>().SingleOrDefault(method => method.Parameters.Length == 0);
            // A default interface implementation is not a component's custom hash.
            if (hash != null && type.FindImplementationForInterfaceMember(hash) is IMethodSymbol implementation &&
                implementation.ContainingType.TypeKind != TypeKind.Interface) flags |= 16;
        }
        if (type.AllInterfaces.Any(contract => contract.ToDisplayString() == "ME.BECS.IConfigInitialize")) flags |= 32;
        return flags;
    }
}
