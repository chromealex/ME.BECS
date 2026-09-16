using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ME.BECS.SourceGenerator;

// Discovery is per compilation. This generator never assigns IDs or runs initialization.
[Generator(LanguageNames.CSharp)]
public sealed class BecsCatalogGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax declaration && declaration.BaseList != null,
            static (syntax, cancellation) => Describe(syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, cancellation) as INamedTypeSymbol,
                syntax.SemanticModel.Compilation.Options is Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions options && options.AllowUnsafe))
            .Where(static candidate => candidate != null);
        context.RegisterSourceOutput(candidates.Collect().Combine(context.CompilationProvider.Select(
            static (compilation, _) => compilation.AssemblyName ?? string.Empty)),
            static (production, input) => Emit(production, input.Left, input.Right));
    }

    private static Candidate? Describe(INamedTypeSymbol? type, bool allowUnsafe) {
        if (type == null || type.TypeKind != TypeKind.Struct || !type.IsUnmanagedType || type.IsRefLikeType) return null;
        // Match the public discovery surface of the existing editor generator.
        for (var owner = type; owner != null; owner = owner.ContainingType) {
            if (owner.DeclaredAccessibility != Accessibility.Public || owner.Arity != 0) return null;
        }
        var component = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IComponentBase");
        var ordinary = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IComponent");
        var aspect = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IAspect");
        var system = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.ISystem");
        var entityType = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IEntityType");
        if (!component && !aspect && !system && !entityType) return null;
        // Shared/static phases remain separate from ordinary component registration.
        var shared = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IComponentShared");
        var isStatic = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IConfigComponentStatic");
        var configInitialize = type.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IConfigInitialize");
        var hasFields = type.GetMembers().OfType<IFieldSymbol>().Any(static field => !field.IsStatic);
        var explicitSize = 0;
        foreach (var attribute in type.GetAttributes()) {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.InteropServices.StructLayoutAttribute") continue;
            foreach (var argument in attribute.NamedArguments) {
                if (argument.Key == "Size" && argument.Value.Value is int size) explicitSize = size;
            }
        }
        var tag = !hasFields && explicitSize <= 1;
        var defaultProperty = type.GetMembers("Default").OfType<IPropertySymbol>().FirstOrDefault(property =>
            property.IsStatic && property.DeclaredAccessibility == Accessibility.Public && property.GetMethod != null &&
            property.GetMethod.DeclaredAccessibility == Accessibility.Public &&
            SymbolEqualityComparer.Default.Equals(property.Type, type));
        var partialScope = aspect && allowUnsafe ? DescribePartialScope(type) : null;
        return new Candidate(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), component, aspect,
            ordinary && !shared && !isStatic, tag, !tag && defaultProperty != null, shared, isStatic,
            aspect ? DescribeQuery(type) : null, aspect && allowUnsafe ? DescribeConstruction(type, partialScope != null) : null, partialScope, configInitialize, system, entityType);
    }

    private static string[]? DescribePartialScope(INamedTypeSymbol aspect) {
        var helper = "__BECS_Construct_" + Encode(aspect.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        if (aspect.GetMembers(helper).Length != 0) return null;
        var chain = new System.Collections.Generic.Stack<INamedTypeSymbol>();
        for (var owner = aspect; owner != null; owner = owner.ContainingType) {
            if (owner.IsRecord || owner.DeclaringSyntaxReferences.Length == 0) return null;
            foreach (var reference in owner.DeclaringSyntaxReferences) {
                if (reference.GetSyntax() is not TypeDeclarationSyntax declaration ||
                    !declaration.Modifiers.Any(static m => m.RawKind == (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)) return null;
            }
            chain.Push(owner);
        }
        var prefix = new StringBuilder();
        var closings = chain.Count;
        if (!aspect.ContainingNamespace.IsGlobalNamespace) {
            prefix.Append("namespace ").Append(aspect.ContainingNamespace.ToDisplayString()).Append(" {\n");
            ++closings;
        }
        foreach (var owner in chain) {
            prefix.Append("public ");
            if (owner.IsStatic) prefix.Append("static ");
            if (owner.IsReadOnly) prefix.Append("readonly ");
            if (owner.IsRefLikeType) prefix.Append("ref ");
            prefix.Append(owner.TypeKind == TypeKind.Struct ? "partial struct @" : "partial class @")
                .Append(owner.Name).Append(" {\n");
        }
        return new[] { prefix.ToString(), new string('}', closings), helper };
    }

    private static string[]? DescribeConstruction(INamedTypeSymbol aspect, bool partialAccess) {
        var fields = aspect.GetMembers().OfType<IFieldSymbol>().Where(static f => !f.IsStatic &&
            f.Type is INamedTypeSymbol data && data.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IAspectData"))
            .OrderBy(static f => f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal);
        // Alternating field names and component type names; order is checked against reflection by the bridge.
        var result = new System.Collections.Generic.List<string>();
        foreach (var field in fields) {
            var data = (INamedTypeSymbol)field.Type;
            if ((!partialAccess && field.DeclaredAccessibility != Accessibility.Public) || field.IsReadOnly || field.IsImplicitlyDeclared ||
                data.OriginalDefinition.ToDisplayString() != "ME.BECS.AspectDataPtr<T>" ||
                data.TypeArguments[0] is not INamedTypeSymbol component || !component.IsUnmanagedType ||
                !component.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IComponent")) return null;
            for (var owner = component; owner != null; owner = owner.ContainingType) {
                if (owner.DeclaredAccessibility != Accessibility.Public || owner.Arity != 0) return null;
            }
            result.Add(field.Name);
            result.Add(component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        return result.ToArray();
    }

    private static string[]? DescribeQuery(INamedTypeSymbol aspect) {
        var result = new System.Collections.Generic.List<string>();
        foreach (var field in aspect.GetMembers().OfType<IFieldSymbol>().Where(static f => !f.IsStatic)) {
            if (!field.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == "ME.BECS.QueryWithAttribute")) continue;
            if (field.Type is not INamedTypeSymbol data || !data.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IAspectData")) continue;
            if (data.TypeArguments.Length == 0 || data.TypeArguments[0] is not INamedTypeSymbol component ||
                !component.IsUnmanagedType || !component.AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IComponentBase")) return null;
            for (var owner = component; owner != null; owner = owner.ContainingType) {
                if (owner.DeclaredAccessibility != Accessibility.Public || owner.Arity != 0) return null;
            }
            result.Add(component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        return result.ToArray();
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<Candidate?> input, string assembly) {
        var types = input.Where(static t => t != null).Select(static t => t!).GroupBy(static t => t.Name, StringComparer.Ordinal)
            .Select(static group => group.First()).OrderBy(static t => t.Name, StringComparer.Ordinal).ToArray();
        if (types.Length == 0) return;
        var source = new StringBuilder("// <auto-generated/>\nnamespace ME.BECS.SourceGenerated {\n");
        // Bounded deterministic encoding is shared with the Editor bridge.
        source.Append("public static class Catalog_").Append(Encode(assembly)).Append(" {\n");
        source.Append("public static global::System.Type[] GetComponents() => new global::System.Type[] {\n");
        foreach (var type in types.Where(static t => t.Component)) source.Append("typeof(").Append(type.Name).Append("),\n");
        source.Append("};\npublic static global::System.Type[] GetAspects() => new global::System.Type[] {\n");
        foreach (var type in types.Where(static t => t.Aspect)) source.Append("typeof(").Append(type.Name).Append("),\n");
        source.Append("};\n");
        source.Append("public static global::System.Type[] GetSystems() => new global::System.Type[] {\n");
        foreach (var type in types.Where(static t => t.System)) source.Append("typeof(").Append(type.Name).Append("),\n");
        source.Append("};\n");
        source.Append("public static global::System.Type[] GetEntityTypes() => new global::System.Type[] {\n");
        foreach (var type in types.Where(static t => t.EntityType)) source.Append("typeof(").Append(type.Name).Append("),\n");
        source.Append("};\n");
        // Metadata only: do not read Default or touch StaticTypes during validation.
        source.Append("public static int GetRegistrationFlags(global::System.Type type) {\n");
        foreach (var type in types.Where(static t => t.Component)) {
            var flags = (type.CanRegister ? 1 : 0) | (type.Tag ? 2 : 0) | (type.HasDefault ? 4 : 0);
            source.Append("if (type == typeof(").Append(type.Name).Append(")) return ").Append(flags).Append(";\n");
        }
        source.Append("return -1;\n}\n");
        foreach (var type in types.Where(static t => t.CanRegister)) {
            context.CancellationToken.ThrowIfCancellationRequested();
            source.Append("// Called individually by the future global bootstrap, in its existing order.\n")
                .Append("public static void Register_").Append(Encode(type.Name)).Append("() {\n")
                .Append("global::ME.BECS.StaticTypes<").Append(type.Name).Append(">.Validate(isTag: ")
                .Append(type.Tag ? "true" : "false").Append(", isStatic: false);\n");
            if (type.HasDefault) source.Append("global::ME.BECS.StaticTypes<").Append(type.Name)
                .Append(">.SetDefaultValue(").Append(type.Name).Append(".Default);\n");
            source.Append("}\n");
        }
        // Separate phases: the global bootstrap supplies the legacy classification and ordering.
        foreach (var type in types) {
            if (type.EntityType) source.Append("public static void RegisterEntityType_").Append(Encode(type.Name))
                .Append("(ushort id) => global::ME.BECS.EntityTypes.Register<").Append(type.Name).Append(">(id);\n");
            if (type.System) source.Append("public static void RegisterSystem_").Append(Encode(type.Name))
                .Append("() => global::ME.BECS.StaticSystemTypes<").Append(type.Name).Append(">.Validate();\n");
            if (type.Component) EmitAot(source, type, "Component", "StaticTypes");
            if (type.Shared) EmitAot(source, type, "Shared", "StaticTypesShared");
            if (type.Static) EmitAot(source, type, "Static", "StaticTypesStatic");
            if (type.ConfigInitialize) {
                EmitAot(source, type, "Config", "ConfigInitializeTypes");
                source.Append("public static void RegisterConfig_").Append(Encode(type.Name))
                    .Append("(bool isTag, bool isStatic) => global::ME.BECS.StaticTypes<").Append(type.Name)
                    .Append(">.Validate(isTag, isStatic);\n");
            }
            if (type.Construction != null && type.Construction.Length > 0) {
                source.Append("public static string[] GetAspectConstruction_").Append(Encode(type.Name))
                    .Append("() => new string[] {");
                for (var i = 0; i < type.Construction.Length; i += 2) {
                    source.Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(type.Construction[i], true)).Append(",");
                }
                source.Append("};\npublic static unsafe void ConstructAspect_").Append(Encode(type.Name))
                    .Append("(ref global::ME.BECS.World world) {\n");
                if (type.PartialScope != null) source.Append(type.Name).Append('.').Append(type.PartialScope[2]).Append("(ref world);\n");
                else EmitConstructionBody(source, type);
                source.Append("}\n");
            }
            if (type.Query != null) {
                source.Append("public static global::System.Type[] GetAspectQuery_").Append(Encode(type.Name))
                    .Append("() => new global::System.Type[] {");
                foreach (var component in type.Query) source.Append("typeof(").Append(component).Append("),");
                source.Append("};\npublic static void InitializeAspectQuery_").Append(Encode(type.Name)).Append("() {\n");
                if (type.Query.Length > 0) source.Append("global::ME.BECS.AspectTypeInfo.with.Get(global::ME.BECS.AspectTypeInfo<").Append(type.Name)
                    .Append(">.typeId).Resize(").Append(type.Query.Length).Append(");\n");
                for (var index = 0; index < type.Query.Length; ++index) {
                    source.Append("global::ME.BECS.AspectTypeInfo.with.Get(global::ME.BECS.AspectTypeInfo<").Append(type.Name)
                        .Append(">.typeId).Get(").Append(index).Append(") = global::ME.BECS.StaticTypes<")
                        .Append(type.Query[index]).Append(">.typeId;\n");
                }
                source.Append("}\n");
            }
            if (type.Component) source.Append("public static void RegisterGroup_").Append(Encode(type.Name))
                .Append("(global::System.Type groupType) => global::ME.BECS.StaticTypes<").Append(type.Name)
                .Append(">.ApplyGroup(groupType);\n");
            if (type.Aspect) source.Append("public static void RegisterAspect_").Append(Encode(type.Name))
                .Append("() => global::ME.BECS.AspectTypeInfo<").Append(type.Name).Append(">.Validate();\n");
            if (type.Shared) source.Append("public static void RegisterShared_").Append(Encode(type.Name))
                .Append("(bool isTag, bool hasCustomHash) => global::ME.BECS.StaticTypes<").Append(type.Name)
                .Append(">.ValidateShared(isTag, hasCustomHash);\n");
            if (type.Static) source.Append("public static void RegisterStatic_").Append(Encode(type.Name))
                .Append("(bool isTag) => global::ME.BECS.StaticTypes<").Append(type.Name)
                .Append(">.ValidateStatic(isTag);\n");
        }
        source.Append("}\n}\n");
        foreach (var type in types.Where(static t => t.PartialScope != null && t.Construction != null && t.Construction.Length > 0)) {
            source.Append(type.PartialScope![0]).Append("internal static unsafe void ").Append(type.PartialScope[2])
                .Append("(ref global::ME.BECS.World world) {\n");
            EmitConstructionBody(source, type);
            source.Append("}\n").Append(type.PartialScope[1]).Append('\n');
        }
        context.AddSource("ME.BECS.Catalog.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void EmitAot(StringBuilder source, Candidate type, string phase, string target) {
        source.Append("public static void Aot").Append(phase).Append('_').Append(Encode(type.Name))
            .Append("() => global::ME.BECS.").Append(target).Append('<').Append(type.Name).Append(">.AOT();\n");
    }

    private static void EmitConstructionBody(StringBuilder source, Candidate type) {
        source.Append("var addr = global::ME.BECS.WorldAspectStorage.Initialize(world.id, global::ME.BECS.AspectTypeInfo<")
            .Append(type.Name).Append(">.typeId, global::ME.BECS.TSize<").Append(type.Name).Append(">.size);\n");
        for (var i = 0; i < type.Construction!.Length; i += 2) {
            source.Append("((").Append(type.Name).Append("*)addr.ptr)->@")
                .Append(type.Construction[i]).Append(" = new global::ME.BECS.AspectDataPtr<")
                .Append(type.Construction[i + 1]).Append(">(in world);\n");
        }
    }

    private static string Encode(string value) {
        return ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(value);
    }

    private sealed class Candidate : IEquatable<Candidate> {
        public readonly string Name;
        public readonly bool Component, Aspect, CanRegister, Tag, HasDefault, Shared, Static, ConfigInitialize, System, EntityType;
        public readonly string[]? Query, Construction, PartialScope;
        public Candidate(string name, bool component, bool aspect, bool canRegister, bool tag, bool hasDefault, bool shared, bool isStatic, string[]? query, string[]? construction, string[]? partialScope, bool configInitialize, bool system, bool entityType) {
            Name = name; Component = component; Aspect = aspect; CanRegister = canRegister; Tag = tag; HasDefault = hasDefault;
            Shared = shared; Static = isStatic;
            Query = query;
            Construction = construction;
            PartialScope = partialScope;
            ConfigInitialize = configInitialize;
            System = system;
            EntityType = entityType;
        }
        public bool Equals(Candidate? other) => other != null && Name == other.Name && Component == other.Component &&
            Aspect == other.Aspect && CanRegister == other.CanRegister && Tag == other.Tag && HasDefault == other.HasDefault &&
            Shared == other.Shared && Static == other.Static && ConfigInitialize == other.ConfigInitialize && System == other.System && EntityType == other.EntityType &&
            (Query == null ? other.Query == null : other.Query != null && Query.SequenceEqual(other.Query, StringComparer.Ordinal)) &&
            (Construction == null ? other.Construction == null : other.Construction != null && Construction.SequenceEqual(other.Construction, StringComparer.Ordinal)) &&
            (PartialScope == null ? other.PartialScope == null : other.PartialScope != null && PartialScope.SequenceEqual(other.PartialScope, StringComparer.Ordinal));
        public override bool Equals(object? other) => other is Candidate candidate && Equals(candidate);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);
    }
}
