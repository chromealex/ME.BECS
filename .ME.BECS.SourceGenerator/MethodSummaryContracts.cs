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
            IsBurstHint(intrinsic, compilation) || IsMathematicsValueOperation(intrinsic, compilation) ||
            IsFloatVectorConstructor(intrinsic, compilation) || IsFloatVectorArithmetic(intrinsic, compilation))) rows.Append("\t!ecs-leaf");
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

    private static bool IsFloatVectorConstructor(IMethodSymbol method, Compilation compilation) {
        // Audited Unity.Mathematics float2/3/4 constructors only copy float lanes.
        // Conversion constructors, matrices, user structs and implicit operators are
        // separate contracts. Caller argument expressions remain ordinary call edges.
        if (method.MethodKind != MethodKind.Constructor || method.IsStatic || method.DeclaringSyntaxReferences.Length != 0 ||
            method.ContainingAssembly.Name != "Unity.Mathematics" || method.Parameters.Length == 0 ||
            method.Parameters.Any(parameter => parameter.RefKind != RefKind.None)) return false;
        int Width(ITypeSymbol type) {
            if (type.SpecialType == SpecialType.System_Single) return 1;
            for (var width = 2; width <= 4; ++width)
                if (SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName("Unity.Mathematics.float" + width))) return width;
            return 0;
        }
        var target = Width(method.ContainingType);
        if (target < 2) return false;
        var arguments = method.Parameters.Select(parameter => Width(parameter.Type)).ToArray();
        return arguments.All(width => width > 0) &&
            ((arguments.Length == 1 && arguments[0] == 1) || arguments.Sum() == target);
    }

    private static bool IsFloatVectorArithmetic(IMethodSymbol method, Compilation compilation) {
        if (method.MethodKind != MethodKind.UserDefinedOperator || !method.IsStatic || method.Arity != 0 ||
            method.DeclaringSyntaxReferences.Length != 0 || method.ContainingAssembly.Name != "Unity.Mathematics" ||
            method.ReturnsByRef || method.ReturnsByRefReadonly ||
            !SymbolEqualityComparer.Default.Equals(method.ReturnType, method.ContainingType) ||
            method.Parameters.Any(parameter => parameter.RefKind != RefKind.None)) return false;
        var vector = method.ContainingType;
        if (!Enumerable.Range(2, 3).Any(width => SymbolEqualityComparer.Default.Equals(vector,
                compilation.GetTypeByMetadataName("Unity.Mathematics.float" + width)))) return false;
        bool Vector(ITypeSymbol type) => SymbolEqualityComparer.Default.Equals(type, vector);
        if (method.Name is "op_UnaryNegation" or "op_UnaryPlus")
            return method.Parameters.Length == 1 && Vector(method.Parameters[0].Type);
        return method.Name is "op_Addition" or "op_Subtraction" or "op_Multiply" or "op_Division" &&
            method.Parameters.Length == 2 && method.Parameters.Any(parameter => Vector(parameter.Type)) &&
            method.Parameters.All(parameter => Vector(parameter.Type) || parameter.Type.SpecialType == SpecialType.System_Single);
    }

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

    private static bool IsMathematicsValueOperation(IMethodSymbol method, Compilation compilation) {
        // Audited value-only operations. No blanket exemption for math, vectors, constructors,
        // pointers or arbitrary Unity APIs. Argument expressions retain their own call edges.
        if (method.DeclaringSyntaxReferences.Length != 0 || method.ContainingAssembly.Name != "Unity.Mathematics" ||
            method.MethodKind != MethodKind.Ordinary || !method.IsStatic || method.Arity != 0 ||
            method.ReturnsByRef || method.ReturnsByRefReadonly || method.Parameters.Any(static parameter => parameter.RefKind != RefKind.None) ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, compilation.GetTypeByMetadataName("Unity.Mathematics.math"))) return false;
        (SpecialType Scalar, int Width) Shape(ITypeSymbol type) {
            if (type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Int32 or SpecialType.System_UInt32 or
                SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double)
                return (type.SpecialType, 1);
            if (type is not INamedTypeSymbol named || named.Arity != 0 || !named.IsUnmanagedType ||
                !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, method.ContainingAssembly)) return default;
            var name = named.Name;
            if (name.Length < 4 || name[name.Length - 1] < '2' || name[name.Length - 1] > '4' ||
                !SymbolEqualityComparer.Default.Equals(named, compilation.GetTypeByMetadataName("Unity.Mathematics." + name))) return default;
            var scalar = name.Substring(0, name.Length - 1) switch {
                "bool" => SpecialType.System_Boolean, "int" => SpecialType.System_Int32,
                "uint" => SpecialType.System_UInt32, "float" => SpecialType.System_Single,
                "double" => SpecialType.System_Double, _ => SpecialType.None,
            };
            return scalar == SpecialType.None ? default : (scalar, name[name.Length - 1] - '0');
        }
        var result = Shape(method.ReturnType);
        if (result.Width == 0 || method.Parameters.Any(parameter => Shape(parameter.Type).Width == 0)) return false;
        bool SameOperands(int count) => method.Parameters.Length == count &&
            method.Parameters.All(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, method.ReturnType));
        if (method.Name is "all" or "any")
            return method.Parameters.Length == 1 && result.Scalar == SpecialType.System_Boolean && result.Width == 1;
        if (method.Name == "select") {
            if (method.Parameters.Length != 3 || !method.Parameters.Take(2).All(parameter =>
                    SymbolEqualityComparer.Default.Equals(parameter.Type, method.ReturnType))) return false;
            var mask = Shape(method.Parameters[2].Type);
            return mask.Scalar == SpecialType.System_Boolean && (mask.Width == 1 || mask.Width == result.Width);
        }
        if (result.Scalar == SpecialType.System_Boolean) return false;
        if (method.Name is "dot" or "lengthsq" or "distance") {
            var count = method.Name == "lengthsq" ? 1 : 2;
            if (method.Parameters.Length != count || result.Width != 1) return false;
            var operand = Shape(method.Parameters[0].Type);
            if (operand.Scalar != result.Scalar || !method.Parameters.All(parameter =>
                    SymbolEqualityComparer.Default.Equals(parameter.Type, method.Parameters[0].Type))) return false;
            return method.Name == "dot"
                ? result.Scalar is SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single or SpecialType.System_Double
                : result.Scalar is SpecialType.System_Single or SpecialType.System_Double;
        }
        return method.Name switch {
            "min" or "max" => SameOperands(2), "abs" => SameOperands(1), "clamp" => SameOperands(3),
            "saturate" or "sqrt" => result.Scalar is SpecialType.System_Single or SpecialType.System_Double && SameOperands(1),
            _ => false,
        };
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
