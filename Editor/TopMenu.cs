namespace ME.BECS.Editor {
    
    using UnityEditor;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections;

    public static class MainMenu {
        
        [MenuItem("ME.BECS/\u2630 Worlds Viewer...", priority = 10000)]
        public static void ShowWorldsViewer() {
            
            WorldGraphEditorWindow.ShowWindow();
            
        }

        [MenuItem("ME.BECS/Regenerate Assemblies", priority = 100)]
        public static void CodeGeneratorRegenerateAsms() {

            CodeGenerator.RegenerateBurstAOT();

        }

        #if ME_BECS_EDITOR_INTERNAL
        [MenuItem("ME.BECS/Internal/Generate Jobs", priority = 0)]
        public static void CodeGenInternalGenerateJobs() {
            
            CodeGenerator.GenerateComponentsParallelFor();
            
        }
        
        [MenuItem("ME.BECS/Internal/Print Allocations", priority = 0)]
        public static void PrintAllocations() {
            
            LeakDetector.PrintAllocated();
            
        }
        
        [MenuItem("ME.BECS/Internal/Generate Fp", priority = 0)]
        public static void GenerateFp() {
            
            FpCodeGenerator.Generate();
            
        }
        #endif

    }
    
    public static class ThreadingToggle {

        private const string MENU_NAME = "ME.BECS/Jobs/Enable Multithreading";
        private const string MENU_NAME_ONE_THREAD = "ME.BECS/Jobs/Enable Multithreading (1 Thread)";
        
        [UnityEditor.MenuItem(MENU_NAME, true)]
        private static bool IsMultiThreadingEnabled() {
            UnityEditor.Menu.SetChecked(MENU_NAME, JobsUtility.JobWorkerCount != 0);
            return true;
        }

        [UnityEditor.MenuItem(MENU_NAME, priority = 100)]
        private static void ToggleMultiThreading() {
            if (JobsUtility.JobWorkerCount != 0) {
                JobsUtility.JobWorkerCount = 0;
            } else {
                JobsUtility.ResetJobWorkerCount();
            }
        }

        [UnityEditor.MenuItem(MENU_NAME_ONE_THREAD, true)]
        private static bool IsMultiThreadingEnabledOneThread() {
            UnityEditor.Menu.SetChecked(MENU_NAME_ONE_THREAD, JobsUtility.JobWorkerCount == 1);
            return true;
        }

        [UnityEditor.MenuItem(MENU_NAME_ONE_THREAD, priority = 100)]
        private static void EnableOneThread() {
            if (JobsUtility.JobWorkerCount != 1) JobsUtility.JobWorkerCount = 1;
        }

    }

    public static class LeakDetection {

        private const string LEAK_OFF = "ME.BECS/Jobs/Leak Detection Off";
        private const string LEAK_ON = "ME.BECS/Jobs/Leak Detection On";
        private const string LEAK_DETECTION_FULL = "ME.BECS/Jobs/Leak Detection Full Stack Traces (Expensive)";

        [MenuItem(LeakDetection.LEAK_OFF, priority = 201)]
        private static void SwitchLeaksOff() {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
        }

        [MenuItem(LeakDetection.LEAK_ON, priority = 202)]
        private static void SwitchLeaksOn() {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.Enabled;
        }

        [MenuItem(LeakDetection.LEAK_DETECTION_FULL, priority = 203)]
        private static void SwitchLeaksFull() {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
        }

        [MenuItem(LeakDetection.LEAK_OFF, true)]
        private static bool SwitchLeaksOffValidate() {
            Menu.SetChecked(LeakDetection.LEAK_OFF, NativeLeakDetection.Mode == NativeLeakDetectionMode.Disabled);
            Menu.SetChecked(LeakDetection.LEAK_ON, NativeLeakDetection.Mode == NativeLeakDetectionMode.Enabled);
            Menu.SetChecked(LeakDetection.LEAK_DETECTION_FULL, NativeLeakDetection.Mode == NativeLeakDetectionMode.EnabledWithStackTrace);
            return true;
        }

    }

}