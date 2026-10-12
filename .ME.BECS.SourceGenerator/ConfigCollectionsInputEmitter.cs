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
            fields.Any(field => order.Contains(field.Name, StringComparer.Ordinal) == false || CanMaterialize(field, compilation) == false ||
                CanMaterializeNested(field.Type, compilation, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)) == false)) return null;
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
        var localIndex = 0;
        for (var fieldIndex = 0; fieldIndex < fields.Length; ++fieldIndex) {
            var field = fields[fieldIndex];
            AppendMaterialize(body, "component->@" + field.Name, (INamedTypeSymbol)field.Type, ref localIndex,
                "config.GetCollectionSourceId<" + name + ">(" + fieldIndex.ToString(CultureInfo.InvariantCulture) + "u)");
        }
        return body.Append("}\n").ToString();
    }

    private static IEnumerable<IFieldSymbol> PublicFields(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IFieldSymbol>().Where(static field => field.IsStatic == false && field.DeclaredAccessibility == Accessibility.Public);

    private static bool IsCollection(ITypeSymbol type) => type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IUnmanagedList");

    private static bool HasNestedCollections(ITypeSymbol type) {
        if (IsCollection(type) == true) return true;
        return type is INamedTypeSymbol named && named.TypeKind == TypeKind.Struct &&
            PublicFields(named).Any(field => HasNestedCollections(field.Type));
    }

    private static bool CanMaterializeNested(ITypeSymbol type, Compilation compilation, HashSet<ITypeSymbol> visiting) {
        if (type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Struct) return true;
        if (visiting.Add(type) == false) return false;
        var element = IsCollection(type) == true ? named.TypeArguments.FirstOrDefault() : null;
        var valid = true;
        if (element != null && HasNestedCollections(element) == true) {
            valid = named.AllInterfaces.Any(static i => i.ToDisplayString() is "ME.BECS.IMemArray" or "ME.BECS.IMemList") &&
                named.GetMembers().OfType<IPropertySymbol>().Any(static property => property.IsIndexer && property.ReturnsByRef) &&
                CanMaterializeNested(element, compilation, visiting);
        } else if (IsCollection(type) == false) {
            valid = PublicFields(named).All(field => (IsCollection(field.Type) == false || CanMaterialize(field, compilation)) &&
                CanMaterializeNested(field.Type, compilation, visiting));
        }
        visiting.Remove(type);
        return valid;
    }

    private static void AppendElementCollections(StringBuilder body, string target, INamedTypeSymbol type, bool dispose, ref int localIndex) {
        var element = type.TypeArguments.FirstOrDefault();
        if (element is not INamedTypeSymbol named || HasNestedCollections(element) == false) return;
        var index = localIndex++;
        var length = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IMemList") == true ? "Count" : "Length";
        body.Append("for (var index").Append(index).Append(" = 0u; index").Append(index).Append(" < ").Append(target).Append('.').Append(length)
            .Append("; ++index").Append(index).Append(") {\nref var item").Append(index).Append(" = ref ").Append(target).Append("[index").Append(index).Append("];\n");
        AppendStructCollections(body, "item" + index, named, dispose, ref localIndex);
        body.Append("}\n");
    }

    private static void AppendStructCollections(StringBuilder body, string target, INamedTypeSymbol type, bool dispose, ref int localIndex) {
        foreach (var field in PublicFields(type)) {
            if (field.Type is not INamedTypeSymbol named || HasNestedCollections(named) == false) continue;
            var child = target + ".@" + field.Name;
            if (IsCollection(named) == true) {
                if (dispose == true) {
                    AppendDispose(body, child, named, ref localIndex);
                } else {
                    AppendMaterialize(body, child, named, ref localIndex);
                }
            } else {
                AppendStructCollections(body, child, named, dispose, ref localIndex);
            }
        }
    }

    private static void AppendDispose(StringBuilder body, string target, INamedTypeSymbol type, ref int localIndex) {
        body.Append("if (").Append(target).Append(".IsCreated == true) {\n");
        AppendElementCollections(body, target, type, dispose: true, ref localIndex);
        body.Append(target).Append(".Dispose();\n}\n");
    }

    private static void AppendMaterialize(StringBuilder body, string target, INamedTypeSymbol type, ref int localIndex, string? sourceId = null) {
        var index = localIndex++;
        var fieldType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        body.Append("{\nvar res").Append(index).Append(" = config.GetCollectionById(").Append(sourceId ?? target + ".GetConfigId()").Append(", out var data").Append(index)
            .Append(", out var length").Append(index).Append(");\n");
        AppendDispose(body, target, type, ref localIndex);
        body.Append("if (res").Append(index).Append(" == true) {\n").Append(target).Append(" = new ").Append(fieldType)
            .Append("(in ent, data").Append(index).Append(", length").Append(index).Append(");\n");
        AppendElementCollections(body, target, type, dispose: false, ref localIndex);
        body.Append("}");
        if (type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IMemList") == true) {
            body.Append(" else { ").Append(target).Append(" = new ").Append(fieldType).Append("(in ent, 1u); }");
        } else if (type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IMemArray") == true) {
            body.Append(" else { ").Append(target).Append(" = ").Append(fieldType).Append(".Empty; }");
        }
        body.Append("\n}\n");
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
