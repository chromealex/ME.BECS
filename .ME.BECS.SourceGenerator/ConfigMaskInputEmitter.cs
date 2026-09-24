using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class ConfigMaskInputEmitter {
    private readonly INamedTypeSymbol type;
    private readonly IFieldSymbol[] fields;
    private ConfigMaskInputEmitter(INamedTypeSymbol type, IFieldSymbol[] fields) { this.type = type; this.fields = fields; }

    internal static bool TryCreate(INamedTypeSymbol? type, string orderedFields, Compilation compilation, out ConfigMaskInputEmitter? entry) {
        entry = null;
        var contract = compilation.GetTypeByMetadataName("ME.BECS.IConfigComponent");
        if (type == null || !type.IsUnmanagedType || type.IsRefLikeType || MethodSummaryType.From(type).IsOpen ||
            !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) || contract == null ||
            !type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract))) return false;
        var fields = type.GetMembers().OfType<IFieldSymbol>().Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public).ToArray();
        var names = orderedFields.Split(',');
        if (fields.Length < 2 || names.Length != fields.Length || names.Distinct(System.StringComparer.Ordinal).Count() != names.Length ||
            fields.Any(static field => field.IsReadOnly || field.IsFixedSizeBuffer || field.IsImplicitlyDeclared)) return false;
        var ordered = new IFieldSymbol[names.Length];
        for (var index = 0; index < names.Length; ++index) {
            var field = fields.SingleOrDefault(candidate => candidate.Name == names[index]);
            if (field == null) return false;
            ordered[index] = field;
        }
        entry = new ConfigMaskInputEmitter(type, ordered);
        return true;
    }

    internal static void Append(StringBuilder source, IReadOnlyList<ConfigMaskInputEmitter> entries) {
        source.Append("namespace ME.BECS.SourceGenerated { [global::Unity.Burst.BurstCompile] internal static unsafe class ConfigMaskInputs {\n")
            .Append("public static void Initialize() {\n");
        for (var index = 0; index < entries.Count; ++index)
            source.Append("global::ME.BECS.WorldStaticCallbacks.RegisterConfigComponentMaskCallback<")
                .Append(entries[index].type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">(Apply_")
                .Append(index.ToString(CultureInfo.InvariantCulture)).Append(");\n");
        source.Append("}\n");
        for (var index = 0; index < entries.Count; ++index) {
            var entry = entries[index];
            var name = entry.type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            source.Append("[global::Unity.Burst.BurstCompile]\n[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.UnsafeEntityConfig.MethodMaskCallerDelegate))]\n")
                .Append("public static void Apply_").Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("(in global::ME.BECS.UnsafeEntityConfig config, void* componentPtr, void* configComponent, void* maskPtr, in global::ME.BECS.Ent ent) {\n")
                .Append("var allocator = ent.World.state.ptr->allocator;\nvar mask = (global::ME.BECS.BitArray*)maskPtr;\nvar component = (")
                .Append(name).Append("*)componentPtr;\nvar source = (").Append(name).Append("*)configComponent;\n");
            for (var field = 0; field < entry.fields.Length; ++field)
                source.Append("if (mask->IsSet(in allocator, ").Append(field.ToString(CultureInfo.InvariantCulture))
                    .Append(")) component->@").Append(entry.fields[field].Name).Append(" = source->@").Append(entry.fields[field].Name).Append(";\n");
            source.Append("}\n");
        }
        source.Append("} }\n");
    }
}
