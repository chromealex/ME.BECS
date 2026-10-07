using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Audited Unity 6000.2 instrumentation/value entry points, not arbitrary methods
// in these namespaces. They neither invoke game delegates nor create/synchronize
// jobs. The caller still evaluates every receiver/argument/conversion normally.
internal static class UnityInstrumentationContracts {
    internal static bool IsLeaf(IMethodSymbol method, Compilation compilation) {
        if (method.ContainingAssembly.Name != "UnityEngine.CoreModule" || method.DeclaredAccessibility != Accessibility.Public ||
            method.ContainingType.TypeKind != TypeKind.Struct && method.ContainingType.TypeKind != TypeKind.Class ||
            method.ContainingType.Arity != 0 || method.ReturnsByRef || method.ReturnsByRefReadonly) return false;
        foreach (var parameter in method.Parameters) if (parameter.RefKind != RefKind.None) return false;
        bool Is(ITypeSymbol type, string name) => SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName(name)) &&
            SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, method.ContainingAssembly);
        var owner = method.ContainingType;
        if (Is(owner, "Unity.Jobs.LowLevel.Unsafe.JobsUtility")) {
            return method.IsStatic && method.MethodKind == MethodKind.PropertyGet && method.Parameters.Length == 0 &&
                (method.Name is "get_ThreadIndex" or "get_ThreadIndexCount" && method.ReturnType.SpecialType == SpecialType.System_Int32 ||
                 method.Name == "get_IsExecutingJob" && method.ReturnType.SpecialType == SpecialType.System_Boolean);
        }
        if (owner.TypeKind != TypeKind.Struct) return false;
        if (Is(owner, "Unity.Profiling.ProfilerMarker+AutoScope"))
            return !method.IsStatic && method.MethodKind == MethodKind.Ordinary && method.Name == "Dispose" &&
                method.Parameters.Length == 0 && method.ReturnsVoid;
        if (Is(owner, "Unity.Profiling.ProfilerCategory")) {
            if (method.MethodKind == MethodKind.Constructor)
                return !method.IsStatic && method.ReturnsVoid && method.Parameters.Length is 1 or 2 &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    (method.Parameters.Length == 1 || Is(method.Parameters[1].Type, "Unity.Profiling.ProfilerCategoryColor"));
            if (method.MethodKind == MethodKind.Conversion && method.Name == "op_Implicit")
                return method.IsStatic && method.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, owner) &&
                    method.ReturnType.SpecialType == SpecialType.System_UInt16;
            // These getters only wrap fixed built-in category IDs. No wildcard
            // permits a future getter with registration or user-code behavior.
            return method.IsStatic && method.MethodKind == MethodKind.PropertyGet && method.Parameters.Length == 0 &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, owner) && method.Name is
                "get_Render" or "get_Scripts" or "get_Gui" or "get_Physics" or "get_Physics2D" or "get_Animation" or "get_Ai" or
                "get_Audio" or "get_Video" or "get_Particles" or "get_Lighting" or "get_Network" or "get_Loading" or "get_Vr" or
                "get_Input" or "get_Memory" or "get_VirtualTexturing" or "get_FileIO" or "get_Internal";
        }
        if (!Is(owner, "Unity.Profiling.ProfilerMarker") || method.IsStatic) return false;
        if (method.MethodKind == MethodKind.Constructor) {
            if (!method.ReturnsVoid) return false;
            var count = method.Parameters.Length;
            var offset = count > 0 && Is(method.Parameters[0].Type, "Unity.Profiling.ProfilerCategory") ? 1 : 0;
            var end = count;
            if (offset == 1 && end > offset && Is(method.Parameters[end - 1].Type, "Unity.Profiling.LowLevel.MarkerFlags")) --end;
            // Six exact overloads: string/char*+length, optional category, and
            // optional flags only when category is supplied. No generic payloads.
            return end == offset + 1 && method.Parameters[offset].Type.SpecialType == SpecialType.System_String ||
                end == offset + 2 && method.Parameters[offset].Type is IPointerTypeSymbol pointer &&
                pointer.PointedAtType.SpecialType == SpecialType.System_Char && method.Parameters[offset + 1].Type.SpecialType == SpecialType.System_Int32;
        }
        if (method.MethodKind == MethodKind.PropertyGet && method.Name == "get_Handle")
            return method.Parameters.Length == 0 && method.ReturnType.SpecialType == SpecialType.System_IntPtr;
        if (method.MethodKind != MethodKind.Ordinary) return false;
        if (method.Name == "Auto") return method.Parameters.Length == 0 && Is(method.ReturnType, "Unity.Profiling.ProfilerMarker+AutoScope");
        if (!method.ReturnsVoid) return false;
        if (method.Name == "End") return method.Parameters.Length == 0;
        return method.Name == "Begin" && (method.Parameters.Length == 0 || method.Parameters.Length == 1 && Is(method.Parameters[0].Type, "UnityEngine.Object"));
    }
}
