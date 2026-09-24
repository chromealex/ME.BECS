using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ME.BECS.SourceGenerator;

internal static class MethodSummaryContracts {
    internal static int ArgumentEnd(string[] operation) {
        for (var i = 5; i < operation.Length; ++i) if (operation[i].StartsWith("!", StringComparison.Ordinal)) return i;
        return operation.Length;
    }

    internal static string? Value(string[] operation, string name) {
        var prefix = "!" + name + "=";
        foreach (var token in operation) if (token.StartsWith(prefix, StringComparison.Ordinal)) return token.Substring(prefix.Length);
        return null;
    }

    internal static bool Has(string[] operation, string name) => operation.Contains("!" + name);

    internal static bool Has(ISymbol symbol, string attribute) => symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attribute);

    internal static void Append(StringBuilder rows, ISymbol symbol, Compilation compilation,
        System.Collections.Generic.Dictionary<INamedTypeSymbol, int?> refModes) {
        if (symbol is IMethodSymbol scalar && IsScalarComparison(scalar)) rows.Append("\t!scalar-comparison");
        if (symbol is IMethodSymbol intrinsic && (IsObjectConstructor(intrinsic) || IsAddressIntrinsic(intrinsic, compilation) ||
            IsBurstHint(intrinsic, compilation))) rows.Append("\t!ecs-leaf");
        if (Has(symbol, "ME.BECS.DisableContainerSafetyRestrictionAttribute")) rows.Append("\t!disable-safety");
        if (Has(symbol, "ME.BECS.CodeGeneratorIgnoreAttribute")) rows.Append("\t!ignore");
        if (symbol is IMethodSymbol weighted && SymbolEqualityComparer.Default.Equals(weighted.ContainingAssembly,
                compilation.GetTypeByMetadataName("ME.BECS.Ent")?.ContainingAssembly)) {
            var weight = Weight(weighted);
            if (weight != 0) rows.Append("\t!weight=").Append(weight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (symbol is IMethodSymbol method && method.Arity != 0) {
            if (method.Name is "Schedule" or "ScheduleSingleWithInject" or "ScheduleSingleWithInjectByRef" &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByMetadataName("Unity.Jobs.JobHandle")) &&
                method.TypeArguments[0].AllInterfaces.Any(i => i.Name.StartsWith("IJob", StringComparison.Ordinal) &&
                    (i.ContainingNamespace.ToDisplayString() == "Unity.Jobs" || i.ContainingNamespace.ToDisplayString() == "ME.BECS.Jobs")))
                rows.Append("\t!scheduled-job=").Append(MethodSummaryType.From(method.TypeArguments[0]).Encode());
            var safety = method.GetAttributes().FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == "ME.BECS.SafetyCheckAttribute");
            if (safety != null) {
                object? mode = safety.ConstructorArguments.Length == 1 ? safety.ConstructorArguments[0].Value : null;
                foreach (var named in safety.NamedArguments) if (named.Key == "Op") mode = named.Value.Value;
                if (mode is int value && value >= 0 && value <= 2) rows.Append("\t!safety=").Append(value);
                else rows.Append("\t!safety-unknown");
                rows.Append("\t!component=").Append(MethodSummaryType.From(method.TypeArguments[0]).Encode());
            }
        }
        if (symbol is IFieldSymbol field && field.Type is INamedTypeSymbol type &&
            type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IRefOp")) {
            if (!refModes.TryGetValue(type.OriginalDefinition, out var mode)) {
                mode = RefMode(type, compilation);
                refModes.Add(type.OriginalDefinition, mode);
            }
            if (mode.HasValue) rows.Append("\t!ref=").Append(mode.Value);
            else rows.Append("\t!ref-unknown");
            // Reflection GenericTypeArguments includes enclosing type arguments first.
            var component = MethodSummaryType.TypeOwners(type).SelectMany(static t => t.TypeArguments).FirstOrDefault();
            if (component != null) rows.Append("\t!component=").Append(MethodSummaryType.From(component).Encode());
        }
    }

    private static bool IsObjectConstructor(IMethodSymbol method) =>
        method.ContainingType.SpecialType == SpecialType.System_Object &&
        method.MethodKind == MethodKind.Constructor && !method.IsStatic &&
        method.Parameters.Length == 0 && method.DeclaringSyntaxReferences.Length == 0;

    private static bool IsBurstHint(IMethodSymbol method, Compilation compilation) {
        // These exact Burst intrinsics only hint at branch probability/assumptions.
        // The caller still visits argument expressions before exporting this leaf call.
        if (method.DeclaringSyntaxReferences.Length != 0 || method.ContainingAssembly.Name != "Unity.Burst" ||
            !method.IsStatic || method.Arity != 0 || method.Parameters.Length != 1 ||
            method.Parameters[0].RefKind != RefKind.None || method.Parameters[0].Type.SpecialType != SpecialType.System_Boolean ||
            method.ReturnsByRef || method.ReturnsByRefReadonly ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType,
                compilation.GetTypeByMetadataName("Unity.Burst.CompilerServices.Hint"))) return false;
        return (method.Name is "Likely" or "Unlikely" && method.ReturnType.SpecialType == SpecialType.System_Boolean) ||
               (method.Name == "Assume" && method.ReturnsVoid);
    }

    private static bool IsAddressIntrinsic(IMethodSymbol method, Compilation compilation) {
        // Address conversions neither dereference the reference nor dispatch user code.
        // This does NOT whitelist memory reads/writes, generic comparers, or the Unsafe type.
        if (method.DeclaringSyntaxReferences.Length != 0 || !method.IsStatic || method.Arity != 1 || method.Parameters.Length != 1 ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType,
                compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.Unsafe"))) return false;
        var definition = method.OriginalDefinition;
        var parameter = definition.Parameters[0];
        var argument = definition.TypeParameters[0];
        bool IsVoidPointer(ITypeSymbol type) => type is IPointerTypeSymbol pointer && pointer.PointedAtType.SpecialType == SpecialType.System_Void;
        if (method.Name == "AsPointer")
            return !definition.ReturnsByRef && IsVoidPointer(definition.ReturnType) && parameter.RefKind == RefKind.Ref &&
                SymbolEqualityComparer.Default.Equals(parameter.Type, argument);
        if (method.Name == "AsRef")
            return definition.ReturnsByRef && !definition.ReturnsByRefReadonly &&
                SymbolEqualityComparer.Default.Equals(definition.ReturnType, argument) &&
                ((parameter.RefKind == RefKind.None && IsVoidPointer(parameter.Type)) ||
                 (parameter.RefKind == RefKind.In && SymbolEqualityComparer.Default.Equals(parameter.Type, argument)));
        return false;
    }

    // Only strongly typed primitive comparisons. Object overloads, strings, enums and
    // generic comparers are deliberately excluded: they may dispatch to user code.
    internal static bool IsScalarComparison(IMethodSymbol method) {
        var kind = method.ContainingType.SpecialType;
        if (kind is not (SpecialType.System_Boolean or SpecialType.System_Char or
            SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or
            SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double)) return false;
        if (method.IsStatic || method.Arity != 0 || method.Parameters.Length != 1 ||
            method.Parameters[0].RefKind != RefKind.None ||
            !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.ContainingType)) return false;
        return (method.Name == "Equals" && method.ReturnType.SpecialType == SpecialType.System_Boolean) ||
               (method.Name == "CompareTo" && method.ReturnType.SpecialType == SpecialType.System_Int32);
    }

    // Preserve legacy BindingFlags, including instance-only Components entries. Current static
    // UnknownType APIs therefore contribute no separate weight through those entries.
    private static int Weight(IMethodSymbol method) => (method.ContainingType.ToDisplayString(), method.Name, method.IsStatic) switch {
        ("ME.BECS.Ent", "NewEnt_INTERNAL", true) when method.Arity == 1 => 10,
        ("ME.BECS.EntExt", "Read" or "Has" or "TryRead", true) => 1,
        ("ME.BECS.EntExt", "Get" or "Set" or "Remove" or "SetTag", true) => 2,
        ("ME.BECS.EntityConfigEntExt", "ReadStatic" or "HasStatic" or "TryReadStatic", true) => 4,
        ("ME.BECS.UnsafeEntityConfig", "ReadStatic", false) => 3,
        ("ME.BECS.Components", "GetUnknownType" or "RemoveUnknownType", false) => 2,
        ("ME.BECS.Components", "ReadUnknownType" or "HasUnknownType", false) => 1,
        _ => 0,
    };

    private static int? RefMode(INamedTypeSymbol type, Compilation compilation) {
        var local = RefModeFromSource(type, compilation);
        if (local.HasValue) return local;
        var id = type.OriginalDefinition.GetDocumentationCommentId();
        int? imported = null;
        foreach (var attribute in type.ContainingAssembly.GetAttributes()) {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" || attribute.ConstructorArguments.Length != 2 ||
                attribute.ConstructorArguments[0].Value as string != RefOpContractGenerator.MetadataKey || attribute.ConstructorArguments[1].Value is not string payload) continue;
            var fields = payload.Split('\t');
            if (fields.Length != 2 || fields[0] != id) continue;
            if (!int.TryParse(fields[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var mode) || mode < 0 || mode > 2) return null;
            if (imported.HasValue && imported.Value != mode) return null;
            imported = mode;
        }
        return imported;
    }

    internal static int? RefModeFromSource(INamedTypeSymbol type, Compilation compilation) {
        var contract = type.AllInterfaces.First(static i => i.ToDisplayString() == "ME.BECS.IRefOp").GetMembers("Op").FirstOrDefault();
        var property = contract == null ? null : type.FindImplementationForInterfaceMember(contract) as IPropertySymbol;
        if (property != null) {
            foreach (var reference in property.DeclaringSyntaxReferences) {
                var syntax = reference.GetSyntax();
                if (!compilation.SyntaxTrees.Contains(syntax.SyntaxTree)) continue;
                var expression = (syntax as PropertyDeclarationSyntax)?.ExpressionBody?.Expression;
                if (expression == null && syntax is PropertyDeclarationSyntax p) {
                    var getter = p.AccessorList?.Accessors.FirstOrDefault(static a => a.Keyword.ValueText == "get");
                    expression = getter?.ExpressionBody?.Expression;
                    if (expression == null && getter?.Body?.Statements.Count == 1 && getter.Body.Statements[0] is ReturnStatementSyntax ret)
                        expression = ret.Expression;
                }
                if (expression == null) continue;
                var value = compilation.GetSemanticModel(syntax.SyntaxTree).GetConstantValue(expression);
                if (value.HasValue && value.Value is int result && result >= 0 && result <= 2) return result;
            }
        }
        return null;
    }
}
