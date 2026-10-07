using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

// Only startup data. The owner need not name a project system/component/job or
// contain an executable feeder body; this can move independently of diagnostics.
internal static class BootstrapPhaseInputEmitter {
    internal static void Append(StringBuilder source, string owner, bool editor, IReadOnlyList<string> initialize,
        IReadOnlyList<string> register, IEnumerable<int> jobSetupOrdinals, bool network, bool views) {
        source.Append("namespace ME.BECS.SourceGenerated { [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\ninternal static class BootstrapPhaseInputs {\n");
        void Callbacks(string field, IEnumerable<string> callbacks) => source.Append("private static readonly global::System.Action<bool>[] ")
            .Append(field).Append(" = new global::System.Action<bool>[] { ").Append(string.Join(",\n", callbacks)).Append(" };\n");
        Callbacks("Initializers", initialize.Select(kind => Initialization(kind) ?? throw new System.InvalidOperationException("Unsupported initialization phase: " + kind)));
        Callbacks("Registrations", register.Select(kind => Registration(kind) ?? throw new System.InvalidOperationException("Unsupported registration phase: " + kind)));
        var preflight = new List<string>();
        if (network) preflight.Add("global::ME.BECS.Network.BootstrapNetworkMethods.RequireComplete");
        if (views) preflight.Add("global::ME.BECS.Views.BootstrapViews.RequireComplete");
        Callbacks("Preflight", preflight);
        source.Append("private static readonly int[] JobSetupOrdinals = new int[] { ")
            .Append(string.Join(",", jobSetupOrdinals.Select(ordinal => ordinal.ToString(CultureInfo.InvariantCulture)))).Append(" };\n")
            .Append("#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS\nprivate const bool JobDebug = true;\n#else\nprivate const bool JobDebug = false;\n#endif\n")
            .Append("public static void Publish() => global::ME.BECS.BootstrapRuntime.InstallPhasePlan(")
            .Append(SymbolDisplay.FormatLiteral(owner, true)).Append(", Initializers, Registrations, Preflight, JobSetupOrdinals, editor: ")
            .Append(editor ? "true" : "false").Append(", jobDebug: JobDebug);\n} }\n");
    }

    private static string? Initialization(string kind) => kind switch {
        "none" => "global::ME.BECS.BootstrapRuntime.NoopPhase",
        "aspects" => "global::ME.BECS.BootstrapRuntime.RegisterInstalledAspects",
        "entities" => "global::ME.BECS.BootstrapRuntime.RegisterInstalledEntities",
        "config-counts" => "global::ME.BECS.BootstrapRuntime.RegisterInstalledConfigCounts",
        "views" => "global::ME.BECS.Views.BootstrapViews.InitializeTrackers",
        "jobs" => "global::ME.BECS.BootstrapRuntime.InitializeInstalledJobs",
        _ => null,
    };
    private static string? Registration(string kind) => kind switch {
        "none" => "global::ME.BECS.BootstrapRuntime.NoopPhase",
        "aspect-construction" => "global::ME.BECS.BootstrapRuntime.RegisterInstalledAspectConstruction",
        "config-callbacks" => "global::ME.BECS.BootstrapRuntime.RegisterInstalledConfigCallbacks",
        "destroy-callbacks" => "global::ME.BECS.BootstrapRuntime.RegisterInstalledDestroyCallbacks",
        "network-methods" => "global::ME.BECS.Network.BootstrapNetworkMethods.RegisterInstalled",
        "view-types" => "global::ME.BECS.Views.BootstrapViews.RegisterInstalledTypes",
        _ => null,
    };
}
