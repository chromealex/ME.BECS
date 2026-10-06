using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

internal sealed class ViewTrackerInputEmitter {
    private int capacity = -1;
    private bool compilerSelected;
    private readonly List<INamedTypeSymbol> tracked = new();
    internal int Capacity => this.capacity;
    internal IReadOnlyList<INamedTypeSymbol> Tracked => this.tracked;
    internal IEnumerable<(INamedTypeSymbol Type, bool Module, INamedTypeSymbol[] Components)> Groups =>
        this.entries.GroupBy(static entry => entry.Type, SymbolEqualityComparer.Default).Select(group =>
            (group.First().Type, group.First().Module, group.SelectMany(entry => entry.Components).Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToArray()));
    private sealed class PhasePlan {
        internal string Origin = "";
        internal readonly List<INamedTypeSymbol> Components = new();
    }
    private sealed class Entry {
        internal INamedTypeSymbol Type = null!;
        internal bool Module;
        internal bool IgnoredSnapshot;
        internal INamedTypeSymbol[] Components = Array.Empty<INamedTypeSymbol>();
        internal readonly Dictionary<string, PhasePlan> Phases = new(StringComparer.Ordinal);
    }
    private readonly List<Entry> entries = new();
    private static readonly string[] callbackPhases = { "ApplyState", "ApplyStateParallel" };
    // One class can be both an EntityView and an IViewModule. Keep role-specific
    // input records; Append unions their dependencies into one runtime type ID.
    private readonly HashSet<(string Identity, bool Module)> identities = new();
    private int views;
    private int modules;
    private readonly List<string> records = new();
    private readonly HashSet<INamedTypeSymbol> ignoredComponents = new(SymbolEqualityComparer.Default);
    internal bool HasIgnoredInputDependencies { get; private set; }
    internal bool HasView(INamedTypeSymbol type) => this.entries.Any(entry => !entry.Module && SymbolEqualityComparer.Default.Equals(entry.Type, type));

    internal static bool IsRecord(string kind) => kind is "view-tracker" or "view-tracker-view" or "view-tracker-module";

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid view tracker input";
        if (fields.Length != 3) return false;
        string payload;
        try { payload = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])); }
        catch (FormatException) { return false; }
        var lines = payload.Split('\n');
        if (!this.compilerSelected && lines.Any(line => line.Any(char.IsControl))) return false;
        INamedTypeSymbol? Resolve(string identity, bool allowInaccessible = false) {
            if (identity.Any(char.IsControl)) return null;
            var type = resolver.ResolveDefinition(identity, out _);
            return type != null && !MethodSummaryType.From(type).IsOpen &&
                (allowInaccessible || compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) ? type : null;
        }
        var componentError = "";
        bool ReadComponents(IEnumerable<string> names, List<INamedTypeSymbol> result, bool allowInaccessible = false) {
            var values = names.ToArray();
            if (values.Length == 1 && values[0].Length == 0) return true;
            foreach (var identity in values) {
                var type = Resolve(identity, allowInaccessible);
                var contract = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
                if (type == null || !type.IsUnmanagedType || type.IsRefLikeType || contract == null ||
                    !type.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, contract))) {
                    componentError = "Invalid or inaccessible view tracker component: " + identity;
                    return false;
                }
                result.Add(type);
            }
            return true;
        }
        if (fields[0] == "view-tracker") {
            if (this.capacity >= 0 || fields[1] != "0" || lines.Length < 2 || (lines[0] != "v1" && lines[0] != "v2" && lines[0] != "v3") ||
                !int.TryParse(lines[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 0 || count == int.MaxValue ||
                lines[1] != count.ToString(CultureInfo.InvariantCulture)) return false;
            this.compilerSelected = lines[0] == "v3";
            if (this.compilerSelected ? lines.Length != 2 : lines.Length < 3) return false;
            // Accessibility is finalized after reading owners: an old manifest can
            // still contain private dependencies of a now-ignored tracker owner.
            if (!this.compilerSelected && !ReadComponents(lines.Skip(2), this.tracked, allowInaccessible: true)) { error = componentError; return false; }
            if (this.tracked.Distinct(SymbolEqualityComparer.Default).Count() != this.tracked.Count) return false;
            if (lines[0] == "v2" && !lines.Skip(2).SequenceEqual(lines.Skip(2).OrderBy(static identity => identity, StringComparer.Ordinal))) {
                error = "View tracker v2 component identities must be in canonical ordinal order";
                return false;
            }
            this.capacity = count;
        } else {
            var module = fields[0] == "view-tracker-module";
            if (this.capacity < 0 || fields[1] != (module ? this.modules : this.views).ToString(CultureInfo.InvariantCulture) ||
                lines.Length < 2 || (!module && this.modules != 0)) return false;
            if (!this.identities.Add((lines[0], module))) {
                error = "Duplicate view tracker owner in " + (module ? "module" : "view") + " role: " + lines[0];
                return false;
            }
            var type = Resolve(lines[0]);
            var contract = compilation.GetTypeByMetadataName(module ? "ME.BECS.Views.IViewModule" : "ME.BECS.Views.IView");
            if (type == null || type.IsAbstract || contract == null ||
                !type.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, contract))) {
                error = "Invalid or inaccessible view tracker owner: " + lines[0];
                return false;
            }
            if (this.compilerSelected) {
                if (!module && !ViewSafetySummary.IsView(type, compilation.GetTypeByMetadataName("ME.BECS.Views.EntityView"))) return false;
                var entry = new Entry { Type = type, Module = module, IgnoredSnapshot = lines.Length == 2 && lines[1] == "ignored" };
                if (!entry.IgnoredSnapshot) {
                    foreach (var row in lines.Skip(1)) {
                        var parts = row.Split('\t');
                        if (parts.Length != 3 || !callbackPhases.Contains(parts[1], StringComparer.Ordinal)) return false;
                        if (!entry.Phases.TryGetValue(parts[1], out var phase)) entry.Phases.Add(parts[1], phase = new());
                        if (parts[0] == "S") {
                            if (phase.Origin.Length != 0 || parts[2] is not ("source" or "legacy" or "none" or "il")) return false;
                            phase.Origin = parts[2];
                        } else if (parts[0] == "C") {
                            if (parts[2].Length == 0) return false;
                            if (!ReadComponents(new[] { parts[2] }, phase.Components, allowInaccessible: true)) { error = componentError; return false; }
                        } else return false;
                    }
                    if (entry.Phases.Count != 2 || entry.Phases.Values.Any(phase => phase.Origin.Length == 0 ||
                        phase.Origin != "legacy" && phase.Origin != "il" && phase.Components.Count != 0 ||
                        phase.Components.Distinct(SymbolEqualityComparer.Default).Count() != phase.Components.Count ||
                        phase.Origin == "il" && !phase.Components.Select(JobSafetySummary.ReflectionIdentity).SequenceEqual(
                            phase.Components.Select(JobSafetySummary.ReflectionIdentity).OrderBy(identity => identity, StringComparer.Ordinal)))) return false;
                }
                this.entries.Add(entry);
                if (module) ++this.modules; else ++this.views;
                if (this.entries.Count > this.capacity) return false;
                this.records.Add(string.Join("\t", fields));
                error = "";
                return true;
            }
            var components = new List<INamedTypeSymbol>();
            var ignoreContract = compilation.GetTypeByMetadataName("ME.BECS.Views.IViewIgnoreTracker");
            var ignored = ignoreContract != null && type.AllInterfaces.Contains(ignoreContract, SymbolEqualityComparer.Default);
            if (!ReadComponents(lines.Skip(1), components, allowInaccessible: ignored)) { error = componentError; return false; }
            if (components.Any(component => !this.tracked.Contains(component, SymbolEqualityComparer.Default))) {
                error = "View tracker dependencies are unresolved or absent from the global tracker: " + lines[0];
                return false;
            }
            if (ignored && components.Count > 0) {
                this.HasIgnoredInputDependencies = true;
                this.ignoredComponents.UnionWith(components);
                components.Clear();
            }
            this.entries.Add(new Entry { Type = type, Module = module, Components = components.ToArray() });
            if (module) ++this.modules; else ++this.views;
            if (this.entries.Count > this.capacity) return false;
        }
        this.records.Add(string.Join("\t", fields));
        error = "";
        return true;
    }

    internal bool Validate(InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "";
        if (this.compilerSelected) return this.SelectCurrent(resolver, compilation, out error);
        foreach (var component in this.tracked) {
            if (compilation.IsSymbolAccessibleWithin(component, compilation.Assembly)) continue;
            // Only the compiler-proven ignore contract allows stale private entries
            // to disappear. Unknown/unreferenced private entries are still invalid.
            if (!this.ignoredComponents.Contains(component) || this.entries.Any(entry => entry.Components.Contains(component, SymbolEqualityComparer.Default))) {
                error = "Invalid or inaccessible view tracker component: " + component.ToDisplayString() + ", " + component.ContainingAssembly.Identity;
                return false;
            }
        }
        this.tracked.RemoveAll(component => !compilation.IsSymbolAccessibleWithin(component, compilation.Assembly));
        return true;
    }

    private bool SelectCurrent(InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid view tracking contracts";
        var module = compilation.GetTypeByMetadataName("ME.BECS.Views.IViewModule");
        var ignoreAll = module?.ContainingAssembly.GetTypeByMetadataName("ME.BECS.Views.IViewIgnoreTracker");
        var ignore = module?.ContainingAssembly.GetTypeByMetadataName("ME.BECS.Views.IViewTrackIgnore`1");
        var track = module?.ContainingAssembly.GetTypeByMetadataName("ME.BECS.Views.IViewTrack`1");
        var component = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
        var aspect = compilation.GetTypeByMetadataName("ME.BECS.IAspect");
        if (ignoreAll == null || ignore == null || track == null || component == null || aspect == null) return false;
        foreach (var entry in this.entries) {
            if (entry.Type.AllInterfaces.Contains(ignoreAll, SymbolEqualityComparer.Default)) {
                entry.Components = Array.Empty<INamedTypeSymbol>();
                entry.Phases.Clear();
                foreach (var phase in callbackPhases) entry.Phases.Add(phase, new PhasePlan { Origin = "ignored" });
                continue;
            }
            if (entry.IgnoredSnapshot) {
                error = "View no longer implements IViewIgnoreTracker: " + JobSafetySummary.ReflectionIdentity(entry.Type) + ". Regenerate tracker inputs.";
                return false;
            }
            var ignored = entry.Type.AllInterfaces.Where(type => SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, ignore))
                .Select(type => type.TypeArguments.Single()).ToArray();
            var selected = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var phase in callbackPhases) {
                var plan = entry.Phases[phase];
                // The current compiled IL snapshot is authoritative, including an
                // empty one. Diagnostic source catalogs cannot replace or veto it.
                if (plan.Origin == "il") {
                    foreach (var type in plan.Components) if (!ignored.Contains(type, SymbolEqualityComparer.Default)) selected.Add(type);
                    continue;
                }
                var status = resolver.ViewSafety.Read(entry.Type, entry.Module, phase, resolver, compilation, out var dependencies, out var present, out error);
                if (status == CompilerJobCatalogs.Status.Invalid || status != CompilerJobCatalogs.Status.Complete && plan.Origin != "legacy") {
                    error += ". Recompile source catalogs and regenerate tracker inputs.";
                    return false;
                }
                var current = status == CompilerJobCatalogs.Status.Complete ? dependencies.Select(dependency => dependency.Type) : plan.Components;
                if (status == CompilerJobCatalogs.Status.Complete) plan.Origin = present ? "source" : "none";
                foreach (var type in current) if (!ignored.Contains(type, SymbolEqualityComparer.Default)) selected.Add(type);
            }
            // Explicit opt-in runs after callback exclusions, preserving the existing
            // IViewTrack<T> + IViewTrackIgnore<T> precedence. Aspect filters are source
            // contracts: imported private QueryWith fields must never disappear.
            foreach (var contract in entry.Type.AllInterfaces.Where(type => SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, track))) {
                if (contract.TypeArguments.Single() is not INamedTypeSymbol type || !type.IsUnmanagedType || MethodSummaryType.From(type).IsOpen) {
                    error = "Invalid explicit view tracking argument: " + contract; return false;
                }
                if (!type.AllInterfaces.Contains(aspect, SymbolEqualityComparer.Default)) { selected.Add(type); continue; }
                var filters = new SortedSet<string>(StringComparer.Ordinal);
                var gaps = new SortedSet<string>(StringComparer.Ordinal);
                var operation = new[] { "call", "0", entry.Type.ContainingAssembly.Identity.ToString(), "IViewTrack", "", "!query-filter=aspect", "!query-count=1", "!query-type-0=" + MethodSummaryType.From(type).Encode() };
                SystemQueryFilterContracts.Collect(operation, compilation, new Dictionary<string, MethodSummaryType>(),
                    token => MethodSummaryType.TryDecode(token, out var value) ? value : null, filters, gaps);
                if (gaps.Count != 0) { error = "Incomplete explicit view tracking aspect " + type + ": " + string.Join("; ", gaps); return false; }
                foreach (var filter in filters) {
                    var resolved = resolver.ResolveDefinition(filter.Split('\t')[2], out _);
                    if (resolved == null) { error = "Unresolved explicit view tracking filter: " + filter; return false; }
                    selected.Add(resolved);
                }
            }
            foreach (var type in selected)
                if (!type.IsUnmanagedType || type.IsRefLikeType || MethodSummaryType.From(type).IsOpen || !type.AllInterfaces.Contains(component, SymbolEqualityComparer.Default) ||
                    !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) {
                    error = "Invalid or inaccessible view tracker component: " + JobSafetySummary.ReflectionIdentity(type) + " for " + JobSafetySummary.ReflectionIdentity(entry.Type);
                    return false;
                }
            entry.Components = selected.OrderBy(JobSafetySummary.ReflectionIdentity, StringComparer.Ordinal).ToArray();
        }
        this.tracked.Clear();
        this.tracked.AddRange(this.entries.SelectMany(entry => entry.Components).Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>()
            .OrderBy(JobSafetySummary.ReflectionIdentity, StringComparer.Ordinal));
        error = "";
        return true;
    }

    internal void AppendMetadata(StringBuilder source) {
        foreach (var record in this.records)
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.ViewTrackerInputs.v1\", ")
                .Append(SymbolDisplay.FormatLiteral(record, true)).Append(")]\n");
        if (this.capacity < 0) return;
        void Selected(string kind, int ordinal, string payload) => source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.ViewTrackerSelection.v1\", ")
            .Append(SymbolDisplay.FormatLiteral(kind + "\t" + ordinal.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)), true)).Append(")]\n");
        Selected("view-tracker", 0, "v2\n" + this.capacity.ToString(CultureInfo.InvariantCulture) + "\n" + string.Join("\n", this.tracked.Select(JobSafetySummary.ReflectionIdentity)));
        var viewOrdinal = 0;
        var moduleOrdinal = 0;
        foreach (var entry in this.entries) {
            Selected(entry.Module ? "view-tracker-module" : "view-tracker-view", entry.Module ? moduleOrdinal++ : viewOrdinal++,
                JobSafetySummary.ReflectionIdentity(entry.Type) + "\n" + string.Join("\n", entry.Components.Select(JobSafetySummary.ReflectionIdentity)));
            var origin = "v1\n" + JobSafetySummary.ReflectionIdentity(entry.Type) + "\n" + (entry.Module ? "module" : "view");
            foreach (var phase in callbackPhases) origin += "\n" + phase + "\t" + (entry.Phases.TryGetValue(phase, out var plan) ? plan.Origin : "legacy");
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.ViewTrackerOrigin.v1\", ")
                .Append(SymbolDisplay.FormatLiteral(origin, true)).Append(")]\n");
        }
    }

}
