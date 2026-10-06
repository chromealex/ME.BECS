namespace ME.BECS.CodeGeneration {
    // Full snapshot names for the Editor publisher and migration of old scoped inputs.
    // Analyzer publications route their owner-local fragments independently.
    internal static class SourceGeneratorInputFiles {
        internal const string Runtime = "RuntimeInputs.ME.BECS.SourceGenerator.additionalfile";
        internal const string Editor = "EditorInputs.ME.BECS.SourceGenerator.additionalfile";
        internal const string ScopedRuntime = "RuntimeInputs.becs-inputs";
        internal const string ScopedEditor = "EditorInputs.becs-inputs";

    }
}
