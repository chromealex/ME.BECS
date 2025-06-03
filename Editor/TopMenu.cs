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

    public class ThemesCodeGenerator : CustomCodeGenerator {

        public struct Theme {

            public string menuName;
            public string style;

        }

        public static readonly Theme[] themes = new Theme[] {
            new Theme { menuName = "Default", style = "ME.BECS.Resources/Styles/Themes/Default.uss" },
            new Theme { menuName = "Classic", style = "ME.BECS.Resources/Styles/Themes/Classic.uss" },
            new Theme { menuName = "Alternative", style = "ME.BECS.Resources/Styles/Themes/Alternative.uss" },
        };
        
        public static readonly string DEFAULT = themes[0].style;

        public override FileContent[] AddFileContent(System.Collections.Generic.List<System.Type> references) {

            if (this.editorAssembly == false) return System.Array.Empty<FileContent>();
            
            var customList = new System.Collections.Generic.List<UnityEngine.UIElements.StyleSheet>();
            var guids = AssetDatabase.FindAssets("t:Object ME.BECS.CustomThemes");
            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var styleSheets = AssetDatabase.FindAssets("t:StyleSheet", new string[] { path });
                foreach (var assetGuid in styleSheets) {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(AssetDatabase.GUIDToAssetPath(assetGuid));
                    if (asset != null) {
                        customList.Add(asset);
                    }
                }
            }

            void Add(System.Text.StringBuilder builder, Theme theme, int priority) {
                builder.AppendLine($"[UnityEditor.MenuItem(\"ME.BECS/Themes/{theme.menuName}\", true)] private static bool {EditorUtils.GetCodeName(theme.menuName)}Validation() {{ UnityEditor.Menu.SetChecked(\"ME.BECS/Themes/{theme.menuName}\", Themes.CurrentTheme == \"{theme.style}\"); return true; }}");
                builder.AppendLine($"[UnityEditor.MenuItem(\"ME.BECS/Themes/{theme.menuName}\", priority = {priority})] private static void {EditorUtils.GetCodeName(theme.menuName)}() => Themes.CurrentTheme = \"{theme.style}\";");
            }
            
            var priority = 200;
            var builder = new System.Text.StringBuilder();
            foreach (var theme in themes) {
                Add(builder, theme, priority);
                ++priority;
            }

            priority += 10;
            foreach (var custom in customList) {
                Add(builder, new Theme() {
                    menuName = custom.name,
                    style = AssetDatabase.GetAssetPath(custom),
                }, priority);
                ++priority;
            }
            
            var content = $@"
    public static class ThemesMenu {{
        {builder.ToString()}
    }}
    ";
            
            var file = new FileContent();
            file.filename = "MenuThemes.cs";
            file.content = content;
            
            return new FileContent[] {
                file,
            };
            
        }

    }
    
    public static class Themes {
        
        public static string CurrentTheme {
            get => EditorPrefs.GetString("ME.BECS.Editor.Theme", ThemesCodeGenerator.DEFAULT);
            set {
                EditorPrefs.SetString("ME.BECS.Editor.Theme", value);
                EditorUIUtils.RefreshStyles();
            }
        }

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