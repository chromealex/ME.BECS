using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class GraphJobPatchPlan {
    internal INamedTypeSymbol Job = null!;
    internal string Key = "";
    internal readonly List<(IFieldSymbol Field, int Slot)> Fields = new();

    internal static GraphJobPatchPlan? Create(Compilation compilation, INamedTypeSymbol? job, string identity,
        string plan, IReadOnlyList<INamedTypeSymbol> slots) {
        if (job == null || !job.IsUnmanagedType || MethodSummaryType.From(job).IsOpen ||
            !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly)) return null;
        var inject = compilation.GetTypeByMetadataName("ME.BECS.IInject");
        var wrapper = compilation.GetTypeByMetadataName("ME.BECS.InjectSystem`1");
        var delta = compilation.GetTypeByMetadataName("ME.BECS.InjectDeltaTimeAttribute");
        var softFloat = compilation.GetTypeByMetadataName("sfloat");
        var fields = job.GetMembers().OfType<IFieldSymbol>().Where(static f => !f.IsStatic).ToArray();
        if (fields.Any(static f => f.Type.SpecialType == SpecialType.System_Boolean)) return null;
        bool IsDelta(IFieldSymbol f) => f.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, delta));
        bool IsInject(IFieldSymbol f) => f.Type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, inject));
        var result = new GraphJobPatchPlan { Job = job, Key = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(identity) };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hasSystem = false;
        foreach (var entry in plan.Split(',')) {
            var parts = entry.Split(':');
            if (parts.Length != 2 || !seen.Add(parts[0])) return null;
            var field = fields.SingleOrDefault(f => f.Name == parts[0]);
            if (field == null || field.IsReadOnly) return null;
            var index = -1;
            if (parts[1] == "d") {
                if (field.DeclaredAccessibility != Accessibility.Public && !InputManifestGenerator.HasDeltaSetter(job, field, compilation)) return null;
                if (!IsDelta(field) || IsInject(field) || (field.Type.SpecialType != SpecialType.System_UInt32 &&
                    field.Type.SpecialType != SpecialType.System_Single && !SymbolEqualityComparer.Default.Equals(field.Type, softFloat))) return null;
            } else {
                if (field.DeclaredAccessibility != Accessibility.Public && !HasInjectionSetter(job, field, compilation)) return null;
                if (IsDelta(field) || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out index) ||
                    parts[1] != index.ToString(CultureInfo.InvariantCulture) || index >= slots.Count ||
                    field.Type is not INamedTypeSymbol data || !SymbolEqualityComparer.Default.Equals(data.OriginalDefinition, wrapper) ||
                    !SymbolEqualityComparer.Default.Equals(data.TypeArguments[0], slots[index])) return null;
                // Graph injection selects the first occurrence of the target system type.
                for (var earlier = 0; earlier < index; ++earlier)
                    if (SymbolEqualityComparer.Default.Equals(slots[earlier], slots[index])) return null;
                hasSystem = true;
            }
            result.Fields.Add((field, index));
        }
        return hasSystem && fields.Count(f => IsDelta(f) || IsInject(f)) == result.Fields.Count ? result : null;
    }

    private static string InjectionSetterName(IFieldSymbol field) =>
        "__BecsInject_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(field.Name);

    private static bool HasInjectionSetter(INamedTypeSymbol owner, IFieldSymbol field, Compilation compilation) {
        var marker = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        return owner.GetMembers(InjectionSetterName(field)).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && method.Arity == 0 && method.ReturnsVoid && method.Parameters.Length == 2 &&
            method.DeclaredAccessibility == Accessibility.Public && compilation.IsSymbolAccessibleWithin(method, compilation.Assembly) &&
            method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, owner) &&
            method.Parameters[1].RefKind == RefKind.None && method.Parameters[1].Type is IPointerTypeSymbol pointer &&
            pointer.PointedAtType.SpecialType == SpecialType.System_Void &&
            method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker)));
    }

    internal void Append(StringBuilder source, string storage) {
        var name = this.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        source.Append("public static void RegisterJob_").Append(this.Key).Append("() => global::ME.BECS.JobInject<").Append(name)
            .Append(">.Register(PatchJob_").Append(this.Key).Append(");\n")
            .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.JobPatchInjectDelegate.PatchDelegate))]\n[global::Unity.Burst.BurstCompile]\n")
            .Append("public static void PatchJob_").Append(this.Key).Append("(void* jobPtr, ushort worldId) { var job = (").Append(name).Append("*)jobPtr;\n");
        this.AppendAssignments(source, storage);
        source.Append("}\n");
    }

    internal void AppendSystem(StringBuilder source, string storage, int ownerSlot) {
        source.Append("public static void InjectSystem_").Append(this.Key).Append("() { var job = (")
            .Append(this.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append("*)")
            .Append(storage).Append('[').Append(ownerSlot.ToString(CultureInfo.InvariantCulture)).Append("];\n");
        this.AppendAssignments(source, storage);
        source.Append("}\n");
    }

    private void AppendAssignments(StringBuilder source, string storage) {
        var name = this.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        foreach (var item in this.Fields) {
            if (item.Field.DeclaredAccessibility != Accessibility.Public) {
                source.Append(name).Append('.').Append(item.Slot < 0 ? InputManifestGenerator.DeltaSetterName(item.Field) : InjectionSetterName(item.Field))
                    .Append("(ref *job, ");
                if (item.Slot < 0) source.Append("worldId");
                else source.Append("(void*)").Append(storage).Append('[').Append(item.Slot.ToString(CultureInfo.InvariantCulture)).Append(']');
                source.Append(");\n");
                continue;
            }
            if (item.Slot < 0) source.Append("{ var dtMs = global::ME.BECS.Worlds.GetWorldDeltaTime(worldId);\nvar context = global::ME.BECS.SystemContext.Create(dtMs, default, default);\njob->@")
                .Append(item.Field.Name).Append(" = context.").Append(item.Field.Type.SpecialType == SpecialType.System_UInt32 ? "deltaTimeMs" : "deltaTime").Append("; }\n");
            else source.Append("job->@").Append(item.Field.Name).Append(" = ")
                .Append(item.Field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(".FromPointer((void*)")
                .Append(storage).Append('[').Append(item.Slot.ToString(CultureInfo.InvariantCulture)).Append("]);\n");
        }
    }
}
