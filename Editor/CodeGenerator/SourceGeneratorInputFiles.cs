namespace ME.BECS.CodeGeneration {
    // Shared by Unity's publisher and the analyzer. Native additional files are
    // routed by Unity to every compilation using this analyzer, not by asmdef.
    internal static class SourceGeneratorInputFiles {
        internal const string Runtime = "RuntimeInputs.ME.BECS.SourceGenerator.additionalfile";
        internal const string Editor = "EditorInputs.ME.BECS.SourceGenerator.additionalfile";
        internal const string ScopedRuntime = "RuntimeInputs.becs-inputs";
        internal const string ScopedEditor = "EditorInputs.becs-inputs";

        internal static bool IsScoped(string path) {
            var name = System.IO.Path.GetFileName(path.Replace('\\', '/'));
            return name == ScopedRuntime || name == ScopedEditor;
        }

        internal static string CompilationSymbol(string content) => "ME_BECS_INPUT_" + SourceGeneratorNames.Hash(content);

        internal static bool IsNative(string path) {
            var name = System.IO.Path.GetFileName(path.Replace('\\', '/'));
            return name == Runtime || name == Editor;
        }

        internal static bool IsInput(string path) => IsNative(path) || path.EndsWith(".becs-inputs", System.StringComparison.OrdinalIgnoreCase);

        internal static bool TargetsCompilation(string path, string assembly) {
            var name = System.IO.Path.GetFileName(path.Replace('\\', '/'));
            if (name == Runtime || name == ScopedRuntime) return assembly == "ME.BECS.Gen.Runtime";
            if (name == Editor || name == ScopedEditor) return assembly == "ME.BECS.Gen.Editor";
            return path.EndsWith(".becs-inputs", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
