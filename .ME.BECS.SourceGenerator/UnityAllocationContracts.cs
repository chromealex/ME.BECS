using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Exact allocation/free entry points. A built-in value must be proved at THIS
// invocation, never inferred for a container's later, possibly mutated allocator.
internal static class UnityAllocationContracts {
    internal static string Invocation(IInvocationOperation operation, Compilation compilation) {
        var method = operation.TargetMethod.OriginalDefinition;
        if (method.DeclaringSyntaxReferences.Length != 0 || !method.IsStatic || method.IsAbstract || method.IsVirtual || method.IsVararg ||
            method.DeclaredAccessibility != Accessibility.Public || method.MethodKind != MethodKind.Ordinary ||
            method.ReturnsByRef || method.ReturnsByRefReadonly || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None)) return "";
        bool Same(ITypeSymbol? a, ITypeSymbol? b) => a != null && b != null && SymbolEqualityComparer.Default.Equals(a, b);
        bool VoidPointer(ITypeSymbol type) => type is IPointerTypeSymbol pointer && pointer.PointedAtType.SpecialType == SpecialType.System_Void;
        var parameters = method.Parameters;
        var index = -1;
        var free = false;
        if (method.ContainingAssembly.Name == "Unity.Collections" &&
            Same(method.ContainingType, compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager")) && parameters.Length >= 2 &&
            Same(parameters[0].Type, compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager+AllocatorHandle"))) {
            var typed = method.Arity == 1 && method.TypeParameters[0].HasUnmanagedTypeConstraint;
            if (method.Name == "Allocate" && (
                method.Arity == 0 && VoidPointer(method.ReturnType) && parameters.Length == 4 &&
                parameters.Skip(1).All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32) ||
                typed && method.ReturnType is IPointerTypeSymbol p && Same(p.PointedAtType, method.TypeParameters[0]) &&
                parameters.Length == 2 && parameters[1].Type.SpecialType == SpecialType.System_Int32)) index = 0;
            if (method.Name == "Free" && method.ReturnsVoid && (
                method.Arity == 0 && VoidPointer(parameters[1].Type) && parameters.Length is 2 or 5 &&
                parameters.Skip(2).All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32) ||
                typed && parameters[1].Type is IPointerTypeSymbol p2 && Same(p2.PointedAtType, method.TypeParameters[0]) &&
                parameters.Length == 3 && parameters[2].Type.SpecialType == SpecialType.System_Int32)) { index = 0; free = true; }
        } else if (method.ContainingAssembly.Name == "UnityEngine.CoreModule" && method.Arity == 0 &&
            Same(method.ContainingType, compilation.GetTypeByMetadataName("Unity.Collections.LowLevel.Unsafe.UnsafeUtility"))) {
            var allocator = compilation.GetTypeByMetadataName("Unity.Collections.Allocator");
            if (method.Name == "Malloc" && VoidPointer(method.ReturnType) && parameters.Length == 3 &&
                parameters[0].Type.SpecialType == SpecialType.System_Int64 && parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                Same(parameters[2].Type, allocator)) index = 2;
            if (method.Name == "Free" && method.ReturnsVoid && parameters.Length == 2 && VoidPointer(parameters[0].Type) &&
                Same(parameters[1].Type, allocator)) { index = 1; free = true; }
        }
        if (index < 0) return "";
        var argument = operation.Arguments.SingleOrDefault(argument => argument.Parameter?.Ordinal == index);
        if (argument == null || !BuiltinAllocatorProof.Value(argument.Value, compilation)) return "";
        var result = "\t!ecs-leaf\t!native-allocation=builtin-v1";
        if (free) {
            result += "\t!native-memory-access";
            foreach (var type in operation.TargetMethod.TypeArguments)
                result += "\t!native-component-write=" + MethodSummaryType.From(type).Encode();
        }
        return result;
    }
}
