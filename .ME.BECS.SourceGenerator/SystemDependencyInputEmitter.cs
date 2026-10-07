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
    private string version = "";
    private sealed class Plan {
        internal INamedTypeSymbol Owner = null!;
        internal string OperationsOrigin = "legacy";
        internal string SynchronizationOrigin = "legacy";
        internal readonly List<INamedTypeSymbol> Members = new();
        internal readonly List<(INamedTypeSymbol Type, byte Mode)> Components = new();
        internal readonly List<INamedTypeSymbol> Dependencies = new();
        internal readonly List<(int Code, string Message)> Errors = new();
    }
    private readonly List<Plan> plans = new();
    private readonly HashSet<ITypeSymbol> owners = new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> indirectTypes = new(SymbolEqualityComparer.Default);

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid system dependency plan";
        if (fields[0] == "system-dependencies-schema") {
            if (HasSchema || fields.Length != 3 || fields[1] != "0" || (fields[2] != "djE=" && fields[2] != "djI=")) return false;
            version = fields[2] == "djE=" ? "v1" : "v2";
            HasSchema = true;
            return true;
        }
        if (!HasSchema || fields.Length != 3 || fields[1] != plans.Count.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Decode(fields[2]).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 2 || rows[0] != version) return false;
        INamedTypeSymbol? Resolve(string identity, bool allowInaccessible = false) {
            var type = resolver.ResolveDefinition(identity, out _);
            if (type == null || compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) return type;
            if (!allowInaccessible) return null;
            // These are Editor diagnostic tables, not runtime registrations. IL
            // can legitimately refer to internal/private component types, also
            // through generic arguments. Keep their exact identity and edges.
            TrackIndirectType(type, compilation);
            return type;
        }
        var owner = Resolve(rows[1]);
        var systemContract = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
        if (owner == null || owners.Contains(owner) || systemContract == null || !owner.IsValueType || owner.IsRefLikeType ||
            !owner.AllInterfaces.Contains(systemContract, SymbolEqualityComparer.Default)) return false;
        var plan = new Plan { Owner = owner };
        var selectors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.Skip(2)) {
            var parts = row.Split('\t');
            if (parts[0] == "C" && parts.Length == 3) {
                var type = Resolve(parts[2], allowInaccessible: true);
                if (type == null || !byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var mode) || mode > 2 ||
                    parts[1] != mode.ToString(CultureInfo.InvariantCulture) || plan.Components.Any(entry => SymbolEqualityComparer.Default.Equals(entry.Type, type))) return false;
                if (version == "v2" && (MethodSummaryType.From(type).IsOpen || !type.IsValueType || type.IsRefLikeType ||
                    !type.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract, systemContract) ||
                        SymbolEqualityComparer.Default.Equals(contract, compilation.GetTypeByMetadataName("ME.BECS.IComponentBase"))))) return false;
                plan.Components.Add((type, mode));
            } else if (parts[0] == "D" && parts.Length == 2 && version == "v1") {
                var type = Resolve(parts[1]);
                if (type == null || plan.Dependencies.Contains(type, SymbolEqualityComparer.Default)) return false;
                plan.Dependencies.Add(type);
            } else if (parts[0] == "E" && parts.Length == 3 && (parts[1] == "0" || parts[1] == "1")) {
                try { plan.Errors.Add((parts[1] == "0" ? 0 : 1, Decode(parts[2]))); }
                catch (FormatException) { return false; }
            } else if (parts[0] == "S" && parts.Length == 3 && version == "v2") {
                if ((parts[1] != "operations" && parts[1] != "synchronization") ||
                    (parts[2] != "source" && parts[2] != "legacy" && parts[2] != "il") ||
                    !selectors.Add(parts[1])) return false;
                if (parts[1] == "operations") plan.OperationsOrigin = parts[2];
                else plan.SynchronizationOrigin = parts[2];
            } else if (parts[0] == "M" && parts.Length == 2 && version == "v2") {
                var member = Resolve(parts[1]);
                if (member == null || MethodSummaryType.From(member).IsOpen || !SymbolEqualityComparer.Default.Equals(member.OriginalDefinition, owner) ||
                    plan.Members.Contains(member, SymbolEqualityComparer.Default)) return false;
                plan.Members.Add(member);
            } else return false;
        }
        if (version == "v2") {
            if (MethodSummaryType.From(owner).IsOpen) {
                if (!owner.IsDefinition || plan.Members.Count == 0 || selectors.Count != 0 || plan.Components.Count != 0 || plan.Errors.Count != 0) return false;
                plan.OperationsOrigin = plan.SynchronizationOrigin = "aggregate";
            } else if (selectors.Count != 2 || plan.Members.Count != 0 ||
                       plan.OperationsOrigin == "source" && plan.Components.Count != 0 ||
                       plan.SynchronizationOrigin == "source" && plan.Errors.Count != 0) return false;
        }
        owners.Add(owner);
        plans.Add(plan);
        return true;
    }

    internal bool Validate(InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "";
        if (version != "v2") return true; // Upgrade compatibility: v1 carries the old complete diagnostic table.
        var closed = plans.Where(plan => plan.Members.Count == 0).ToArray();
        var aliases = plans.Where(plan => plan.Members.Count != 0).ToArray();
        foreach (var plan in closed) {
            foreach (var synchronization in new[] { false, true }) {
                // Fresh compiled-IL snapshots are authoritative. Source
                // catalogs remain independent diagnostics and must neither replace
                // an empty snapshot nor veto one with missing/corrupt metadata.
                // Synchronization only supplies advisory messages, never edges.
                if ((synchronization ? plan.SynchronizationOrigin : plan.OperationsOrigin) == "il") continue;
                var status = resolver.SystemDependencies.Read(plan.Owner, synchronization, resolver, compilation, out var operations, out var errors, out error);
                if (synchronization) {
                    if (status == CompilerJobCatalogs.Status.Complete) {
                        plan.SynchronizationOrigin = "source";
                        plan.Errors.Clear();
                        plan.Errors.AddRange(errors);
                    } else if (plan.SynchronizationOrigin == "source") {
                        // Missing, incomplete or malformed warning metadata must
                        // not prevent bootstrap emission or pretend to prove safety.
                        plan.SynchronizationOrigin = "unavailable";
                    }
                    continue;
                }
                var required = plan.OperationsOrigin == "source";
                if (status == CompilerJobCatalogs.Status.Invalid || required && status != CompilerJobCatalogs.Status.Complete) {
                    error += ". Regenerate system dependency inputs after resolving source coverage.";
                    return false;
                }
                if (status != CompilerJobCatalogs.Status.Complete) continue;
                plan.OperationsOrigin = "source";
                plan.Components.Clear();
                plan.Components.AddRange(operations);
            }
            // Old source/legacy inputs can select current catalog components
            // here without ever passing through Read's C-row resolver. Apply the
            // same indirect emission to them as to the authoritative IL snapshot.
            foreach (var entry in plan.Components) TrackIndirectType(entry.Type, compilation);
            Normalize(plan);
        }
        var system = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
        bool IsSystem(INamedTypeSymbol type) => type.AllInterfaces.Contains(system, SymbolEqualityComparer.Default);
        // Derive edges only after current compiler selections are known. Reusing
        // Editor-computed D rows would retain stale readers/writers after a code edit.
        var writers = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        foreach (var plan in closed) foreach (var entry in plan.Components) {
            if (entry.Mode == 0 || IsSystem(entry.Type)) continue;
            if (!writers.TryGetValue(entry.Type, out var values)) writers.Add(entry.Type, values = new());
            values.Add(plan.Owner);
        }
        foreach (var plan in closed) {
            foreach (var entry in plan.Components) {
                if (IsSystem(entry.Type)) plan.Dependencies.Add(entry.Type);
                if (entry.Mode == 1 || !writers.TryGetValue(entry.Type, out var values)) continue;
                plan.Dependencies.AddRange(values.Where(owner => !SymbolEqualityComparer.Default.Equals(owner, plan.Owner)));
            }
            Normalize(plan);
        }
        foreach (var alias in aliases) {
            var members = closed.Where(plan => SymbolEqualityComparer.Default.Equals(plan.Owner.OriginalDefinition, alias.Owner))
                .OrderBy(plan => JobSafetySummary.ReflectionIdentity(plan.Owner), StringComparer.Ordinal).ToArray();
            if (members.Length != alias.Members.Count || members.Any(plan => !alias.Members.Contains(plan.Owner, SymbolEqualityComparer.Default))) {
                error = "Generic system dependency alias must include every selected specialization exactly once: " + Identity(alias.Owner);
                return false;
            }
            alias.Members.Clear();
            alias.Members.AddRange(members.Select(plan => plan.Owner));
            foreach (var member in members) {
                alias.Components.AddRange(member.Components);
                alias.Errors.AddRange(member.Errors);
                alias.Dependencies.AddRange(member.Dependencies.Select(type => type.IsGenericType ? type.OriginalDefinition : type));
            }
            Normalize(alias);
        }
        foreach (var plan in closed.Where(plan => plan.Owner.IsGenericType))
            if (!aliases.Any(alias => SymbolEqualityComparer.Default.Equals(alias.Owner, plan.Owner.OriginalDefinition))) {
                error = "Missing dependency alias for generic system: " + JobSafetySummary.ReflectionIdentity(plan.Owner);
                return false;
            }
        error = "";
        return true;
    }

    private void TrackIndirectType(INamedTypeSymbol type, Compilation compilation) {
        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) indirectTypes.Add(type);
        if (!compilation.IsSymbolAccessibleWithin(type.OriginalDefinition, compilation.Assembly)) indirectTypes.Add(type.OriginalDefinition);
    }

    private static void Normalize(Plan plan) {
        var components = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        foreach (var entry in plan.Components) {
            components.TryGetValue(entry.Type, out var mode);
            components[entry.Type] = mode | (entry.Mode == 2 ? 3 : entry.Mode + 1);
        }
        plan.Components.Clear();
        plan.Components.AddRange(components.OrderBy(entry => JobSafetySummary.ReflectionIdentity(entry.Key), StringComparer.Ordinal)
            .Select(entry => (entry.Key, (byte)(entry.Value == 3 ? 2 : entry.Value - 1))));
        var dependencies = plan.Dependencies.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>()
            .OrderBy(Identity, StringComparer.Ordinal).ToArray();
        plan.Dependencies.Clear();
        plan.Dependencies.AddRange(dependencies);
        var errors = plan.Errors.Distinct().ToArray();
        plan.Errors.Clear();
        plan.Errors.AddRange(errors);
    }

    internal void AppendMetadata(StringBuilder source) {
        foreach (var plan in plans) {
            var payload = new StringBuilder("v1\n").Append(Identity(plan.Owner));
            foreach (var entry in plan.Components) payload.Append("\nC\t").Append(entry.Mode.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(JobSafetySummary.ReflectionIdentity(entry.Type));
            foreach (var type in plan.Dependencies) payload.Append("\nD\t").Append(Identity(type));
            foreach (var entry in plan.Errors) payload.Append("\nE\t").Append(entry.Code.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Message)));
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.SystemDependencySelection.v1\", ")
                .Append(SymbolDisplay.FormatLiteral(payload.ToString(), true)).Append(")]\n");
            var origin = "v1\n" + Identity(plan.Owner) + "\noperations\t" + plan.OperationsOrigin + "\nsynchronization\t" + plan.SynchronizationOrigin;
            foreach (var member in plan.Members) origin += "\nM\t" + JobSafetySummary.ReflectionIdentity(member);
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.SystemDependencyOrigin.v1\", ")
                .Append(SymbolDisplay.FormatLiteral(origin, true)).Append(")]\n");
        }
    }

    private static string Identity(INamedTypeSymbol type) => !type.IsDefinition || !type.IsGenericType ? JobSafetySummary.ReflectionIdentity(type)! :
        (type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") +
        string.Join("+", MethodSummaryType.TypeOwners(type).Select(owner => owner.MetadataName)) + ", " + type.ContainingAssembly.Identity;

    private static string Decode(string value) {
        try { return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(value)); }
        catch (DecoderFallbackException exception) { throw new FormatException("Invalid dependency text encoding", exception); }
    }
    private static string TypeName(INamedTypeSymbol type) =>
        (type.IsGenericType && type.IsDefinition ? type.ConstructUnboundGenericType() : type).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private string TypeExpression(INamedTypeSymbol type) => indirectTypes.Contains(type)
        ? "global::System.Type.GetType(" + SymbolDisplay.FormatLiteral(Identity(type), true) + ", true)"
        : "typeof(" + TypeName(type) + ")";

    internal void Append(StringBuilder source) {
        const string collection = "global::System.Collections.Generic.";
        const string component = "global::ME.BECS.Editor.ComponentDependencyGraphInfo";
        const string error = "global::ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error";
        var depsType = collection + "HashSet<global::System.Type>";
        var componentsType = collection + "List<" + component + ">";
        var errorsType = collection + "List<" + error + ">";
        source.Append("\nnamespace ME.BECS.SourceGenerated { internal static class EditorSystemDependencies {\n");
        foreach (var field in new[] { ("Dependencies", depsType), ("Components", componentsType), ("Errors", errorsType) })
            source.Append("private static ").Append(collection).Append("Dictionary<global::System.Type, ").Append(field.Item2).Append("> sourceSystem").Append(field.Item1).Append(";\n");
        foreach (var getter in new[] { ("GetSystemDependencies", "Dependencies", depsType), ("GetSystemComponentsDependencies", "Components", componentsType), ("GetSystemDependenciesErrors", "Errors", errorsType) })
            source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static ").Append(getter.Item3).Append(' ').Append(getter.Item1)
                .Append("(global::System.Type type) { InitializeSystemDependenciesInfo(); return sourceSystem").Append(getter.Item2).Append("[type]; }\n");
        source.Append("public static bool ContainsSystem(global::System.Type type) { InitializeSystemDependenciesInfo(); return sourceSystemComponents.ContainsKey(type); }\n");
        source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void InitializeSystemDependenciesInfo() {\nif (sourceSystemDependencies != null) return;\n")
            .Append("var dependencies = new ").Append(collection).Append("Dictionary<global::System.Type, ").Append(depsType).Append(">();\n")
            .Append("var components = new ").Append(collection).Append("Dictionary<global::System.Type, ").Append(componentsType).Append(">();\n")
            .Append("var errors = new ").Append(collection).Append("Dictionary<global::System.Type, ").Append(errorsType).Append(">();\n");
        foreach (var plan in plans) {
            var owner = "typeof(" + TypeName(plan.Owner) + ")";
            source.Append("components.Add(").Append(owner).Append(", new ").Append(componentsType).Append(" {\n");
            foreach (var entry in plan.Components)
                source.Append("new ").Append(component).Append(" { type = ").Append(TypeExpression(entry.Type)).Append(", op = ").Append(entry.Mode.ToString(CultureInfo.InvariantCulture)).Append(" },\n");
            source.Append("});\nerrors.Add(").Append(owner).Append(", new ").Append(errorsType).Append(" {\n");
            foreach (var entry in plan.Errors)
                source.Append("new ").Append(error).Append(" { code = (").Append(error).Append(".Code)").Append(entry.Code.ToString(CultureInfo.InvariantCulture))
                    .Append(", message = ").Append(SymbolDisplay.FormatLiteral(entry.Message, true)).Append(" },\n");
            source.Append("});\ndependencies.Add(").Append(owner).Append(", ");
            if (plan.Dependencies.Count == 0) source.Append("null");
            else source.Append("new ").Append(depsType).Append(" { ").Append(string.Join(", ", plan.Dependencies.Select(TypeExpression))).Append(" }");
            source.Append(");\n");
        }
        source.Append("sourceSystemComponents = components; sourceSystemErrors = errors; sourceSystemDependencies = dependencies;\n} } }\n");
    }
}
