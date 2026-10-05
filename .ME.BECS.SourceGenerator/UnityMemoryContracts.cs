using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Audited Unity 6000.2.14f1 intrinsics and their trivial forwarding wrappers.
// These bodies do not dispatch user code. This is NOT a blanket UnsafeUtility
// exemption, nor proof that arbitrary pointers are valid or jobs are complete.
internal static class UnityMemoryContracts {
    internal enum Kind { None, Layout, Address, Read, Write, Copy }

    internal static Kind Classify(IMethodSymbol method, Compilation compilation) {
        if (method.DeclaringSyntaxReferences.Length != 0 || !method.IsStatic || method.IsAbstract || method.IsVirtual || method.IsVararg ||
            method.DeclaredAccessibility != Accessibility.Public || method.MethodKind != MethodKind.Ordinary ||
            !method.ContainingType.IsStatic || method.ContainingType.Arity != 0 ||
            method.ContainingAssembly.Name != "UnityEngine.CoreModule" || method.ReturnsByRefReadonly ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType,
                compilation.GetTypeByMetadataName("Unity.Collections.LowLevel.Unsafe.UnsafeUtility"))) return Kind.None;
        var definition = method.OriginalDefinition;
        var parameters = definition.Parameters;
        bool Type(ITypeSymbol a, ITypeSymbol b) => SymbolEqualityComparer.Default.Equals(a, b);
        bool Pointer(ITypeSymbol type) => type is IPointerTypeSymbol pointer && pointer.PointedAtType.SpecialType == SpecialType.System_Void;
        bool Value(int index, SpecialType type) => parameters[index].RefKind == RefKind.None && parameters[index].Type.SpecialType == type;
        bool Ptr(int index) => parameters[index].RefKind == RefKind.None && Pointer(parameters[index].Type);
        bool Int(int index) => Value(index, SpecialType.System_Int32);
        if (definition.Arity == 0) {
            if (definition.ReturnsByRef) return Kind.None;
            if (parameters.Length == 3 && Ptr(0) && Ptr(1) && Value(2, SpecialType.System_Int64)) {
                if (definition.ReturnsVoid && method.Name is "MemCpy" or "MemMove" or "MemSwap") return Kind.Copy;
                if (method.Name == "MemCmp" && definition.ReturnType.SpecialType == SpecialType.System_Int32) return Kind.Read;
            }
            if (!definition.ReturnsVoid) return Kind.None;
            return method.Name switch {
                "MemCpyReplicate" when parameters.Length == 4 && Ptr(0) && Ptr(1) && Int(2) && Int(3) => Kind.Copy,
                "MemCpyStride" when parameters.Length == 6 && Ptr(0) && Int(1) && Ptr(2) && Int(3) && Int(4) && Int(5) => Kind.Copy,
                "MemClear" when parameters.Length == 2 && Ptr(0) && Value(1, SpecialType.System_Int64) => Kind.Write,
                "MemSet" when parameters.Length == 3 && Ptr(0) && Value(1, SpecialType.System_Byte) && Value(2, SpecialType.System_Int64) => Kind.Write,
                _ => Kind.None,
            };
        }
        if (definition.Arity == 2) {
            return method.Name == "As" && parameters.Length == 1 && parameters[0].RefKind == RefKind.Ref &&
                Type(parameters[0].Type, definition.TypeParameters[0]) && definition.ReturnsByRef &&
                Type(definition.ReturnType, definition.TypeParameters[1]) ? Kind.Address : Kind.None;
        }
        if (definition.Arity != 1) return Kind.None;
        var t = definition.TypeParameters[0];
        bool Arg(int index, RefKind passing) => parameters[index].RefKind == passing && Type(parameters[index].Type, t);
        var valueReturn = !definition.ReturnsByRef && Type(definition.ReturnType, t);
        // The four array operations have unconstrained T in this Unity version;
        // the other public generic intrinsics require a value type.
        if (method.Name == "ReadArrayElement" && parameters.Length == 2 && Ptr(0) && Int(1) && valueReturn) return Kind.Read;
        if (method.Name == "ReadArrayElementWithStride" && parameters.Length == 3 && Ptr(0) && Int(1) && Int(2) && valueReturn) return Kind.Read;
        if (!definition.ReturnsByRef && definition.ReturnsVoid) {
            if (method.Name == "WriteArrayElement" && parameters.Length == 3 && Ptr(0) && Int(1) && Arg(2, RefKind.None)) return Kind.Write;
            if (method.Name == "WriteArrayElementWithStride" && parameters.Length == 4 && Ptr(0) && Int(1) && Int(2) && Arg(3, RefKind.None)) return Kind.Write;
        }
        if (!t.HasValueTypeConstraint) return Kind.None;
        if (method.Name is "SizeOf" or "AlignOf" && parameters.Length == 0 && !definition.ReturnsByRef &&
            definition.ReturnType.SpecialType == SpecialType.System_Int32) return Kind.Layout;
        if (method.Name == "AddressOf" && parameters.Length == 1 && Arg(0, RefKind.Ref) &&
            !definition.ReturnsByRef && Pointer(definition.ReturnType)) return Kind.Address;
        if (definition.ReturnsByRef && Type(definition.ReturnType, t)) {
            if (method.Name == "AsRef" && parameters.Length == 1 && Ptr(0)) return Kind.Address;
            if (method.Name == "ArrayElementAsRef" && parameters.Length == 2 && Ptr(0) && Int(1)) return Kind.Address;
        }
        if (!definition.ReturnsByRef && definition.ReturnsVoid && parameters.Length == 2) {
            if (method.Name == "CopyPtrToStructure" && Ptr(0) && Arg(1, RefKind.Out)) return Kind.Copy;
            if (method.Name == "CopyStructureToPtr" && Arg(0, RefKind.Ref) && Ptr(1)) return Kind.Copy;
        }
        return Kind.None;
    }

    internal static void Append(StringBuilder row, IMethodSymbol method, Compilation compilation) {
        var kind = Classify(method, compilation);
        if (kind == Kind.None) return;
        row.Append("\t!ecs-leaf");
        // Access happens AFTER all arguments. A later argument can schedule work
        // after an earlier one has obtained a component address.
        if (kind is Kind.Read or Kind.Write or Kind.Copy) row.Append("\t!native-memory-access");
        if (kind == Kind.Layout) return;
        // Native typed loads/stores and escaping references can bypass Ent.Get/Read.
        // Retain concrete component types after substitution; plain scalar T adds
        // no ECS dependency. Address/copy operations conservatively retain RW.
        foreach (var argument in method.TypeArguments)
            row.Append(kind == Kind.Read ? "\t!native-component-read=" : "\t!native-component-write=")
                .Append(MethodSummaryType.From(argument).Encode());
    }
}
