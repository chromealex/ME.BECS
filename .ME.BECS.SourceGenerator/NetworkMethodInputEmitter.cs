using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

// Ordered method identities are transport data. Never import executable C# or rely
// on name-only overload binding: validate the current compiler symbols first.
internal sealed class NetworkMethodInputEmitter {
    private int expected = -1;
    private readonly List<(string Owner, IMethodSymbol Method)> methods = new();
    internal IReadOnlyList<(string Owner, IMethodSymbol Method)> Methods => this.methods;
    internal bool HasSchema => this.expected >= 0;
    internal static bool IsRecord(string kind) => kind is "network-method-schema" or "network-method";

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid network method input";
        if (fields.Length != 3) return false;
        string payload;
        try {
            var bytes = Convert.FromBase64String(fields[2]);
            if (Convert.ToBase64String(bytes) != fields[2]) return false;
            payload = new UTF8Encoding(false, true).GetString(bytes);
        } catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
        var lines = payload.Split('\n');
        if (lines.Length != 2 || lines.Any(line => line.Length == 0 || line.Any(char.IsControl))) return false;
        var attribute = compilation.GetTypeByMetadataName("ME.BECS.Network.NetworkMethodAttribute");
        var contract = compilation.GetTypeByMetadataName("ME.BECS.Network.NetworkMethodDelegate")?.DelegateInvokeMethod;
        var inputType = compilation.GetTypeByMetadataName("ME.BECS.Network.InputData");
        var contextType = compilation.GetTypeByMetadataName("ME.BECS.SystemContext");
        error = "Missing or invalid NetworkMethod attribute/delegate/storage contract";
        if (attribute == null || contract == null || !contract.ReturnsVoid || contract.Parameters.Length != 2 ||
            contract.Parameters[0].RefKind != RefKind.In || contract.Parameters[1].RefKind != RefKind.Ref ||
            inputType == null || contextType == null ||
            !SymbolEqualityComparer.Default.Equals(contract.Parameters[0].Type, inputType) ||
            !SymbolEqualityComparer.Default.Equals(contract.Parameters[1].Type, contextType) ||
            compilation.GetTypeByMetadataName("ME.BECS.Network.UnsafeNetworkModule+MethodsStorage") == null) return false;
        error = "Invalid network method input";
        if (fields[0] == "network-method-schema") {
            if (this.HasSchema || fields[1] != "0" || lines[0] != "v1" ||
                !int.TryParse(lines[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                count < 0 || count > ushort.MaxValue || lines[1] != count.ToString(CultureInfo.InvariantCulture)) return false;
            this.expected = count;
            error = "";
            return true;
        }
        if (fields[0] != "network-method" || !this.HasSchema || this.methods.Count >= this.expected ||
            fields[1] != this.methods.Count.ToString(CultureInfo.InvariantCulture)) return false;
        var owner = resolver.ResolveDefinition(lines[0], out var gap);
        error = "Invalid or inaccessible network method owner: " + lines[0] + (gap == null ? "" : " (" + gap + ")");
        if (owner == null || MethodSummaryType.From(owner).IsOpen || owner.IsRefLikeType ||
            !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly)) return false;
        var candidates = owner.GetMembers(lines[1]).OfType<IMethodSymbol>().Where(method =>
            method.GetAttributes().Any(item => SymbolEqualityComparer.Default.Equals(item.AttributeClass, attribute))).ToArray();
        error = "Network method must be one accessible static non-generic void method matching NetworkMethodDelegate: " +
            lines[0] + " / " + lines[1];
        if (candidates.Length != 1) return false;
        var target = candidates[0];
        if (!SyntaxFacts.IsValidIdentifier(target.Name) && SyntaxFacts.GetKeywordKind(target.Name) == SyntaxKind.None) return false;
        if (target.MethodKind != MethodKind.Ordinary || !target.IsStatic || target.IsAbstract || target.IsVirtual ||
            target.IsVararg || target.Arity != 0 || !target.ReturnsVoid || target.ReturnsByRef || target.ReturnsByRefReadonly ||
            !compilation.IsSymbolAccessibleWithin(target, compilation.Assembly) ||
            (target.IsPartialDefinition && target.PartialImplementationPart == null) ||
            target.Parameters.Length != contract.Parameters.Length ||
            !target.Parameters.Zip(contract.Parameters, (actual, expected) => actual.RefKind == expected.RefKind &&
                SymbolEqualityComparer.Default.Equals(actual.Type, expected.Type)).All(static same => same)) return false;
        if (this.methods.Count > 0) {
            var previous = this.methods[this.methods.Count - 1];
            var order = StringComparer.Ordinal.Compare(previous.Method.Name, target.Name);
            if (order > 0 || (order == 0 && StringComparer.Ordinal.Compare(previous.Owner, lines[0]) >= 0)) {
                error = "Duplicate or non-canonical network method order: " + lines[0] + " / " + lines[1];
                return false;
            }
        }
        this.methods.Add((lines[0], target));
        error = "";
        return true;
    }

    internal bool Validate(int registrationFeeders, out string error) {
        error = "Missing, duplicate or incomplete network method registration plan. Regenerate bootstrap and inputs together.";
        if (this.HasSchema ? registrationFeeders != 1 || this.expected != this.methods.Count : registrationFeeders != 0) return false;
        error = "";
        return true;
    }

    internal void AppendMetadata(StringBuilder source) {
        if (!this.HasSchema) return;
        var payload = new StringBuilder("v1\n").Append(this.methods.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < this.methods.Count; ++index)
            payload.Append('\n').Append((index + 1).ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(this.methods[index].Owner).Append('\t').Append(this.methods[index].Method.Name);
        source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.NetworkMethodInputs.v1\", ")
            .Append(SymbolDisplay.FormatLiteral(payload.ToString(), true)).Append(")]\n");
    }

    internal void Append(StringBuilder source, NetworkRegistrationOwners owners, bool editor) {
        var profile = editor ? "true" : "false";
        source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapNetworkSelection { public static void Publish() {");
        if (this.HasSchema && owners.Distributed)
            source.Append("global::ME.BECS.Network.BootstrapNetworkMethods.ExpectPlan(").Append(SymbolDisplay.FormatLiteral(owners.Plan, true))
                .Append(", ").Append(owners.Count.ToString(CultureInfo.InvariantCulture)).Append(", editor: ").Append(profile).Append(");");
        source.Append("} public static void Validate() {");
        if (this.HasSchema && owners.Distributed)
            source.Append("global::ME.BECS.Network.BootstrapNetworkMethods.RequireComplete(editor: ").Append(profile).Append(");");
        source.Append("} } }\n");
        if (!this.HasSchema) return;
        if (owners.Distributed) {
            source.Append("namespace ME.BECS.SourceGenerated { internal static class NetworkMethodInputs {\n")
                .Append("[global::UnityEngine.Scripting.PreserveAttribute] public static void Initialize() => ")
                .Append("global::ME.BECS.Network.BootstrapNetworkMethods.RegisterInstalled(editor: ").Append(profile).Append(");\n} }\n");
            return;
        }
        // Transitional old snapshot: normal Editor export supplies owner rows.
        source.Append("namespace ME.BECS.SourceGenerated { internal static class NetworkMethodInputs {\n")
            .Append("[global::UnityEngine.Scripting.PreserveAttribute] public static void Initialize() {\n")
            .Append("global::ME.BECS.WorldStaticCallbacks.RegisterCallback<global::ME.BECS.Network.UnsafeNetworkModule.MethodsStorage>(Register);\n}\n")
            .Append("private static void Register(ref global::ME.BECS.Network.UnsafeNetworkModule.MethodsStorage methods) {\n");
        foreach (var entry in this.methods)
            source.Append("methods.Add((global::ME.BECS.Network.NetworkMethodDelegate)")
                .Append(entry.Method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append(".@").Append(entry.Method.Name).Append(");\n");
        source.Append("}\n} }\n");
    }
}
