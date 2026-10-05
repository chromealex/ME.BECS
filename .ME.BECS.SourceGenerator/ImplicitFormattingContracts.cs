using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Interpolation and built-in string concatenation can invoke ToString/IFormattable
// without an explicit IInvocationOperation. Never certify a missing formatting edge.
internal static class ImplicitFormattingContracts {
    internal const string Schema = "implicit-formatting-schema=1";
    private const string Marker = "implicit-formatting";

    // A symbolic edge preserves the receiver until generic job/system arguments
    // are known. Older readers cannot mistake it for an effect-free operation.
    internal static string? Operation(IOperation value, bool interpolation, int loopDepth) {
        var type = ReceiverValue(value).Type;
        return type?.ContainingAssembly == null ? null : "call\t" + loopDepth.ToString(CultureInfo.InvariantCulture) + "\t" + type.ContainingAssembly.Identity +
            "\tM:__ImplicitFormatting\t" + MethodSummaryType.From(type).Encode() + "\t!" + Marker + "=1:" + (interpolation ? "format" : "concat");
    }

    internal static string InterpolationMode(IInterpolationOperation operation, SemanticModel model) {
        var syntax = operation.Syntax.FirstAncestorOrSelf<InterpolatedStringExpressionSyntax>();
        return InterpolationMode(syntax, model);
    }

    internal static string InterpolationMode(IInterpolatedStringOperation operation, Compilation compilation) {
        var syntax = operation.Syntax as InterpolatedStringExpressionSyntax;
        return syntax == null ? "unknown" : InterpolationMode(syntax, compilation.GetSemanticModel(syntax.SyntaxTree));
    }

    private static string InterpolationMode(InterpolatedStringExpressionSyntax? syntax, SemanticModel model) {
        if (syntax == null) return "unknown";
        var converted = model.GetTypeInfo(syntax).ConvertedType;
        var core = model.Compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly;
        // FormattableStringFactory stores arguments; formatting happens only if
        // the returned object is later consumed. Do not execute a stored callback.
        if (converted != null && (SymbolEqualityComparer.Default.Equals(converted, core.GetTypeByMetadataName("System.FormattableString")) ||
            SymbolEqualityComparer.Default.Equals(converted, core.GetTypeByMetadataName("System.IFormattable")))) return "deferred";
        // Handler-based interpolation has different dispatch/evaluation rules.
        // Do not project its callbacks onto the older String.Format contract.
        if (model.Compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler") != null) return "unknown";
        return converted?.SpecialType is SpecialType.System_String or SpecialType.System_Object ? "format" : "unknown";
    }

    internal static IOperation ReceiverValue(IOperation value) {
        while (value is IConversionOperation conversion && conversion.OperatorMethod == null && !conversion.Conversion.IsUserDefined &&
               (conversion.Conversion.IsIdentity || conversion.Type?.IsReferenceType == true &&
                   (conversion.Operand.Type?.IsValueType == true || conversion.Operand.Type is ITypeParameterSymbol ||
                    conversion.Operand.Type is INamedTypeSymbol { IsSealed: true }))) value = conversion.Operand;
        // Numeric/nullable/user conversions change the value actually formatted.
        return value;
    }

    internal static string[] Resolve(string[] row, Compilation compilation,
        IReadOnlyDictionary<string, MethodSummaryType> bindings, ISet<string> gaps,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts) {
        var mode = MethodSummaryContracts.Value(row, Marker);
        if (mode == null) return row;
        if (row.Length != 6 || row[0] != "call" || row[3] != "M:__ImplicitFormatting" || (mode != "1:concat" && mode != "1:format") ||
            !int.TryParse(row[1], NumberStyles.None, CultureInfo.InvariantCulture, out var loopDepth) ||
            !MethodSummaryType.TryDecode(row[4], out var expression)) {
            gaps.Add("MalformedImplicitFormatting"); return row;
        }
        var concrete = expression!.Substitute(bindings);
        var type = MethodSummaryTypeResolver.Resolve(concrete, compilation) as INamedTypeSymbol;
        if (type?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) type = type.TypeArguments[0] as INamedTypeSymbol;
        if (type == null || concrete.IsOpen || concrete.IsUnsupported || type.IsRefLikeType || type.IsAbstract ||
            (!type.IsValueType && !type.IsSealed)) {
            gaps.Add("ImplicitFormatting: unresolved receiver " + concrete.Identity.Replace('\n', ' ')); return row;
        }
        var core = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly;
        var formattable = core.GetTypeByMetadataName("System.IFormattable");
        IMethodSymbol? target = null;
        string? sourceId = null;
        if (mode == "1:format") {
            var spanContract = core.GetTypeByMetadataName("System.ISpanFormattable");
            if (spanContract != null && type.AllInterfaces.Contains(spanContract, SymbolEqualityComparer.Default) &&
                !BclFormattingContracts.IsCultureIndependent(type, compilation)) {
                gaps.Add("ImplicitFormatting: span formatting " + type.ToDisplayString()); return row;
            }
            if (formattable != null && type.AllInterfaces.Contains(formattable, SymbolEqualityComparer.Default)) {
                var member = formattable.GetMembers("ToString").OfType<IMethodSymbol>().SingleOrDefault(method =>
                    method.Arity == 0 && method.Parameters.Length == 2 && method.ReturnType.SpecialType == SpecialType.System_String &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, core.GetTypeByMetadataName("System.IFormatProvider")));
                if (member != null) {
                    target = MethodSummaryInterfaceMap.Implementation(type, member);
                    var mappedOwner = target?.ContainingType ?? type;
                    var targetId = target == null ? null : MethodSummaryIdentity.Get(target);
                    if (targetId == null || !methods.ContainsKey((target!.ContainingAssembly.Identity.ToString(), targetId)))
                        sourceId = MethodSummaryInterfaceMap.FindSourceBody(MethodSummaryType.From(mappedOwner), MethodSummaryType.From(formattable),
                            member.ContainingAssembly.Identity.ToString(), MethodSummaryIdentity.Get(member)!, methods, conflicts);
                    if (target == null && sourceId != null) {
                        // Trimmed reference metadata can omit a private explicit
                        // implementation. The exported exact interface map owns it.
                        return new[] { "call", row[1], mappedOwner.ContainingAssembly.Identity.ToString(), sourceId, MethodSummaryType.From(mappedOwner).Encode(), "!formatting-arity=2" };
                    }
                }
                if (target == null || target.IsAbstract) { gaps.Add("ImplicitFormatting: unavailable IFormattable implementation"); return row; }
            }
        }
        target ??= ObjectToString(type, compilation);
        if (target == null || target.IsAbstract) { gaps.Add("ImplicitFormatting: unavailable ToString override"); return row; }
        var result = MethodSummaryContracts.Operation("call", loopDepth, target, compilation,
            new Dictionary<INamedTypeSymbol, int?>(SymbolEqualityComparer.Default))!.Split('\t');
        if (!methods.ContainsKey((result[2], result[3])) && sourceId != null) result[3] = sourceId;
        result = result.Concat(new[] { "!formatting-arity=" + target.Parameters.Length.ToString(CultureInfo.InvariantCulture) }).ToArray();
        // These exact BCL bodies return the string itself or runtime type metadata.
        // The exception applies only AFTER virtual dispatch, never to an arbitrary
        // object.ToString call whose runtime override is unknown.
        if (target.DeclaringSyntaxReferences.Length == 0 && target.Parameters.Length == 0 &&
            target.ContainingType.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_String &&
            SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, core)) result = result.Concat(new[] { "!ecs-leaf" }).ToArray();
        return result;
    }

    private static IMethodSymbol? ObjectToString(INamedTypeSymbol type, Compilation compilation) {
        var slot = compilation.GetSpecialType(SpecialType.System_Object).GetMembers("ToString").OfType<IMethodSymbol>()
            .SingleOrDefault(method => !method.IsStatic && method.IsVirtual && method.Arity == 0 && method.Parameters.Length == 0 &&
                !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.ReturnType.SpecialType == SpecialType.System_String);
        if (slot == null) return null;
        for (var owner = type; owner != null; owner = owner.BaseType)
            foreach (var method in owner.GetMembers("ToString").OfType<IMethodSymbol>()) {
                var root = method;
                while (root.OverriddenMethod != null) root = root.OverriddenMethod;
                // A new/hiding ToString is not the virtual object slot used by
                // boxing/String.Concat/String.Format. Never infer by name alone.
                if (SymbolEqualityComparer.Default.Equals(root.OriginalDefinition, slot)) return method;
            }
        return null;
    }

    internal static bool IsText(IOperation value) {
        while (value is IConversionOperation conversion && conversion.OperatorMethod == null && !conversion.Conversion.IsUserDefined)
            value = conversion.Operand;
        return value.Type?.SpecialType == SpecialType.System_String || value.ConstantValue is { HasValue: true, Value: null };
    }
}
