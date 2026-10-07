using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

// Unity discovers stylesheet assets; the compiler owns all executable menu code.
internal sealed class ThemeMenuInputEmitter {
    private int expected = -1;
    private int builtInCount;
    private readonly List<(string Name, string Style)> themes = new();
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    internal static bool IsRecord(string kind) => kind is "theme-menu-schema" or "theme-menu";

    internal bool Read(string[] fields, bool editor, out string error) {
        error = "Theme menu inputs are Editor-only";
        if (!editor) return false;
        error = "Invalid theme menu input";
        if (fields.Length != 3) return false;
        string payload;
        try {
            var bytes = Convert.FromBase64String(fields[2]);
            if (Convert.ToBase64String(bytes) != fields[2]) return false;
            payload = new UTF8Encoding(false, true).GetString(bytes);
        } catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
        var lines = payload.Split('\n');
        if (lines.Any(line => string.IsNullOrWhiteSpace(line) || line.Any(char.IsControl))) return false;
        if (fields[0] == "theme-menu-schema") {
            if (this.expected >= 0 || fields[1] != "0" || lines.Length != 3 || lines[0] != "v1" ||
                !Count(lines[1], out var count) || !Count(lines[2], out var builtIns) ||
                count > int.MaxValue - 210 || builtIns > count) return false;
            this.expected = count;
            this.builtInCount = builtIns;
            error = "";
            return true;
        }
        if (fields[0] != "theme-menu" || this.expected < 0 || lines.Length != 2 || this.themes.Count >= this.expected ||
            fields[1] != this.themes.Count.ToString(CultureInfo.InvariantCulture)) return false;
        if (this.names.TryGetValue(lines[0], out var previous)) {
            error = "Duplicate theme menu '" + lines[0] + "': " + previous + " and " + lines[1] + ". Rename one theme.";
            return false;
        }
        if (this.themes.Count > this.builtInCount && StringComparer.Ordinal.Compare(this.themes[this.themes.Count - 1].Name, lines[0]) >= 0) {
            error = "Custom theme menu inputs must be in canonical ordinal name order";
            return false;
        }
        this.names.Add(lines[0], lines[1]);
        this.themes.Add((lines[0], lines[1]));
        error = "";
        return true;
    }

    private static bool Count(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
        value > 0 && text == value.ToString(CultureInfo.InvariantCulture);

    internal bool Validate(Compilation compilation, out string error) {
        error = "";
        if (this.expected < 0) return true; // Old manifests retain their legacy menu until regeneration.
        error = "Incomplete theme menu plan. Regenerate Editor inputs.";
        if (this.themes.Count != this.expected) return false;
        error = "Missing or inaccessible theme menu contract: Themes.CurrentTheme, UnityEditor.MenuItem or UnityEditor.Menu.SetChecked";
        bool Accessible(ISymbol? symbol) => symbol != null && compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly);
        var owner = compilation.GetTypeByMetadataName("ME.BECS.Editor.Themes");
        var properties = owner?.GetMembers("CurrentTheme").OfType<IPropertySymbol>().ToArray();
        var property = properties?.Length == 1 ? properties[0] : null;
        if (!Accessible(owner) || property == null || !property.IsStatic || property.Type.SpecialType != SpecialType.System_String ||
            property.Parameters.Length != 0 || property.ReturnsByRef || property.ReturnsByRefReadonly ||
            !Accessible(property.GetMethod) || !Accessible(property.SetMethod) || property.SetMethod!.IsInitOnly) return false;
        var menu = compilation.GetTypeByMetadataName("UnityEditor.Menu");
        var attribute = compilation.GetTypeByMetadataName("UnityEditor.MenuItem");
        if (!Accessible(menu) || !Accessible(attribute) || attribute!.TypeKind != TypeKind.Class || attribute.IsAbstract ||
            !IsAttribute(attribute, compilation.GetTypeByMetadataName("System.Attribute"))) return false;
        bool Parameters(IMethodSymbol method, params SpecialType[] types) => method.Parameters.Length == types.Length &&
            method.Parameters.Select((parameter, index) => parameter.RefKind == RefKind.None && parameter.Type.SpecialType == types[index]).All(static same => same);
        if (!menu!.GetMembers("SetChecked").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 0 && method.ReturnsVoid &&
                Accessible(method) && Parameters(method, SpecialType.System_String, SpecialType.System_Boolean)) ||
            !attribute!.InstanceConstructors.Any(method => Accessible(method) &&
                Parameters(method, SpecialType.System_String, SpecialType.System_Boolean, SpecialType.System_Int32))) return false;
        error = "";
        return true;
    }

    private static bool IsAttribute(INamedTypeSymbol type, INamedTypeSymbol? attribute) {
        for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            if (SymbolEqualityComparer.Default.Equals(parent, attribute)) return true;
        return false;
    }

    private int Priority(int index) => 200 + index + (index < this.builtInCount ? 0 : 10);

    internal void AppendMetadata(StringBuilder source) {
        if (this.expected < 0) return;
        var payload = new StringBuilder("v1\n").Append(this.expected.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append(this.builtInCount.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < this.themes.Count; ++index)
            payload.Append('\n').Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(this.Priority(index).ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(this.themes[index].Name).Append('\t').Append(this.themes[index].Style);
        source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.ThemeMenuInputs.v1\", ")
            .Append(SymbolDisplay.FormatLiteral(payload.ToString(), true)).Append(")]\n");
    }

    internal void Append(StringBuilder source) {
        if (this.expected < 0) return;
        source.Append("namespace ME.BECS.Editor { public static class ThemesMenu {\n")
            .Append("[global::UnityEditor.MenuItem(").Append(SymbolDisplay.FormatLiteral("ME.BECS/Themes/" + this.themes[0].Name, true))
            .Append(", true, 200)] private static bool ValidateThemes() {\n");
        foreach (var theme in this.themes)
            source.Append("global::UnityEditor.Menu.SetChecked(").Append(SymbolDisplay.FormatLiteral("ME.BECS/Themes/" + theme.Name, true))
                .Append(", global::ME.BECS.Editor.Themes.CurrentTheme == ").Append(SymbolDisplay.FormatLiteral(theme.Style, true)).Append(");\n");
        source.Append("return true;\n}\n");
        for (var index = 0; index < this.themes.Count; ++index) {
            var theme = this.themes[index];
            // Ordinals keep method names bounded and collision-free, even for long or non-identifier asset names.
            source.Append("[global::UnityEditor.MenuItem(").Append(SymbolDisplay.FormatLiteral("ME.BECS/Themes/" + theme.Name, true))
                .Append(", false, ").Append(this.Priority(index).ToString(CultureInfo.InvariantCulture)).Append(")] private static void Select_")
                .Append(index.ToString(CultureInfo.InvariantCulture)).Append("() => global::ME.BECS.Editor.Themes.CurrentTheme = ")
                .Append(SymbolDisplay.FormatLiteral(theme.Style, true)).Append(";\n");
        }
        source.Append("} }\n");
    }
}
