using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Collections aea9d3bd5e19: enumeration follows buckets/next indices without
// hashing, equality, formatting or allocator callbacks. KVPair retains a header
// pointer; obtaining it is not an element read and must not imply ownership.
internal static class NativeMapEnumerationContracts {
    private static bool Same(ITypeSymbol? left, ITypeSymbol? right) => left != null && right != null && SymbolEqualityComparer.Default.Equals(left, right);
    private static bool Exact(INamedTypeSymbol type, string name, Compilation compilation) =>
        type.DeclaringSyntaxReferences.Length == 0 && type.ContainingAssembly.Name == "Unity.Collections" &&
        Same(type.OriginalDefinition, compilation.GetTypeByMetadataName(name));
    internal static bool IsPair(ITypeSymbol type, Compilation compilation) => type is INamedTypeSymbol named && Exact(named, "Unity.Collections.KVPair`2", compilation);
    private static bool Nested(INamedTypeSymbol type, string suffix, Compilation compilation) =>
        Exact(type, "Unity.Collections.NativeHashMap`2+" + suffix, compilation) ||
        Exact(type, "Unity.Collections.LowLevel.Unsafe.UnsafeHashMap`2+" + suffix, compilation);

    private static string? Kind(IMethodSymbol method, Compilation compilation, out INamedTypeSymbol? types) {
        types = null;
        if (method.DeclaringSyntaxReferences.Length != 0 || method.ContainingAssembly.Name != "Unity.Collections" ||
            method.DeclaredAccessibility != Accessibility.Public || method.IsAbstract || method.IsVirtual || method.IsVararg ||
            method.Arity != 0 || method.ReturnsByRefReadonly) return null;
        var owner = method.ContainingType;
        var args = method.Parameters;
        if (IsPair(owner, compilation)) {
            types = owner;
            if (method.MethodKind == MethodKind.PropertyGet && args.Length == 0) {
                if (method.IsStatic && !method.ReturnsByRef && method.Name == "get_Null" && Same(method.ReturnType, owner)) return "header";
                if (!method.IsStatic && !method.ReturnsByRef && method.Name == "get_Key" && Same(method.ReturnType, owner.TypeArguments[0])) return "key";
                if (!method.IsStatic && method.ReturnsByRef && method.Name == "get_Value" && Same(method.ReturnType, owner.TypeArguments[1])) return "value";
            }
            if (!method.IsStatic && !method.ReturnsByRef && method.MethodKind == MethodKind.Ordinary && method.Name == "GetKeyValue" &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && args.Length == 2 &&
                args.Select((arg, i) => arg.RefKind == RefKind.Out && Same(arg.Type, owner.TypeArguments[i])).All(value => value)) return "pair";
            return null;
        }
        if (method.IsStatic || method.ReturnsByRef || args.Length != 0) return null;
        if (UnityContainerContracts.IsMap(owner, compilation) || Nested(owner, "ReadOnly", compilation)) {
            types = Nested(owner, "ReadOnly", compilation) ? owner.ContainingType : owner;
            return method.MethodKind == MethodKind.Ordinary && method.Name == "GetEnumerator" && method.ReturnType is INamedTypeSymbol enumerator &&
                Nested(enumerator, "Enumerator", compilation) && Same(enumerator.ContainingType, types) ? "enumerator" : null;
        }
        if (!Nested(owner, "Enumerator", compilation)) return null;
        types = owner.ContainingType;
        if (method.MethodKind == MethodKind.Ordinary) {
            if (method.Name == "MoveNext" && method.ReturnType.SpecialType == SpecialType.System_Boolean) return "move";
            if (method.Name is "Reset" or "Dispose" && method.ReturnsVoid) return "header";
        }
        if (method.MethodKind == MethodKind.PropertyGet && method.Name == "get_Current" && method.ReturnType is INamedTypeSymbol pair &&
            IsPair(pair, compilation) && pair.TypeArguments.AsEnumerable().SequenceEqual(types.TypeArguments, SymbolEqualityComparer.Default)) return "header";
        return null;
    }

    internal static bool IsEnumeratorFactory(IMethodSymbol method, Compilation compilation) => Kind(method, compilation, out _) == "enumerator";
    internal static bool IsPairAccess(IMethodSymbol method, Compilation compilation) => Kind(method, compilation, out _) is "key" or "value" or "pair";
    internal static bool IsLeaf(IMethodSymbol method, Compilation compilation) => Kind(method, compilation, out _) != null;
    internal static void Append(StringBuilder row, IMethodSymbol method, Compilation compilation) {
        var kind = Kind(method, compilation, out var types);
        if (kind == null) return;
        row.Append("\t!ecs-leaf\t!native-map-enumeration=1");
        if (kind is "move" or "enumerator" or "key" or "value" or "pair") row.Append("\t!native-memory-access");
        if (kind is "key" or "pair") row.Append("\t!native-component-read=").Append(MethodSummaryType.From(types!.TypeArguments[0]).Encode());
        // Value returns a mutable reference; conservatively retain RW even when
        // this particular caller only reads through that reference.
        if (kind == "value") row.Append("\t!native-component-write=").Append(MethodSummaryType.From(types!.TypeArguments[1]).Encode());
        if (kind == "pair") row.Append("\t!native-component-read=").Append(MethodSummaryType.From(types!.TypeArguments[1]).Encode());
    }
}
