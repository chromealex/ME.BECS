using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// These external bodies have no entity creation, component API calls or user
// callbacks. Receiver/argument expressions and conversions must still be visited;
// a returned address does not establish JobHandle ownership or synchronization.
internal static class ExternalValueContracts {
    // This is a call-site proof, deliberately NOT part of IsLeaf(method). System.Type
    // and MemberInfo can have user-defined subclasses with effectful virtual getters.
    // typeof(...) instead produces a CLR runtime type without constructing T or running
    // its type initializer. Only these metadata string getters are covered.
    internal static bool IsTypeOfMetadata(IMethodSymbol? method, IOperation? receiver, Compilation compilation) {
        if (method == null || method.DeclaringSyntaxReferences.Length != 0 || method.IsStatic ||
            method.MethodKind != MethodKind.PropertyGet || method.Arity != 0 || method.Parameters.Length != 0 ||
            method.ReturnType.SpecialType != SpecialType.System_String || method.ReturnsByRef || method.ReturnsByRefReadonly ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly)) return false;
        while (true) {
            if (receiver is IConversionOperation conversion && conversion.OperatorMethod == null && !conversion.Conversion.IsUserDefined)
                receiver = conversion.Operand;
            else if (receiver is IParenthesizedOperation parentheses) receiver = parentheses.Operand;
            else break;
        }
        if (receiver is not ITypeOfOperation) return false;
        return method.Name switch {
            "get_Name" => SymbolEqualityComparer.Default.Equals(method.ContainingType, compilation.GetTypeByMetadataName("System.Reflection.MemberInfo")) ||
                          SymbolEqualityComparer.Default.Equals(method.ContainingType, compilation.GetTypeByMetadataName("System.Type")),
            "get_FullName" or "get_AssemblyQualifiedName" =>
                SymbolEqualityComparer.Default.Equals(method.ContainingType, compilation.GetTypeByMetadataName("System.Type")),
            _ => false,
        };
    }

    internal static bool IsLeaf(IMethodSymbol method, Compilation compilation) {
        if (method.DeclaringSyntaxReferences.Length != 0 || method.Arity != 0 || method.IsAbstract || method.IsVirtual || method.IsVararg)
            return false;
        return IsBurstPause(method, compilation) || IsFixedStringValue(method, compilation) ||
               IsJobHandleCombination(method, compilation) || IsAllocatorValue(method, compilation) ||
               BclExceptionContracts.IsLeaf(method, compilation) || UnityInstrumentationContracts.IsLeaf(method, compilation);
    }

    private static bool IsAllocatorValue(IMethodSymbol method, Compilation compilation) {
        if (method.ContainingAssembly.Name != "Unity.Collections" || method.DeclaredAccessibility != Accessibility.Public) return false;
        var owner = method.ContainingType;
        // AllocatorHelper<T>.Allocator only reinterprets the stored T*. It does not
        // construct T or call IAllocator members. Constructor/Dispose do both
        // registration and allocation and are deliberately NOT covered.
        if (!method.IsStatic && method.MethodKind == MethodKind.PropertyGet && method.Name == "get_Allocator" &&
            owner.TypeKind == TypeKind.Struct && owner.TypeArguments.Length == 1 && method.Parameters.Length == 0 &&
            method.ReturnsByRef && !method.ReturnsByRefReadonly &&
            SymbolEqualityComparer.Default.Equals(owner.OriginalDefinition, compilation.GetTypeByMetadataName("Unity.Collections.AllocatorHelper`1")) &&
            SymbolEqualityComparer.Default.Equals(method.ReturnType, owner.TypeArguments[0])) return true;
        if (method.ReturnsByRef || method.ReturnsByRefReadonly) return false;
        var handle = compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager+AllocatorHandle");
        var allocator = compilation.GetTypeByMetadataName("Unity.Collections.Allocator");
        if (handle == null || handle.TypeKind != TypeKind.Struct || handle.Arity != 0 || allocator?.TypeKind != TypeKind.Enum ||
            !SymbolEqualityComparer.Default.Equals(handle.ContainingAssembly, method.ContainingAssembly)) return false;
        if (SymbolEqualityComparer.Default.Equals(owner, handle)) {
            if (!method.IsStatic && method.Parameters.Length == 0 && method.MethodKind == MethodKind.PropertyGet) {
                return method.Name switch {
                    "get_Value" => method.ReturnType.SpecialType == SpecialType.System_Int32,
                    "get_ToAllocator" => SymbolEqualityComparer.Default.Equals(method.ReturnType, allocator),
                    "get_IsCustomAllocator" => method.ReturnType.SpecialType == SpecialType.System_Boolean,
                    "get_Handle" => SymbolEqualityComparer.Default.Equals(method.ReturnType, handle),
                    _ => false,
                };
            }
            if (!method.IsStatic && method.MethodKind == MethodKind.PropertySet && method.Name == "set_Handle" &&
                method.ReturnsVoid && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, handle)) return true;
            if (method.MethodKind != MethodKind.Conversion || method.Name != "op_Implicit") return false;
        } else if (!SymbolEqualityComparer.Default.Equals(owner, compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager")) ||
                   method.MethodKind != MethodKind.Ordinary || method.Name != "ConvertToAllocatorHandle") return false;
        // Preserve the distinction between the implicit conversion (version=0)
        // and ConvertToAllocatorHandle (version retained). Both are value copies,
        // neither proves that the resulting handle points to a built-in allocator.
        return method.IsStatic && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
               SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, allocator) &&
               SymbolEqualityComparer.Default.Equals(method.ReturnType, handle);
    }

    private static bool IsJobHandleCombination(IMethodSymbol method, Compilation compilation) {
        // Audited Unity 6000.2 entry points combine fences without invoking user
        // code or scheduling a new user job. This does NOT prove completion: the
        // synchronization analysis separately tracks the returned fence's inputs.
        if (method.ContainingAssembly.Name != "UnityEngine.CoreModule" || !method.IsStatic ||
            method.MethodKind != MethodKind.Ordinary || method.Name != "CombineDependencies" ||
            method.DeclaredAccessibility != Accessibility.Public || method.ReturnsByRef || method.ReturnsByRefReadonly)
            return false;
        var handle = compilation.GetTypeByMetadataName("Unity.Jobs.JobHandle");
        if (handle == null || handle.TypeKind != TypeKind.Struct || handle.Arity != 0 ||
            !SymbolEqualityComparer.Default.Equals(handle.ContainingAssembly, method.ContainingAssembly) ||
            !SymbolEqualityComparer.Default.Equals(method.ReturnType, handle)) return false;
        foreach (var parameter in method.Parameters) if (parameter.RefKind != RefKind.None) return false;
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, handle)) {
            if (method.Parameters.Length is 2 or 3) {
                foreach (var parameter in method.Parameters)
                    if (!SymbolEqualityComparer.Default.Equals(parameter.Type, handle)) return false;
                return true;
            }
            if (method.Parameters.Length != 1 || method.Parameters[0].Type is not INamedTypeSymbol container ||
                container.TypeKind != TypeKind.Struct || container.TypeArguments.Length != 1 ||
                !SymbolEqualityComparer.Default.Equals(container.ContainingAssembly, method.ContainingAssembly) ||
                !SymbolEqualityComparer.Default.Equals(container.TypeArguments[0], handle)) return false;
            return SymbolEqualityComparer.Default.Equals(container.OriginalDefinition, compilation.GetTypeByMetadataName("Unity.Collections.NativeArray`1")) ||
                   SymbolEqualityComparer.Default.Equals(container.OriginalDefinition, compilation.GetTypeByMetadataName("Unity.Collections.NativeSlice`1"));
        }
        return SymbolEqualityComparer.Default.Equals(method.ContainingType,
                   compilation.GetTypeByMetadataName("Unity.Jobs.LowLevel.Unsafe.JobHandleUnsafeUtility")) &&
               method.Parameters.Length == 2 && method.Parameters[0].Type is IPointerTypeSymbol pointer &&
               SymbolEqualityComparer.Default.Equals(pointer.PointedAtType, handle) &&
               method.Parameters[1].Type.SpecialType == SpecialType.System_Int32;
    }

    private static bool IsBurstPause(IMethodSymbol method, Compilation compilation) {
        if (method.ContainingAssembly.Name != "Unity.Burst" || method.Parameters.Length != 0) return false;
        var owner = method.ContainingType;
        if (method.MethodKind == MethodKind.Ordinary && method.IsStatic && method.Name == "Pause" && method.ReturnsVoid &&
            SymbolEqualityComparer.Default.Equals(owner, compilation.GetTypeByMetadataName("Unity.Burst.Intrinsics.Common"))) return true;
        // SharedStatic's typed reference/factory contracts live in BurstStorageContracts.
        // FunctionPointer<T>.Invoke remains ordinary unresolved callback dispatch.
        return false;
    }

    private static bool IsFixedStringValue(IMethodSymbol method, Compilation compilation) {
        if (method.ContainingAssembly.Name != "Unity.Collections" || method.DeclaredAccessibility != Accessibility.Public) return false;
        var owner = method.ContainingType;
        if (owner.TypeKind != TypeKind.Struct || owner.Arity != 0 || owner.Name is not
            ("FixedString32Bytes" or "FixedString64Bytes" or "FixedString128Bytes" or "FixedString512Bytes" or "FixedString4096Bytes") ||
            !SymbolEqualityComparer.Default.Equals(owner, compilation.GetTypeByMetadataName("Unity.Collections." + owner.Name))) return false;
        // UTF16 -> UTF8 into the inline buffer. Overflow checks format only built-in
        // strings and CopyError; the implicit conversion calls the same constructor.
        // Other constructors/formatters stay opaque. Generic interface calls must
        // first bind to one of these exact implementations, never just IUTF8Bytes.
        if (method.Parameters.Length == 1 && Parameter(0, SpecialType.System_String) &&
            !method.ReturnsByRef && !method.ReturnsByRefReadonly &&
            ((method.MethodKind == MethodKind.Constructor && !method.IsStatic) ||
             (method.MethodKind == MethodKind.Conversion && method.IsStatic && method.Name == "op_Implicit" &&
              SymbolEqualityComparer.Default.Equals(method.ReturnType, owner)))) return true;

        // FixedString.gen.cs (Collections aea9d3bd5e19): these touch only length
        // and inline bytes. Bounds checks format built-in numbers, not user data;
        // TryResize clears the same inline buffer and never invokes an allocator.
        bool Parameter(int index, SpecialType type, RefKind kind = RefKind.None) =>
            method.Parameters[index].RefKind == kind && method.Parameters[index].Type.SpecialType == type;
        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
            return method.ReturnsByRef && !method.ReturnsByRefReadonly && !method.IsStatic &&
                   method.MethodKind == MethodKind.Ordinary && method.Name == "ElementAt" &&
                   method.Parameters.Length == 1 && Parameter(0, SpecialType.System_Int32) &&
                   method.ReturnType.SpecialType == SpecialType.System_Byte;
        if (method.IsStatic)
            return method.MethodKind == MethodKind.PropertyGet && method.Name == "get_UTF8MaxLengthInBytes" &&
                   method.Parameters.Length == 0 && method.ReturnType.SpecialType == SpecialType.System_Int32;
        if (method.MethodKind == MethodKind.PropertyGet) {
            return method.Name switch {
                "get_IsEmpty" => method.Parameters.Length == 0 && method.ReturnType.SpecialType == SpecialType.System_Boolean,
                "get_Length" or "get_Capacity" => method.Parameters.Length == 0 && method.ReturnType.SpecialType == SpecialType.System_Int32,
                "get_Item" => method.Parameters.Length == 1 && Parameter(0, SpecialType.System_Int32) &&
                              method.ReturnType.SpecialType == SpecialType.System_Byte,
                _ => false,
            };
        }
        if (method.MethodKind == MethodKind.PropertySet && method.ReturnsVoid) {
            return method.Name switch {
                "set_Length" or "set_Capacity" => method.Parameters.Length == 1 && Parameter(0, SpecialType.System_Int32),
                "set_Item" => method.Parameters.Length == 2 && Parameter(0, SpecialType.System_Int32) && Parameter(1, SpecialType.System_Byte),
                _ => false,
            };
        }
        if (method.MethodKind != MethodKind.Ordinary) return false;
        return method.Name switch {
            "Clear" => method.Parameters.Length == 0 && method.ReturnsVoid,
            "Add" => method.Parameters.Length == 1 && Parameter(0, SpecialType.System_Byte, RefKind.In) && method.ReturnsVoid,
            "GetUnsafePtr" => method.Parameters.Length == 0 && method.ReturnType is IPointerTypeSymbol pointer &&
                              pointer.PointedAtType.SpecialType == SpecialType.System_Byte,
            "TryResize" => method.Parameters.Length == 2 && Parameter(0, SpecialType.System_Int32) &&
                           method.Parameters[1].RefKind == RefKind.None &&
                           method.Parameters[1].Type.TypeKind == TypeKind.Enum &&
                           SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, compilation.GetTypeByMetadataName("Unity.Collections.NativeArrayOptions")) &&
                           method.ReturnType.SpecialType == SpecialType.System_Boolean,
            _ => false,
        };
    }
}
