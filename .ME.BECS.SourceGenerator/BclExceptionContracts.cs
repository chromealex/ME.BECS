using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Audited Unity 6000.2 core-library constructors only initialize error fields.
// In particular, innerException/actualValue are stored, not formatted or invoked.
// This is NOT a contract for Message, ToString, serialization, throwing, or for
// constructors of derived/user exceptions. Call-site arguments remain effects.
internal static class BclExceptionContracts {
    internal static bool IsLeaf(IMethodSymbol method, Compilation compilation) {
        if (method.MethodKind != MethodKind.Constructor || method.IsStatic || method.Arity != 0 ||
            method.DeclaringSyntaxReferences.Length != 0 || method.DeclaredAccessibility != Accessibility.Public ||
            method.IsVararg || !method.ReturnsVoid || method.ReturnsByRef || method.ReturnsByRefReadonly) return false;
        var owner = method.ContainingType;
        if (owner.TypeKind != TypeKind.Class || owner.Arity != 0 ||
            !SymbolEqualityComparer.Default.Equals(owner.ContainingAssembly, compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly) ||
            !SymbolEqualityComparer.Default.Equals(owner, compilation.GetTypeByMetadataName("System." + owner.Name))) return false;
        foreach (var parameter in method.Parameters) if (parameter.RefKind != RefKind.None) return false;
        var count = method.Parameters.Length;
        bool Text(int index) => method.Parameters[index].Type.SpecialType == SpecialType.System_String;
        bool Inner(int index) => SymbolEqualityComparer.Default.Equals(method.Parameters[index].Type, compilation.GetTypeByMetadataName("System.Exception"));
        var common = count == 0 || count == 1 && Text(0) || count == 2 && Text(0) && Inner(1);
        return owner.Name switch {
            "Exception" or "SystemException" or "InvalidOperationException" or "IndexOutOfRangeException" or
            "NotSupportedException" or "NotImplementedException" or "NullReferenceException" or
            "ArithmeticException" or "OverflowException" => common,
            "ArgumentException" => common || count == 2 && Text(0) && Text(1) || count == 3 && Text(0) && Text(1) && Inner(2),
            "ArgumentNullException" => common || count == 2 && Text(0) && Text(1),
            "ArgumentOutOfRangeException" => common || count == 2 && Text(0) && Text(1) ||
                count == 3 && Text(0) && method.Parameters[1].Type.SpecialType == SpecialType.System_Object && Text(2),
            "ObjectDisposedException" => count > 0 && common || count == 2 && Text(0) && Text(1),
            _ => false,
        };
    }
}
