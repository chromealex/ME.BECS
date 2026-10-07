namespace ME.BECS.Editor {

    public class Builder : UnityEditor.Build.IPreprocessBuildWithReport {

        public int callbackOrder => int.MinValue;
        
        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report) {
            // Importing/recompiling generated inputs during a player build cannot
            // establish that this build consumes the new bootstrap. Fail before
            // building instead, including after a restart or in batch mode.
            if (!SourceGeneratorInputRefresh.TryValidateReady(out var reason))
                throw new UnityEditor.Build.BuildFailedException("[ME.BECS] " + reason);
            
        }

    }

}
