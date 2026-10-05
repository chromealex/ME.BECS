using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// GetBucket calls the virtual object slot on the concrete unmanaged key. A new
// method with the same name is not that slot. Default ValueType hashing is not
// assumed pure: it can hash fields whose implementations need separate coverage.
internal static class NativeHashKeyCall {
    internal static string[] Operation(ITypeSymbol key) => new[] {
        "call", "0", key.ContainingAssembly.Identity.ToString(), "M:__NativeMapHash",
        MethodSummaryType.From(key).Encode(), "!native-map-hash=1",
    };

    internal static string[] Resolve(string[] row, Compilation compilation,
        IReadOnlyDictionary<string, MethodSummaryType> bindings, ISet<string> gaps) {
        var version = MethodSummaryContracts.Value(row, "native-map-hash");
        if (version == null) return row;
        if (version != "1" || row.Length != 6 || row[0] != "call" || row[3] != "M:__NativeMapHash" ||
            !int.TryParse(row[1], NumberStyles.None, CultureInfo.InvariantCulture, out var depth) ||
            !MethodSummaryType.TryDecode(row[4], out var expression)) {
            gaps.Add("MalformedNativeMapHash"); return row;
        }
        var concrete = expression!.Substitute(bindings);
        var type = MethodSummaryTypeResolver.Resolve(concrete, compilation) as INamedTypeSymbol;
        if (type == null || concrete.IsOpen || concrete.IsUnsupported || !type.IsUnmanagedType || !type.IsValueType) {
            gaps.Add("UnresolvedNativeMapKey: " + concrete.Identity.Replace('\n', ' ')); return row;
        }
        var core = compilation.GetSpecialType(SpecialType.System_Object);
        var slot = core.GetMembers("GetHashCode").OfType<IMethodSymbol>().SingleOrDefault(method =>
            method.IsVirtual && !method.IsStatic && method.Arity == 0 && method.Parameters.Length == 0 &&
            method.ReturnType.SpecialType == SpecialType.System_Int32 && !method.ReturnsByRef && !method.ReturnsByRefReadonly);
        if (slot != null) foreach (var method in type.GetMembers("GetHashCode").OfType<IMethodSymbol>()) {
            var root = method;
            while (root.OverriddenMethod != null) root = root.OverriddenMethod;
            if (!SymbolEqualityComparer.Default.Equals(root.OriginalDefinition, slot)) continue;
            var result = MethodSummaryContracts.Operation("call", depth, method,
                compilation, new Dictionary<INamedTypeSymbol, int?>(SymbolEqualityComparer.Default))!.Split('\t');
            // Sealed primitive value types contain no user fields/callbacks. This
            // exception is applied AFTER dispatch, never to arbitrary object hashing.
            if (method.DeclaringSyntaxReferences.Length == 0 && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, core.ContainingAssembly) &&
                type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte or
                    SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
                    SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double)
                result = result.Concat(new[] { "!ecs-leaf" }).ToArray();
            return result;
        }
        gaps.Add("UnresolvedNativeMapHash: " + type.ToDisplayString());
        return row;
    }
}
