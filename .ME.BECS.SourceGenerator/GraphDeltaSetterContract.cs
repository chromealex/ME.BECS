using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Shared by graph validation and emission; independent of the retired aggregate manifest.
internal static class GraphDeltaSetterContract {
    internal static string Name(IFieldSymbol field) =>
        "__BecsInjectDelta_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(field.Name);

    internal static bool IsAvailable(INamedTypeSymbol owner, IFieldSymbol field, Compilation compilation) {
        var marker = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        return owner.GetMembers(Name(field)).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && method.Arity == 0 && method.ReturnsVoid && method.Parameters.Length == 2 &&
            method.DeclaredAccessibility == Accessibility.Public && compilation.IsSymbolAccessibleWithin(method, compilation.Assembly) &&
            method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, owner) &&
            method.Parameters[1].RefKind == RefKind.None && method.Parameters[1].Type.SpecialType == SpecialType.System_UInt16 &&
            method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker)));
    }
}
