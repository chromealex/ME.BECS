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
    private readonly List<INamedTypeSymbol> tracked = new();
    private readonly List<(INamedTypeSymbol Type, bool Module, INamedTypeSymbol[] Components)> entries = new();
    private readonly HashSet<string> identities = new(StringComparer.Ordinal);
    private int views;
    private int modules;
    private readonly List<string> records = new();

    internal static bool IsRecord(string kind) => kind is "view-tracker" or "view-tracker-view" or "view-tracker-module";

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid view tracker input";
        if (fields.Length != 3) return false;
        string payload;
        try { payload = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])); }
        catch (FormatException) { return false; }
        var lines = payload.Split('\n');
        if (lines.Any(line => line.Any(char.IsControl))) return false;
        INamedTypeSymbol? Resolve(string identity) {
            var type = resolver.ResolveDefinition(identity, out _);
            return type != null && !MethodSummaryType.From(type).IsOpen &&
                compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) ? type : null;
        }
        bool ReadComponents(IEnumerable<string> names, List<INamedTypeSymbol> result) {
            var values = names.ToArray();
            if (values.Length == 1 && values[0].Length == 0) return true;
            foreach (var identity in values) {
                var type = Resolve(identity);
                var contract = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
                if (type == null || !type.IsUnmanagedType || type.IsRefLikeType || contract == null ||
                    !type.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, contract))) return false;
                result.Add(type);
            }
            return true;
        }
        if (fields[0] == "view-tracker") {
            if (this.capacity >= 0 || fields[1] != "0" || lines.Length < 3 || (lines[0] != "v1" && lines[0] != "v2") ||
                !int.TryParse(lines[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 0 || count == int.MaxValue ||
                lines[1] != count.ToString(CultureInfo.InvariantCulture) || !ReadComponents(lines.Skip(2), this.tracked)) return false;
            if (this.tracked.Distinct(SymbolEqualityComparer.Default).Count() != this.tracked.Count) return false;
            if (lines[0] == "v2" && !lines.Skip(2).SequenceEqual(lines.Skip(2).OrderBy(static identity => identity, StringComparer.Ordinal))) {
                error = "View tracker v2 component identities must be in canonical ordinal order";
                return false;
            }
            this.capacity = count;
        } else {
            var module = fields[0] == "view-tracker-module";
            if (this.capacity < 0 || fields[1] != (module ? this.modules : this.views).ToString(CultureInfo.InvariantCulture) ||
                lines.Length < 2 || (!module && this.modules != 0) || !this.identities.Add(lines[0])) return false;
            var type = Resolve(lines[0]);
            var contract = compilation.GetTypeByMetadataName(module ? "ME.BECS.Views.IViewModule" : "ME.BECS.Views.IView");
            if (type == null || type.IsAbstract || contract == null ||
                !type.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, contract))) {
                error = "Invalid or inaccessible view tracker owner: " + lines[0];
                return false;
            }
            var components = new List<INamedTypeSymbol>();
            if (!ReadComponents(lines.Skip(1), components) ||
                components.Any(component => !this.tracked.Contains(component, SymbolEqualityComparer.Default))) {
                error = "View tracker dependencies are unresolved or absent from the global tracker: " + lines[0];
                return false;
            }
            this.entries.Add((type, module, components.ToArray()));
            if (module) ++this.modules; else ++this.views;
            if (this.entries.Count > this.capacity) return false;
        }
        this.records.Add(string.Join("\t", fields));
        error = "";
        return true;
    }

    internal void AppendMetadata(StringBuilder source) {
        foreach (var record in this.records)
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.ViewTrackerInputs.v1\", ")
                .Append(SymbolDisplay.FormatLiteral(record, true)).Append(")]\n");
    }

    internal void Append(StringBuilder source) {
        if (this.capacity < 0) return; // Views addon is optional; old manifests have no schema.
        string Name(INamedTypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        source.Append("namespace ME.BECS.SourceGenerated { internal static class ViewTrackerInputs {\n")
            .Append("[global::UnityEngine.Scripting.PreserveAttribute] public static void Initialize() {\n")
            .Append("global::ME.BECS.StaticTypes.SetTracker(").Append(this.tracked.Count.ToString(CultureInfo.InvariantCulture)).Append("u);\n");
        foreach (var type in this.tracked)
            source.Append("global::ME.BECS.StaticTypes<").Append(Name(type)).Append(">.TrackVersion();\n");
        source.Append("global::ME.BECS.ViewsTracker.SetTracker(").Append(this.capacity.ToString(CultureInfo.InvariantCulture)).Append("u);\n");
        foreach (var entry in this.entries) {
            source.Append("{ var info = new global::ME.BECS.ViewsTracker.ViewInfo();\n");
            if (entry.Components.Length != 0)
                source.Append("info.tracker.Resize(").Append(entry.Components.Length.ToString(CultureInfo.InvariantCulture)).Append("u);\n");
            for (var i = 0; i < entry.Components.Length; ++i)
                source.Append("info.tracker.Get(").Append(i.ToString(CultureInfo.InvariantCulture)).Append("u) = global::ME.BECS.StaticTypes<")
                    .Append(Name(entry.Components[i])).Append(">.trackerIndex;\n");
            source.Append("global::ME.BECS.ViewsTracker.").Append(entry.Module ? "TrackViewModule" : "TrackView")
                .Append('<').Append(Name(entry.Type)).Append(">(info); }\n");
        }
        source.Append("} } }\n");
    }
}
