using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Unity 6000.2.14f1 Mono JIT/AOT BCL. Numeric formatting can dispatch through
// CultureInfo.CurrentCulture.GetFormat even with a null provider. Only a proved
// invariant provider removes that callback. Boolean/Char/Guid ignore providers.
internal static class BclFormattingContracts {
    private const string Provider = "invariant-format-provider";
    private static bool Same(ITypeSymbol? left, ITypeSymbol? right) => left != null && right != null && SymbolEqualityComparer.Default.Equals(left, right);
    private static INamedTypeSymbol? CoreType(Compilation compilation, string name) => compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly.GetTypeByMetadataName(name);
    private static bool Core(ISymbol symbol, Compilation compilation) => symbol.DeclaringSyntaxReferences.Length == 0 &&
        SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly);

    internal static bool IsCultureIndependent(INamedTypeSymbol type, Compilation compilation) => Core(type, compilation) &&
        (type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char || Same(type, CoreType(compilation, "System.Guid")));
    private static bool Numeric(INamedTypeSymbol type, Compilation compilation) => Core(type, compilation) && type.SpecialType is
        SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
        SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;

    private static bool ToString(IMethodSymbol method, Compilation compilation) => Core(method, compilation) && !method.IsStatic &&
        method.DeclaredAccessibility == Accessibility.Public && !method.IsAbstract && !method.IsVararg && method.Arity == 0 &&
        method.MethodKind == MethodKind.Ordinary && method.Name == "ToString" && !method.ReturnsByRef && !method.ReturnsByRefReadonly &&
        method.ReturnType.SpecialType == SpecialType.System_String && method.Parameters.All(parameter => parameter.RefKind == RefKind.None);

    private static bool InvariantGetter(IMethodSymbol method, Compilation compilation) => Core(method, compilation) && method.IsStatic &&
        method.DeclaredAccessibility == Accessibility.Public && method.MethodKind == MethodKind.PropertyGet && method.Parameters.Length == 0 &&
        !method.ReturnsByRef && !method.ReturnsByRefReadonly &&
        (method.Name == "get_InvariantCulture" && Same(method.ContainingType, CoreType(compilation, "System.Globalization.CultureInfo")) && Same(method.ReturnType, method.ContainingType) ||
         method.Name == "get_InvariantInfo" && Same(method.ContainingType, CoreType(compilation, "System.Globalization.NumberFormatInfo")) && Same(method.ReturnType, method.ContainingType));

    internal static bool IsLeaf(IMethodSymbol method, Compilation compilation) {
        if (InvariantGetter(method, compilation)) return true;
        if (!ToString(method, compilation) || !IsCultureIndependent(method.ContainingType, compilation)) return false;
        var args = method.Parameters;
        if (args.Length == 0) return true;
        var provider = CoreType(compilation, "System.IFormatProvider");
        if (method.ContainingType.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char)
            return args.Length == 1 && Same(args[0].Type, provider);
        return args.Length is 1 or 2 && args[0].Type.SpecialType == SpecialType.System_String &&
            (args.Length == 1 || Same(args[1].Type, provider));
    }

    internal static void Append(StringBuilder row, IMethodSymbol method, Compilation compilation) {
        if (IsLeaf(method, compilation)) row.Append("\t!ecs-leaf\t!bcl-invariant-format=1");
    }

    // -1 means no supported provider-bearing numeric/IFormattable signature.
    private static int ProviderIndex(IMethodSymbol method, Compilation compilation) {
        var contract = Same(method.ContainingType, CoreType(compilation, "System.IFormattable"));
        if (!(Numeric(method.ContainingType, compilation) || contract) || !Core(method, compilation) || method.IsStatic ||
            method.MethodKind != MethodKind.Ordinary || method.DeclaredAccessibility != Accessibility.Public || method.IsVararg ||
            !contract && !ToString(method, compilation) ||
            method.Arity != 0 || method.Name != "ToString" || method.ReturnsByRef || method.ReturnsByRefReadonly ||
            method.ReturnType.SpecialType != SpecialType.System_String || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None)) return -1;
        var args = method.Parameters;
        if (args.Length is not (1 or 2) || contract && args.Length != 2 || args.Length == 2 && args[0].Type.SpecialType != SpecialType.System_String) return -1;
        return Same(args[args.Length - 1].Type, CoreType(compilation, "System.IFormatProvider")) ? args.Length - 1 : -1;
    }

    internal static string Invocation(IInvocationOperation invocation, Compilation compilation) {
        var index = ProviderIndex(invocation.TargetMethod, compilation);
        if (index < 0) return "";
        IOperation? value = invocation.Arguments.SingleOrDefault(argument => argument.Parameter?.Ordinal == index)?.Value;
        while (true) {
            if (value is IParenthesizedOperation parentheses) value = parentheses.Operand;
            else if (value is IConversionOperation conversion && conversion.OperatorMethod == null && !conversion.Conversion.IsUserDefined)
                value = conversion.Operand;
            else break;
        }
        return value is IPropertyReferenceOperation { Property.GetMethod: { } getter } && InvariantGetter(getter, compilation)
            ? "\t!" + Provider + "=1" : "";
    }

    internal static string[] Resolve(string[] row, Compilation compilation, IReadOnlyDictionary<string, MethodSummaryType> bindings, ISet<string> gaps) {
        var tokens = row.Where(token => token.StartsWith("!" + Provider + "=", StringComparison.Ordinal)).ToArray();
        if (tokens.Length == 0) return row;
        if (tokens.Length != 1 || tokens[0] != "!" + Provider + "=1" || row[0] != "call" ||
            !MethodSummaryType.TryDecode(row[4], out var expression)) { gaps.Add("MalformedInvariantFormatProvider: " + row[3]); return row; }
        var owner = MethodSummaryTypeResolver.Resolve(expression!.Substitute(bindings), compilation) as INamedTypeSymbol;
        // A provider fact never suppresses a user IFormattable body. It says
        // nothing about that implementation's ECS effects, recursion or callbacks.
        if (owner == null || !Numeric(owner, compilation)) return row;
        var target = owner.GetMembers("ToString").OfType<IMethodSymbol>().SingleOrDefault(method => MethodSummaryIdentity.Get(method) == row[3]);
        if (target == null || row[2] != target.ContainingAssembly.Identity.ToString() || ProviderIndex(target, compilation) < 0) {
            gaps.Add("InvalidInvariantFormatTarget: " + row[3]); return row;
        }
        var constrained = row.Count(token => token.StartsWith("!constrained=", StringComparison.Ordinal));
        if (constrained > 1 || row.Length != 6 + constrained || MethodSummaryContracts.ArgumentEnd(row) != 5) {
            gaps.Add("MalformedInvariantFormatProvider: " + row[3]); return row;
        }
        return row.Concat(new[] { "!ecs-leaf", "!bcl-invariant-format=1" }).ToArray();
    }
}
