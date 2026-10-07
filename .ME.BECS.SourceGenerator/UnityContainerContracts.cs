using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Collections aea9d3bd5e19. Only operations whose bodies cannot allocate or
// dispatch element/key callbacks are unconditional leaves. Allocating constructors
// additionally need an exact call-site proof of a built-in allocator.
internal static class UnityContainerContracts {
    private const string UnsafeList = "Unity.Collections.LowLevel.Unsafe.UnsafeList`1";
    private const string NativeList = "Unity.Collections.NativeList`1";
    private const string NativeMap = "Unity.Collections.NativeHashMap`2";
    private const string UnsafeMap = "Unity.Collections.LowLevel.Unsafe.UnsafeHashMap`2";
    private static bool Same(ITypeSymbol? left, ITypeSymbol? right) =>
        left != null && right != null && SymbolEqualityComparer.Default.Equals(left, right);
    private static bool Owner(INamedTypeSymbol type, string name, Compilation compilation) =>
        Same(type.OriginalDefinition, compilation.GetTypeByMetadataName(name));
    internal static bool IsList(ITypeSymbol type, Compilation compilation) => type is INamedTypeSymbol named &&
        named.ContainingAssembly.Name == "Unity.Collections" && (Owner(named, NativeList, compilation) || Owner(named, UnsafeList, compilation));
    internal static bool IsMap(ITypeSymbol type, Compilation compilation) => type is INamedTypeSymbol named &&
        named.ContainingAssembly.Name == "Unity.Collections" && (Owner(named, NativeMap, compilation) || Owner(named, UnsafeMap, compilation));
    internal static bool IsLocalContainer(ITypeSymbol type, Compilation compilation) => IsList(type, compilation) || IsMap(type, compilation);
    private static bool Eligible(IMethodSymbol method) => method.DeclaringSyntaxReferences.Length == 0 &&
        method.ContainingAssembly.Name == "Unity.Collections" && method.DeclaredAccessibility == Accessibility.Public &&
        method.ContainingType.TypeKind == TypeKind.Struct && !method.IsStatic && !method.IsAbstract &&
        !method.IsVirtual && !method.IsVararg && method.Arity == 0 && !method.ReturnsByRefReadonly;

    internal static void Append(StringBuilder row, IMethodSymbol method, Compilation compilation) {
        if (!Eligible(method)) return;
        var owner = method.ContainingType;
        var list = Owner(owner, UnsafeList, compilation) || Owner(owner, NativeList, compilation);
        var map = Owner(owner, NativeMap, compilation) || Owner(owner, UnsafeMap, compilation);
        if (!list && !map) return;
        var args = owner.TypeArguments;
        var parameters = method.Parameters;
        bool Int(int index) => parameters[index].RefKind == RefKind.None && parameters[index].Type.SpecialType == SpecialType.System_Int32;
        bool Value(int index, ITypeSymbol type) => parameters[index].RefKind == RefKind.None && Same(parameters[index].Type, type);
        var kind = UnityMemoryContracts.Kind.None;
        var memory = false;
        if (!method.ReturnsByRef && method.MethodKind == MethodKind.PropertyGet && parameters.Length == 0) {
            if (method.Name is "get_IsCreated" or "get_IsEmpty" && method.ReturnType.SpecialType == SpecialType.System_Boolean ||
                method.Name is "get_Length" or "get_Capacity" or "get_Count" && method.ReturnType.SpecialType == SpecialType.System_Int32)
                kind = UnityMemoryContracts.Kind.Layout;
            // Native wrappers read metadata through an internal pointer. UnsafeList
            // stores its header inline; neither variant reads a T element here.
            memory = Owner(owner, NativeMap, compilation) || Owner(owner, NativeList, compilation) && method.Name != "get_IsCreated";
        } else if (!method.ReturnsByRef && method.MethodKind == MethodKind.Ordinary &&
                   method.Name == "Clear" && parameters.Length == 0 && method.ReturnsVoid) {
            kind = UnityMemoryContracts.Kind.Layout;
            memory = map || Owner(owner, NativeList, compilation);
        } else if (list) {
            var element = args[0];
            if (method.MethodKind == MethodKind.Constructor && Owner(owner, UnsafeList, compilation) &&
                parameters.Length == 2 && parameters[0].RefKind == RefKind.None &&
                parameters[0].Type is IPointerTypeSymbol pointer && Same(pointer.PointedAtType, element) && Int(1))
                kind = UnityMemoryContracts.Kind.Layout; // Borrowed buffer; no allocation or dereference.
            else if (method.MethodKind == MethodKind.PropertyGet && method.Name == "get_Item" &&
                     !method.ReturnsByRef && parameters.Length == 1 && Int(0) && Same(method.ReturnType, element))
                kind = UnityMemoryContracts.Kind.Read;
            else if (method.MethodKind == MethodKind.PropertySet && method.Name == "set_Item" &&
                     method.ReturnsVoid && parameters.Length == 2 && Int(0) && Value(1, element))
                kind = UnityMemoryContracts.Kind.Write;
            else if (method.MethodKind == MethodKind.Ordinary) {
                if (method.Name == "ElementAt" && method.ReturnsByRef && parameters.Length == 1 && Int(0) && Same(method.ReturnType, element))
                    { kind = UnityMemoryContracts.Kind.Address; memory = Owner(owner, NativeList, compilation); }
                else if (!method.ReturnsByRef && method.ReturnsVoid) {
                    if (method.Name == "AddNoResize" && parameters.Length == 1 && Value(0, element)) kind = UnityMemoryContracts.Kind.Write;
                    if (method.Name == "AddRangeNoResize" && (parameters.Length == 1 && Value(0, owner) ||
                        parameters.Length == 2 && parameters[0].RefKind == RefKind.None &&
                        parameters[0].Type is IPointerTypeSymbol ptr && ptr.PointedAtType.SpecialType == SpecialType.System_Void && Int(1)))
                        kind = UnityMemoryContracts.Kind.Copy;
                    if (method.Name is "RemoveAt" or "RemoveAtSwapBack" && parameters.Length == 1 && Int(0) ||
                        method.Name is "RemoveRange" or "RemoveRangeSwapBack" && parameters.Length == 2 && Int(0) && Int(1))
                        kind = UnityMemoryContracts.Kind.Copy;
                }
            }
        }
        if (kind == UnityMemoryContracts.Kind.None) return;
        row.Append("\t!ecs-leaf\t!native-container=1");
        if (memory || kind is UnityMemoryContracts.Kind.Read or UnityMemoryContracts.Kind.Write or UnityMemoryContracts.Kind.Copy)
            row.Append("\t!native-memory-access");
        if (kind == UnityMemoryContracts.Kind.Layout) return;
        foreach (var argument in args)
            row.Append(kind == UnityMemoryContracts.Kind.Read ? "\t!native-component-read=" : "\t!native-component-write=")
                .Append(MethodSummaryType.From(argument).Encode());
    }

    internal static string CreationContract(IObjectCreationOperation creation, Compilation compilation) {
        var method = creation.Constructor;
        if (method == null || !Eligible(method) || method.MethodKind != MethodKind.Constructor) return "";
        var owner = method.ContainingType;
        var list = Owner(owner, UnsafeList, compilation);
        var nativeList = Owner(owner, NativeList, compilation);
        if (!list && !nativeList && !Owner(owner, NativeMap, compilation) && !Owner(owner, UnsafeMap, compilation)) return "";
        var parameters = method.Parameters;
        var allocatorIndex = nativeList && parameters.Length == 1 ? 0 : 1;
        if (parameters.Length != (list ? 3 : allocatorIndex + 1) || parameters.Any(parameter => parameter.RefKind != RefKind.None) ||
            allocatorIndex == 1 && parameters[0].Type.SpecialType != SpecialType.System_Int32 ||
            !Same(parameters[allocatorIndex].Type, compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager+AllocatorHandle")) ||
            list && !Same(parameters[2].Type, compilation.GetTypeByMetadataName("Unity.Collections.NativeArrayOptions"))) return "";
        var argument = creation.Arguments.SingleOrDefault(item => item.Parameter?.Ordinal == allocatorIndex);
        return argument != null && BuiltinAllocatorProof.Value(argument.Value, compilation) ? "\t!ecs-leaf\t!native-container-allocator=builtin-v1" : "";
    }

    // Collections aea9d3bd5e19: these methods can allocate/copy/free, but do not
    // call T. They preserve the allocator or reset it to Invalid when disposed.
    // ONLY LocalContainerAllocatorProof may attach this call-site contract.
    internal static string LocalAllocationContract(IMethodSymbol method, Compilation compilation) {
        if (!Eligible(method) || !IsList(method.ContainingType, compilation) || method.ReturnsByRef || !method.ReturnsVoid) return "";
        var parameters = method.Parameters;
        var element = method.ContainingType.TypeArguments[0];
        bool Int(int i) => parameters[i].RefKind == RefKind.None && parameters[i].Type.SpecialType == SpecialType.System_Int32;
        bool Element(int i) => parameters[i].RefKind == RefKind.In && Same(parameters[i].Type, element);
        var accepted = method.MethodKind == MethodKind.PropertySet && method.Name is "set_Length" or "set_Capacity" &&
            parameters.Length == 1 && Int(0);
        if (method.MethodKind == MethodKind.Ordinary) {
            accepted = method.Name switch {
                "Add" => parameters.Length == 1 && Element(0),
                "AddReplicate" => parameters.Length == 2 && Element(0) && Int(1),
                "Resize" => parameters.Length == 2 && Int(0) && parameters[1].RefKind == RefKind.None &&
                    Same(parameters[1].Type, compilation.GetTypeByMetadataName("Unity.Collections.NativeArrayOptions")),
                "ResizeUninitialized" or "SetCapacity" => parameters.Length == 1 && Int(0),
                "TrimExcess" or "Dispose" => parameters.Length == 0,
                "AddRange" => parameters.Length == 2 && parameters[0].RefKind == RefKind.None &&
                    parameters[0].Type is IPointerTypeSymbol pointer && pointer.PointedAtType.SpecialType == SpecialType.System_Void && Int(1) ||
                    parameters.Length == 1 && parameters[0].RefKind == RefKind.None &&
                    (Owner(method.ContainingType, UnsafeList, compilation) && Same(parameters[0].Type, method.ContainingType) ||
                     Owner(method.ContainingType, NativeList, compilation) && parameters[0].Type is INamedTypeSymbol array &&
                     Owner(array, "Unity.Collections.NativeArray`1", compilation) && array.TypeArguments.Length == 1 && Same(array.TypeArguments[0], element)),
                _ => false,
            };
        }
        return accepted ? "\t!ecs-leaf\t!native-container-local-allocator=builtin-v1\t!native-memory-access\t!native-component-write=" +
            MethodSummaryType.From(element).Encode() : "";
    }

}
