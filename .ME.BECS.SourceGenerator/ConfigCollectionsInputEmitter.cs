using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

internal static class ConfigCollectionsInputEmitter {
    private static IEnumerable<IFieldSymbol> GetCollectionFields(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IFieldSymbol>().Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public &&
            field.Type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IUnmanagedList"));

    internal static uint GetCollectionCount(INamedTypeSymbol type) => checked((uint)GetCollectionFields(type).Count());

    // Match Type.FullName ordering used by the former Editor exporter, including
    // closed/nested generic arguments and their assembly identities.
    private static string ReflectionName(ITypeSymbol type) {
        if (type is IArrayTypeSymbol array) return ReflectionName(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is IPointerTypeSymbol pointer) return ReflectionName(pointer.PointedAtType) + "*";
        if (type is not INamedTypeSymbol named) return type.MetadataName;
        named = named.TupleUnderlyingType ?? named;
        var owners = MethodSummaryType.TypeOwners(named).ToArray();
        var ns = named.ContainingNamespace;
        var name = (ns.IsGlobalNamespace ? "" : ns.ToDisplayString() + ".") + string.Join("+", owners.Select(part => part.MetadataName));
        var arguments = owners.SelectMany(part => part.TypeArguments).ToArray();
        if (arguments.Length != 0)
            name += "[" + string.Join(",", arguments.Select(argument => "[" + ReflectionName(argument) + ", " + argument.ContainingAssembly.Identity + "]")) + "]";
        return name;
    }

    internal static string? Describe(INamedTypeSymbol? type, Compilation compilation, string[]? order, string key,
                                     StringBuilder? registrations = null, string callbackOwner = "") {
        if (type == null || !type.IsUnmanagedType || type.IsRefLikeType || MethodSummaryType.From(type).IsOpen ||
            !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) ||
            compilation.Options is not CSharpCompilationOptions options || !options.AllowUnsafe ||
            !type.AllInterfaces.Any(static i => i.ToDisplayString() is "ME.BECS.IConfigComponent" or "ME.BECS.IConfigComponentStatic" or "ME.BECS.IConfigComponentShared")) return null;
        var fields = GetCollectionFields(type).ToArray();
        // OrderBy is stable: equal collection types retain declaration/metadata order.
        order ??= fields.OrderBy(field => ReflectionName(field.Type), StringComparer.Ordinal).Select(field => field.Name).ToArray();
        if (fields.Length == 0 || fields.Length != order.Length || order.Distinct(StringComparer.Ordinal).Count() != order.Length ||
            fields.Any(field => !order.Contains(field.Name, StringComparer.Ordinal) || !CanMaterialize(field, compilation))) return null;
        fields = order.Select(fieldName => fields.Single(field => field.Name == fieldName)).ToArray();
        var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var body = new StringBuilder();
        registrations ??= body;
        registrations.Append("public static string[] GetFields_").Append(key).Append("() => new string[] { ")
            .Append(string.Join(",", fields.Select(field => SymbolDisplay.FormatLiteral(field.Name, true)))).Append(" };\n");
        registrations.Append("public static uint GetCount_").Append(key).Append("() => ")
            .Append(fields.Length.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
        registrations.Append("public static void Register_").Append(key)
            .Append("() => global::ME.BECS.WorldStaticCallbacks.RegisterConfigComponentCallback<").Append(name).Append(">(").Append(callbackOwner).Append("Apply_").Append(key).Append(");\n");
        body.Append("[global::Unity.Burst.BurstCompile]\n[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.UnsafeEntityConfig.MethodCallerDelegate))]\n")
            .Append("[global::UnityEngine.Scripting.Preserve]\npublic static void Apply_").Append(key)
            .Append("(in global::ME.BECS.UnsafeEntityConfig config, void* componentPtr, in global::ME.BECS.Ent ent) {\n")
            .Append("var component = (").Append(name).Append("*)componentPtr;\n");
        foreach (var field in fields) {
            var target = "component->@" + field.Name;
            var fieldType = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            body.Append("{\nvar res = config.GetCollectionById(").Append(target).Append(".GetConfigId(), out var data, out var length);\n")
                .Append("if (").Append(target).Append(".IsCreated) ").Append(target).Append(".Dispose();\n")
                .Append("if (res) { ").Append(target).Append(" = new ").Append(fieldType).Append("(in ent, data, length); }");
            if (field.Type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IMemList"))
                body.Append(" else { ").Append(target).Append(" = new ").Append(fieldType).Append("(in ent, 1u); }");
            else if (field.Type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IMemArray"))
                body.Append(" else { ").Append(target).Append(" = ").Append(fieldType).Append(".Empty; }");
            body.Append("\n}\n");
        }
        return body.Append("}\n").ToString();
    }

    internal static void Append(StringBuilder source, IReadOnlyList<string> bodies) {
        source.Append("namespace ME.BECS.SourceGenerated { [global::Unity.Burst.BurstCompile] internal static unsafe class ConfigCollectionsInputs {\npublic static void Initialize() {\n");
        for (var index = 0; index < bodies.Count; ++index)
            source.Append("Register_").Append(index.ToString(CultureInfo.InvariantCulture)).Append("();\n");
        source.Append("}\n");
        foreach (var body in bodies) source.Append(body);
        source.Append("} }\n");
    }

    private static bool CanMaterialize(IFieldSymbol field, Compilation compilation) {
        if (field.IsReadOnly || field.IsFixedSizeBuffer || field.IsImplicitlyDeclared ||
            field.Type is not INamedTypeSymbol type || !type.IsUnmanagedType) return false;
        var ent = compilation.GetTypeByMetadataName("ME.BECS.Ent");
        var data = compilation.GetTypeByMetadataName("ME.BECS.safe_ptr`1")?.Construct(compilation.GetSpecialType(SpecialType.System_Byte));
        if (ent == null || data == null) return false;
        bool Method(string name, SpecialType result) => type.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            !method.IsStatic && method.Arity == 0 && method.Parameters.Length == 0 &&
            method.DeclaredAccessibility == Accessibility.Public && method.ReturnType.SpecialType == result);
        if (!Method("GetConfigId", SpecialType.System_UInt32) || !Method("Dispose", SpecialType.System_Void) ||
            !type.GetMembers("IsCreated").OfType<IPropertySymbol>().Any(property => !property.IsStatic &&
                property.Parameters.Length == 0 && property.Type.SpecialType == SpecialType.System_Boolean &&
                property.GetMethod?.DeclaredAccessibility == Accessibility.Public)) return false;
        bool StartsWithEntity(IMethodSymbol constructor) => constructor.DeclaredAccessibility == Accessibility.Public &&
            constructor.Parameters.Length > 0 && constructor.Parameters[0].RefKind == RefKind.In &&
            SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, ent);
        if (!type.InstanceConstructors.Any(constructor => StartsWithEntity(constructor) && constructor.Parameters.Length == 3 &&
            constructor.Parameters[1].RefKind == RefKind.None && constructor.Parameters[1].Type.IsUnmanagedType &&
            compilation.ClassifyCommonConversion(data, constructor.Parameters[1].Type).IsImplicit &&
            constructor.Parameters[2].RefKind == RefKind.None && constructor.Parameters[2].Type.SpecialType == SpecialType.System_UInt32)) return false;
        if (type.AllInterfaces.Any(static contract => contract.ToDisplayString() == "ME.BECS.IMemList"))
            return type.InstanceConstructors.Any(constructor => StartsWithEntity(constructor) && constructor.Parameters.Length == 2 &&
                constructor.Parameters[1].RefKind == RefKind.None && constructor.Parameters[1].Type.SpecialType == SpecialType.System_UInt32);
        if (type.AllInterfaces.Any(static contract => contract.ToDisplayString() == "ME.BECS.IMemArray"))
            return type.GetMembers("Empty").Any(member =>
                member is IFieldSymbol emptyField && emptyField.IsStatic && emptyField.DeclaredAccessibility == Accessibility.Public &&
                    SymbolEqualityComparer.Default.Equals(emptyField.Type, type) ||
                member is IPropertySymbol emptyProperty && emptyProperty.IsStatic && emptyProperty.Parameters.Length == 0 &&
                    emptyProperty.GetMethod?.DeclaredAccessibility == Accessibility.Public && SymbolEqualityComparer.Default.Equals(emptyProperty.Type, type));
        return true;
    }
}
