namespace ME.BECS.Tests {
    // Only source-analysis oracle tests opt out. IL/runtime/registration tests
    // still run with diagnostics disabled; missing metadata in opt-in mode fails
    // normally instead of being mistaken for a disabled diagnostic feature.
    public static class SourceAnalysisTests {
        public static void Require() {
            #if !BECS_SOURCE_ANALYSIS_DIAGNOSTICS
            NUnit.Framework.Assert.Ignore("Optional source-analysis diagnostics are disabled. Enable BECS_SOURCE_ANALYSIS_DIAGNOSTICS to run these comparison tests.");
            #endif
        }
    }
}
