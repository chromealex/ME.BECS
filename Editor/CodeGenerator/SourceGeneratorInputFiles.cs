namespace ME.BECS.CodeGeneration {
    // Full snapshot names for the Editor publisher and migration of old scoped inputs.
    // Analyzer publications route their owner-local fragments independently.
    internal static class SourceGeneratorInputFiles {
        // Editor-only state. Deliberately NOT named *.ME.BECS.SourceGenerator.additionalfile:
        // no generator reads the full snapshot, and every Unity compilation the analyzer
        // applies to would otherwise receive (and be invalidated by) tens of megabytes.
        internal const string Runtime = "RuntimeInputs.becs-snapshot";
        internal const string Editor = "EditorInputs.becs-snapshot";
        // Previous compiler-visible names; moved (preserving GUIDs) on the next export.
        internal const string NativeRuntime = "RuntimeInputs.ME.BECS.SourceGenerator.additionalfile";
        internal const string NativeEditor = "EditorInputs.ME.BECS.SourceGenerator.additionalfile";
        internal const string ScopedRuntime = "RuntimeInputs.becs-inputs";
        internal const string ScopedEditor = "EditorInputs.becs-inputs";

    }
}
