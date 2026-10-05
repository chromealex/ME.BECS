namespace ME.BECS.Editor {
    internal static class SourceAnalysisDiagnostics {
        internal static bool BeginComparison() {
            #if BECS_SOURCE_ANALYSIS_DIAGNOSTICS
            return true;
            #else
            UnityEngine.Debug.Log("[ME.BECS] Optional source-analysis comparisons are disabled. " +
                "Enable BECS_SOURCE_ANALYSIS_DIAGNOSTICS across the participating assemblies only if you want these reports. " +
                "Normal generation and runtime use IL analysis and do not require this diagnostic mode.");
            return false;
            #endif
        }
    }
}
