using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

internal static class AspectInputEmitter {
    internal static void Append(StringBuilder source, AspectRegistrationOwners owners,
                                IReadOnlyList<(INamedTypeSymbol Type, IMethodSymbol? Query)> registrations,
                                IReadOnlyList<IMethodSymbol> constructors, bool editor, string target) {
        var profile = editor ? "true" : "false";
        source.Append("namespace ME.BECS.SourceGenerated { internal static class AspectInputs {\n")
            .Append("public static void Initialize() => global::ME.BECS.BootstrapRuntime.RegisterInstalledAspects(editor: ").Append(profile).Append(");\n")
            .Append("public static void RegisterConstruction() => global::ME.BECS.BootstrapRuntime.RegisterInstalledAspectConstruction(editor: ").Append(profile).Append(");\n");
        if (!owners.Distributed) {
            // One-way upgrade for old snapshots: preserve their two complete,
            // ordered phases until the Editor can publish per-owner inputs.
            source.Append("internal static void InitializeLegacy() {\n");
            foreach (var registration in registrations) {
                source.Append("global::ME.BECS.AspectTypeInfo<").Append(registration.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Append(">.Validate();\n").Append(registration.Query!.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Append('.').Append(registration.Query.Name).Append("();\n");
            }
            source.Append("}\ninternal static void ConstructLegacy(ref global::ME.BECS.World world) {\n");
            foreach (var constructor in constructors) source.Append(constructor.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append('.').Append(constructor.Name).Append("(ref world);\n");
            source.Append("}\n");
        }
        source.Append("} internal static class BootstrapAspectSelection {\n");
        if (owners.Distributed) source.Append("public static void Publish() => global::ME.BECS.BootstrapRuntime.ExpectAspectPlan(")
            .Append(SymbolDisplay.FormatLiteral(owners.Plan, true)).Append(", ").Append(owners.Count.ToString(CultureInfo.InvariantCulture))
            .Append(", editor: ").Append(profile).Append(");\n");
        else {
            var identity = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash("aspect-upgrade\n" + target + "\n" +
                string.Join("\n", registrations.Select(item => item.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) + "\n" +
                string.Join("\n", constructors.Select(item => item.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));
            source.Append("private static readonly int[] Ordinals = new int[] { 0 };\n")
                .Append("private static readonly global::System.Action[] Callbacks = new global::System.Action[] { AspectInputs.InitializeLegacy };\n")
                .Append("private static readonly global::ME.BECS.WorldStaticCallbacks.CallbackDelegate<global::ME.BECS.World>[] Constructors = new global::ME.BECS.WorldStaticCallbacks.CallbackDelegate<global::ME.BECS.World>[] { AspectInputs.ConstructLegacy };\n")
                .Append("public static void Publish() => global::ME.BECS.BootstrapRuntime.InstallAspectFragment(")
                .Append(SymbolDisplay.FormatLiteral(identity, true)).Append(", ").Append(SymbolDisplay.FormatLiteral(target, true))
                .Append(", 1, Ordinals, Callbacks, Constructors, editor: ").Append(profile).Append(");\n");
        }
        source.Append("} }\n");
    }
}
