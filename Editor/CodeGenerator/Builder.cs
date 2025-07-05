namespace ME.BECS.Editor {

    public class Builder : UnityEditor.Build.BuildPlayerProcessor {
        
        public override void PrepareForBuild(UnityEditor.Build.BuildPlayerContext buildPlayerContext) {
            
            CodeGenerator.RegenerateBurstAOT(true);
            
        }

    }

}