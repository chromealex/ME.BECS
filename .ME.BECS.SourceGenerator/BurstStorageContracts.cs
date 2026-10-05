using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Burst 6bb9aca3ef38: context hashes use typeof(T).AssemblyQualifiedName, not
// T.GetHashCode/ToString or T's initializer. SharedStatic factories only acquire a
// native buffer; they do not read its T value or complete pending jobs. Reflection
// overloads need a call-site typeof proof because arbitrary Type getters are virtual.
internal static class BurstStorageContracts {
    private static bool Same(ITypeSymbol? a, ITypeSymbol? b) =>
        a != null && b != null && SymbolEqualityComparer.Default.Equals(a, b);

    private static bool Eligible(IMethodSymbol method) => method.DeclaringSyntaxReferences.Length == 0 &&
        method.ContainingAssembly.Name == "Unity.Burst" && method.DeclaredAccessibility == Accessibility.Public &&
        !method.IsAbstract && !method.IsVirtual && !method.IsVararg && !method.ReturnsByRefReadonly;

    private static bool Shared(IMethodSymbol method, Compilation compilation) => method.ContainingType.TypeKind == TypeKind.Struct &&
        method.ContainingType.Arity == 1 && Same(method.ContainingType.OriginalDefinition, compilation.GetTypeByMetadataName("Unity.Burst.SharedStatic`1"));

    private static bool Factory(IMethodSymbol method, Compilation compilation) => Eligible(method) && Shared(method, compilation) &&
        method.IsStatic && method.MethodKind == MethodKind.Ordinary && !method.ReturnsByRef &&
        Same(method.ReturnType, method.ContainingType) && method.Parameters.All(parameter => parameter.RefKind == RefKind.None);

    private static bool Hash(IMethodSymbol method, Compilation compilation) => Eligible(method) && method.IsStatic &&
        method.MethodKind == MethodKind.Ordinary && !method.ReturnsByRef &&
        Same(method.ContainingType, compilation.GetTypeByMetadataName("Unity.Burst.BurstRuntime")) &&
        (method.Name == "GetHashCode32" && method.ReturnType.SpecialType == SpecialType.System_Int32 ||
         method.Name == "GetHashCode64" && method.ReturnType.SpecialType == SpecialType.System_Int64);

    internal static void Append(StringBuilder row, IMethodSymbol method, Compilation compilation) {
        if (!Eligible(method)) return;
        var parameters = method.Parameters;
        bool Parameter(int index, SpecialType type) => parameters[index].Type.SpecialType == type;
        if (Factory(method, compilation) && (
            method.Name == "GetOrCreate" && method.Arity is 1 or 2 && parameters.Length == 1 && Parameter(0, SpecialType.System_UInt32) ||
            method.Name is "GetOrCreatePartiallyUnsafeWithHashCode" or "GetOrCreatePartiallyUnsafeWithSubHashCode" &&
            method.Arity == 1 && parameters.Length == 2 && Parameter(0, SpecialType.System_UInt32) && Parameter(1, SpecialType.System_Int64) ||
            method.Name == "GetOrCreateUnsafe" && method.Arity == 0 && parameters.Length == 3 &&
            Parameter(0, SpecialType.System_UInt32) && Parameter(1, SpecialType.System_Int64) && Parameter(2, SpecialType.System_Int64))) {
            row.Append("\t!ecs-leaf\t!burst-storage=1");
            return;
        }
        if (Hash(method, compilation) && method.Arity == 1 && parameters.Length == 0) {
            row.Append("\t!ecs-leaf\t!burst-type-hash=1");
            return;
        }
        if (!Shared(method, compilation) || method.IsStatic || method.Arity != 0 || parameters.Length != 0 ||
            method.MethodKind != MethodKind.PropertyGet) return;
        if (method.Name == "get_Data" && method.ReturnsByRef && Same(method.ReturnType, method.ContainingType.TypeArguments[0])) {
            // A writable escaped reference may bypass Ent.Get/Read. Retain its
            // component type after substitution, as with UnsafeUtility.AsRef<T>.
            // Merely returning its address does not dereference the stored value.
            row.Append("\t!ecs-leaf\t!burst-storage=1\t!native-component-write=").Append(MethodSummaryType.From(method.ReturnType).Encode());
        } else if (method.Name == "get_UnsafeDataPointer" && !method.ReturnsByRef &&
                   method.ReturnType is IPointerTypeSymbol pointer && pointer.PointedAtType.SpecialType == SpecialType.System_Void)
            row.Append("\t!ecs-leaf\t!burst-storage=1");
    }

    internal static string Invocation(IInvocationOperation invocation, Compilation compilation) {
        var method = invocation.TargetMethod;
        if (method.Arity != 0) return "";
        var type = compilation.GetTypeByMetadataName("System.Type");
        var parameters = method.Parameters;
        var count = 0;
        if (Hash(method, compilation) && parameters.Length == 1) count = 1;
        else if (Factory(method, compilation) && method.Name == "GetOrCreate" && parameters.Length is 2 or 3 &&
                 parameters[parameters.Length - 1].Type.SpecialType == SpecialType.System_UInt32) count = parameters.Length - 1;
        if (count == 0) return "";
        for (var i = 0; i < count; ++i) {
            if (parameters[i].RefKind != RefKind.None || !Same(parameters[i].Type, type)) return "";
            var argument = invocation.Arguments.SingleOrDefault(argument => argument.Parameter?.Ordinal == i);
            IOperation? value = argument?.Value;
            while (true) {
                if (value is IParenthesizedOperation parentheses) value = parentheses.Operand;
                else if (value is IConversionOperation conversion && conversion.OperatorMethod == null && !conversion.Conversion.IsUserDefined)
                    value = conversion.Operand;
                else break;
            }
            if (value is not ITypeOfOperation) return "";
        }
        return "\t!ecs-leaf\t!burst-typeof=1";
    }

    internal static bool HasUnversionedAccess(string[] operation, Compilation compilation) {
        // Earlier producers declared Data a leaf without its escaped component
        // reference. Those imported rows must not certify an empty dependency set.
        // Counts/weights are unaffected; only dependency selection needs this gate.
        if (operation.Length < 5 || operation[3] != "M:Unity.Burst.SharedStatic`1.get_Data" || !operation.Contains("!ecs-leaf")) return false;
        var owner = compilation.GetTypeByMetadataName("Unity.Burst.SharedStatic`1");
        if (owner == null || owner.ContainingAssembly.Name != "Unity.Burst" ||
            operation[2] != owner.ContainingAssembly.Identity.ToString()) return false;
        return operation.Count(token => token.StartsWith("!burst-storage=", System.StringComparison.Ordinal)) != 1 ||
            !operation.Contains("!burst-storage=1") ||
            operation.Count(token => token.StartsWith("!native-component-write=", System.StringComparison.Ordinal)) != 1;
    }
}
