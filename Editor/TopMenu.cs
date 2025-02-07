namespace ME.BECS.Editor {

    public static class MainMenu {
        
        [UnityEditor.MenuItem("ME.BECS/Worlds Viewer...")]
        public static void ShowWorldsViewer() {
            
            WorldGraphEditorWindow.ShowWindow();
            
        }

        [UnityEditor.MenuItem("ME.BECS/Regenerate Assemblies")]
        public static void CodeGeneratorRegenerateAsms() {

            CodeGenerator.RegenerateBurstAOT();

        }

        #if ME_BECS_EDITOR_INTERNAL
        [UnityEditor.MenuItem("ME.BECS/Internal/Generate Jobs")]
        public static void CodeGenInternalGenerateJobs() {
            
            CodeGenerator.GenerateComponentsParallelFor();
            
        }
        
        [UnityEditor.MenuItem("ME.BECS/Internal/Print Allocations")]
        public static void PrintAllocations() {
            
            LeakDetector.PrintAllocated();
            
        }
        
        [UnityEditor.MenuItem("ME.BECS/Internal/Generate Fp")]
        public static void GenerateFp() {
            
            FpCodeGenerator.Generate();
            
        }
        #endif

    }

}