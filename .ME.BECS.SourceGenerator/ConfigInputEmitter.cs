using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

internal static class ConfigInputEmitter {
    internal static void Append(StringBuilder source, ConfigRegistrationOwners owners, bool editor,
        IReadOnlyList<ConfigMaskInputEmitter> masks, IReadOnlyList<string> bodies, IReadOnlyList<(INamedTypeSymbol Type, uint Count)> counts) {
        var profile = editor ? "true" : "false";
        if (owners.Distributed) {
            foreach (var pair in new[] { (Facade: "ConfigCollectionCounts", Phase: "Counts"), (Facade: "ConfigMaskInputs", Phase: "Masks"),
                         (Facade: "ConfigCollectionsInputs", Phase: "Collections") })
                source.Append("namespace ME.BECS.SourceGenerated { internal static class ").Append(pair.Facade)
                    .Append(" { public static void Initialize() => global::ME.BECS.BootstrapRuntime.RegisterInstalledConfig").Append(pair.Phase)
                    .Append("(editor: ").Append(profile).Append("); } }\n");
        } else {
            // One-way upgrade for the already installed snapshot. Typed bodies
            // still compile together here until normal Editor export adds owners.
            ConfigMaskInputEmitter.Append(source, masks);
            ConfigCollectionsInputEmitter.Append(source, bodies);
            source.Append("namespace ME.BECS.SourceGenerated { internal static class ConfigCollectionCounts { public static void Initialize() {\n")
                .Append("global::ME.BECS.StaticTypes.collectionsCount.Resize(global::ME.BECS.StaticTypes.counter + 1u);\n");
            foreach (var entry in counts)
                source.Append("global::ME.BECS.StaticTypes<").Append(entry.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Append(">.SetCollectionsCount(").Append(entry.Count.ToString(CultureInfo.InvariantCulture)).Append("u);\n");
            source.Append("} } }\n");
        }
        source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapConfigSelection { public static void Publish() => ")
            .Append("global::ME.BECS.BootstrapRuntime.ExpectConfigPlan(").Append(SymbolDisplay.FormatLiteral(owners.Plan, true))
            .Append(", ").Append(owners.PhaseCount("Counts").ToString(CultureInfo.InvariantCulture))
            .Append(", ").Append(owners.PhaseCount("Masks").ToString(CultureInfo.InvariantCulture))
            .Append(", ").Append(owners.PhaseCount("Collections").ToString(CultureInfo.InvariantCulture))
            .Append(", editor: ").Append(profile).Append("); } }\n");
    }
}
