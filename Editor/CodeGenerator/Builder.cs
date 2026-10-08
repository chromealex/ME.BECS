namespace ME.BECS.Editor {

    /// <summary>
    /// Provides Unity Editor controls for builder.
    /// </summary>
    public class Builder : UnityEditor.Build.IPreprocessBuildWithReport {

        /// <summary>
        /// Gets callback order; this implementation returns <c>int.MinValue</c>.
        /// </summary>
        public int callbackOrder => int.MinValue;
        
        /// <summary>
        /// Handles the preprocess build callback.
        /// </summary>
        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report) {
            // Importing/recompiling generated inputs during a player build cannot
            // establish that this build consumes the new bootstrap. Fail before
            // building instead, including after a restart or in batch mode.
            if (!SourceGeneratorInputRefresh.TryValidateReady(out var reason))
                throw new UnityEditor.Build.BuildFailedException("[ME.BECS] " + reason);
            
        }

    }

}
