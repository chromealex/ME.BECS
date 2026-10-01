using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

internal sealed class SystemDependencyInputEmitter {
    internal bool HasSchema { get; private set; }
    private sealed class Plan {
        internal INamedTypeSymbol Owner = null!;
        internal readonly List<(INamedTypeSymbol Type, byte Mode)> Components = new();
        internal readonly List<INamedTypeSymbol> Dependencies = new();
        internal readonly List<(int Code, string Message)> Errors = new();
    }
    private readonly List<Plan> plans = new();
    private readonly HashSet<ITypeSymbol> owners = new(SymbolEqualityComparer.Default);

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation) {
        if (fields[0] == "system-dependencies-schema") {
            if (HasSchema || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            HasSchema = true;
            return true;
        }
        if (!HasSchema || fields.Length != 3 || fields[1] != plans.Count.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Decode(fields[2]).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 2 || rows[0] != "v1") return false;
        INamedTypeSymbol? Resolve(string identity) {
            var type = resolver.ResolveDefinition(identity, out _);
            return type != null && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) ? type : null;
        }
        var owner = Resolve(rows[1]);
        if (owner == null || owners.Contains(owner)) return false;
        var plan = new Plan { Owner = owner };
        foreach (var row in rows.Skip(2)) {
            var parts = row.Split('\t');
            if (parts[0] == "C" && parts.Length == 3) {
                var type = Resolve(parts[2]);
                if (type == null || !byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var mode) || mode > 2) return false;
                plan.Components.Add((type, mode));
            } else if (parts[0] == "D" && parts.Length == 2) {
                var type = Resolve(parts[1]);
                if (type == null || plan.Dependencies.Contains(type, SymbolEqualityComparer.Default)) return false;
                plan.Dependencies.Add(type);
            } else if (parts[0] == "E" && parts.Length == 3 && (parts[1] == "0" || parts[1] == "1")) {
                try { plan.Errors.Add((parts[1] == "0" ? 0 : 1, Decode(parts[2]))); }
                catch (FormatException) { return false; }
            } else return false;
        }
        owners.Add(owner);
        plans.Add(plan);
        return true;
    }

    private static string Decode(string value) {
        try { return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(value)); }
        catch (DecoderFallbackException exception) { throw new FormatException("Invalid dependency text encoding", exception); }
    }
    private static string TypeName(INamedTypeSymbol type) =>
        (type.IsGenericType && type.IsDefinition ? type.ConstructUnboundGenericType() : type).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    internal void Append(StringBuilder source) {
        const string collection = "global::System.Collections.Generic.";
        const string component = "global::ME.BECS.Editor.ComponentDependencyGraphInfo";
        const string error = "global::ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error";
        var depsType = collection + "HashSet<global::System.Type>";
        var componentsType = collection + "List<" + component + ">";
        var errorsType = collection + "List<" + error + ">";
        source.Append("\nnamespace ME.BECS.Editor { public static unsafe partial class StaticMethods {\n");
        foreach (var field in new[] { ("Dependencies", depsType), ("Components", componentsType), ("Errors", errorsType) })
            source.Append("private static ").Append(collection).Append("Dictionary<global::System.Type, ").Append(field.Item2).Append("> sourceSystem").Append(field.Item1).Append(";\n");
        foreach (var getter in new[] { ("GetSystemDependencies", "Dependencies", depsType), ("GetSystemComponentsDependencies", "Components", componentsType), ("GetSystemDependenciesErrors", "Errors", errorsType) })
            source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static ").Append(getter.Item3).Append(' ').Append(getter.Item1)
                .Append("(global::System.Type type) { InitializeSystemDependenciesInfo(); return sourceSystem").Append(getter.Item2).Append("[type]; }\n");
        source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void InitializeSystemDependenciesInfo() {\nif (sourceSystemDependencies != null) return;\n")
            .Append("var dependencies = new ").Append(collection).Append("Dictionary<global::System.Type, ").Append(depsType).Append(">();\n")
            .Append("var components = new ").Append(collection).Append("Dictionary<global::System.Type, ").Append(componentsType).Append(">();\n")
            .Append("var errors = new ").Append(collection).Append("Dictionary<global::System.Type, ").Append(errorsType).Append(">();\n");
        foreach (var plan in plans) {
            var owner = "typeof(" + TypeName(plan.Owner) + ")";
            source.Append("components.Add(").Append(owner).Append(", new ").Append(componentsType).Append(" {\n");
            foreach (var entry in plan.Components)
                source.Append("new ").Append(component).Append(" { type = typeof(").Append(TypeName(entry.Type)).Append("), op = ").Append(entry.Mode.ToString(CultureInfo.InvariantCulture)).Append(" },\n");
            source.Append("});\nerrors.Add(").Append(owner).Append(", new ").Append(errorsType).Append(" {\n");
            foreach (var entry in plan.Errors)
                source.Append("new ").Append(error).Append(" { code = (").Append(error).Append(".Code)").Append(entry.Code.ToString(CultureInfo.InvariantCulture))
                    .Append(", message = ").Append(SymbolDisplay.FormatLiteral(entry.Message, true)).Append(" },\n");
            source.Append("});\ndependencies.Add(").Append(owner).Append(", ");
            if (plan.Dependencies.Count == 0) source.Append("null");
            else source.Append("new ").Append(depsType).Append(" { ").Append(string.Join(", ", plan.Dependencies.Select(type => "typeof(" + TypeName(type) + ")"))).Append(" }");
            source.Append(");\n");
        }
        source.Append("sourceSystemComponents = components; sourceSystemErrors = errors; sourceSystemDependencies = dependencies;\n} } }\n");
    }
}
