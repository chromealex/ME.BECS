using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

internal sealed class ViewTypeInputEmitter {
    private int expected = -1;
    private readonly List<(string Identity, INamedTypeSymbol Type, int Flags)> types = new();
    internal IReadOnlyList<(string Identity, INamedTypeSymbol Type, int Flags)> Types => this.types;
    private readonly List<string> callbacks = new();
    private readonly ViewCallbackBodies bodies;
    private List<(IMethodSymbol Slot, int Flag)>? slots;

    internal ViewTypeInputEmitter(Compilation compilation, System.Threading.CancellationToken cancellation) => this.bodies = new(compilation, cancellation);
    internal bool HasSchema => this.expected >= 0;
    internal static bool IsRecord(string kind) => kind is "view-type-schema" or "view-type";

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid view type input";
        if (fields.Length != 3) return false;
        string payload;
        try {
            var bytes = Convert.FromBase64String(fields[2]);
            if (Convert.ToBase64String(bytes) != fields[2]) return false;
            payload = new UTF8Encoding(false, true).GetString(bytes);
        } catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
        if (fields[0] == "view-type-schema") {
            var schema = payload.Split('\n');
            if (this.HasSchema || fields[1] != "0" || schema.Length != 2 || schema[0] != "v1" ||
                !int.TryParse(schema[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 0 ||
                schema[1] != count.ToString(CultureInfo.InvariantCulture)) return false;
            if (!this.ReadSlots(compilation, out error)) return false;
            this.expected = count;
            return true;
        }
        if (fields[0] != "view-type" || !this.HasSchema || this.types.Count >= this.expected || payload.Any(char.IsControl) ||
            fields[1] != this.types.Count.ToString(CultureInfo.InvariantCulture)) return false;
        var type = resolver.ResolveDefinition(payload, out var gap);
        var view = compilation.GetTypeByMetadataName("ME.BECS.Views.EntityView");
        error = "Invalid or inaccessible view registration: " + payload + (gap == null ? "" : " (" + gap + ")");
        if (type == null || type.TypeKind != TypeKind.Class || type.IsAbstract || MethodSummaryType.From(type).IsOpen ||
            !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) || !ViewSafetySummary.IsView(type, view)) return false;
        if (this.types.Count > 0 && StringComparer.Ordinal.Compare(this.types[this.types.Count - 1].Identity, payload) >= 0) {
            error = "Duplicate or non-canonical view type registration: " + payload;
            return false;
        }
        var flags = 0;
        var ordinal = this.types.Count.ToString(CultureInfo.InvariantCulture);
        foreach (var slot in this.slots!) {
            var method = ViewSafetySummary.MostDerived(type, slot.Slot);
            if (method.IsAbstract || !this.bodies.Read(method, out var status)) {
                error = "Invalid view callback body catalog: " + method.ToDisplayString();
                return false;
            }
            if (status == "unknown") {
                error = "Missing source view callback body contract: " + method.ToDisplayString() + " [" + method.ContainingAssembly.Identity +
                    "]. Recompile its owning assembly with the current source generator. Callback flags cannot be guessed because view phases share change trackers.";
                return false;
            }
            if (status != "empty") flags |= slot.Flag;
            this.callbacks.Add("C\t" + ordinal + "\t" + slot.Slot.Name + "\t" + method.ContainingAssembly.Identity + "\t" +
                method.OriginalDefinition.GetDocumentationCommentId() + "\t" + status);
        }
        this.types.Add((payload, type, flags));
        error = "";
        return true;
    }

    private bool ReadSlots(Compilation compilation, out string error) {
        error = "Missing or invalid EntityView callback/TypeFlags contract";
        var view = compilation.GetTypeByMetadataName("ME.BECS.Views.EntityView");
        var flags = compilation.GetTypeByMetadataName("ME.BECS.Views.TypeFlags");
        var data = compilation.GetTypeByMetadataName("ME.BECS.Views.ViewData");
        if (view == null || flags?.EnumUnderlyingType?.SpecialType != SpecialType.System_Byte || data == null) return false;
        var result = new List<(IMethodSymbol, int)>();
        var used = 0;
        foreach (var phase in ViewCallbackBodyGenerator.Phases) {
            var hasData = phase.Method is "OnEnableFromPool" or "ApplyState" or "ApplyStateParallel" or "OnUpdate" or "OnUpdateParallel";
            var hasDelta = phase.Method is "OnUpdate" or "OnUpdateParallel";
            var candidates = view.GetMembers(phase.Method).OfType<IMethodSymbol>().Where(method => !method.IsStatic &&
                (method.IsVirtual || method.IsAbstract) && method.Arity == 0 && method.ReturnsVoid &&
                method.Parameters.Length == (hasDelta ? 2 : hasData ? 1 : 0) &&
                (!hasData || method.Parameters[0].RefKind == RefKind.In && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, data)) &&
                (!hasDelta || method.Parameters[1].RefKind == RefKind.None && method.Parameters[1].Type.SpecialType == SpecialType.System_Single)).ToArray();
            var values = flags.GetMembers(phase.Flag).OfType<IFieldSymbol>().Where(static field => field.HasConstantValue).ToArray();
            if (candidates.Length != 1 || values.Length != 1 || values[0].ConstantValue is not byte value ||
                value == 0 || (value & (value - 1)) != 0 || (used & value) != 0) return false;
            used |= value;
            result.Add((candidates[0], value));
        }
        this.slots = result;
        error = "";
        return true;
    }

    internal bool Validate(int registrationFeeders, ViewTrackerInputEmitter tracker, out string error) {
        error = "Missing, duplicate or incomplete view type registration plan. Regenerate bootstrap and inputs together.";
        if (this.HasSchema ? registrationFeeders != 1 || this.expected != this.types.Count : registrationFeeders != 0) return false;
        foreach (var entry in this.types) if (!tracker.HasView(entry.Type)) {
            error = "Registered view has no view-role tracker entry: " + entry.Identity;
            return false;
        }
        error = "";
        return true;
    }

    internal void AppendMetadata(StringBuilder source) {
        if (!this.HasSchema) return;
        var payload = new StringBuilder("v1\n").Append(this.types.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < this.types.Count; ++index)
            payload.Append("\nT\t").Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(this.types[index].Identity)
                .Append('\t').Append(this.types[index].Flags.ToString(CultureInfo.InvariantCulture));
        foreach (var callback in this.callbacks) payload.Append('\n').Append(callback);
        source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.ViewTypeInputs.v1\", ")
            .Append(SymbolDisplay.FormatLiteral(payload.ToString(), true)).Append(")]\n");
    }


    internal static void AppendRegistration(StringBuilder source, INamedTypeSymbol type, int flags) {
        var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        source.Append("global::ME.BECS.Views.ViewsTypeInfo.RegisterType<").Append(name)
            .Append(">(new global::ME.BECS.Views.ViewTypeInfo { flags = (global::ME.BECS.Views.TypeFlags)")
            .Append(flags.ToString(CultureInfo.InvariantCulture)).Append(", tracker = global::ME.BECS.ViewsTracker.info[global::ME.BECS.ViewsTracker.Tracker<")
            .Append(name).Append(">.id] });\n");
    }
}
