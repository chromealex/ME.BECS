namespace ME.BECS.Editor {
    
    using UnityEditor;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections;
    using System.Linq;

    /// <summary>
    /// Defines main menu state and operations.
    /// </summary>
    public static class MainMenu {
        
        /// <summary>
        /// Shows worlds viewer.
        /// </summary>
        [MenuItem("ME.BECS/\u2630 Worlds Viewer...", priority = 10000)]
        public static void ShowWorldsViewer() {
            
            WorldGraphEditorWindow.ShowWindow();
            
        }

        /// <summary>
        /// Shows quick start.
        /// </summary>
        [MenuItem("ME.BECS/✪ Quick Start...", priority = 300)]
        public static void ShowQuickStart() {
            
            QuickStartEditorWindow.ShowWindow();
            
        }

        #if ME_BECS_EDITOR_INTERNAL
        /// <summary>
        /// Clears allocations state.
        /// </summary>
        [MenuItem("ME.BECS/Internal/Clear Allocations State", priority = 0)]
        public static void ClearAllocationsState() {
            
            LeakDetector.ClearAllocated();
            
        }

        /// <summary>
        /// Prints allocations all.
        /// </summary>
        [MenuItem("ME.BECS/Internal/Print Allocations (All)", priority = 0)]
        public static void PrintAllocationsAll() {
            
            LeakDetector.PrintAllocated(Allocator.None);
            
        }

        /// <summary>
        /// Prints allocations persistent.
        /// </summary>
        [MenuItem("ME.BECS/Internal/Print Allocations (Persistent)", priority = 0)]
        public static void PrintAllocationsPersistent() {
            
            LeakDetector.PrintAllocated(Allocator.Persistent);
            
        }

        /// <summary>
        /// Prints allocations domain.
        /// </summary>
        [MenuItem("ME.BECS/Internal/Print Allocations (Domain)", priority = 0)]
        public static void PrintAllocationsDomain() {
            
            LeakDetector.PrintAllocated(Allocator.Domain);
            
        }

        /// <summary>
        /// Generates fp.
        /// </summary>
        [MenuItem("ME.BECS/Internal/Generate Fp", priority = 0)]
        public static void GenerateFp() {
            
            FpCodeGenerator.Generate();
            
        }
        #endif

    }

    /// <summary>
    /// Defines code generator menu state and operations.
    /// </summary>
    public static class CodeGeneratorMenu {

        private const string MENU_NAME = "ME.BECS/Source Generator/Rebuild Inputs (Full Analysis)";
        
        // Compatibility for external callers. Asset/code refresh is mandatory;
        // the old optional auto-codegen preference must not leave stale inputs.
        /// <summary>
        /// Gets is enabled auto; this implementation returns <c>true</c>.
        /// </summary>
        public static bool IsEnabledAuto => true;
        
        [MenuItem(MENU_NAME, priority = 100)]
        private static void Run() {
            
            SourceGeneratorInputRefresh.RequestExport(rebuild: true);

        }

    }

    /// <summary>
    /// Exports themes registration data for generated code.
    /// </summary>
    public class ThemesCodeGenerator : CustomCodeGenerator {

        /// <summary>
        /// Defines theme state and operations for <c>ThemesCodeGenerator</c>.
        /// </summary>
        public struct Theme {

            /// <summary>
            /// Menu name used by <c>ThemesCodeGenerator.Theme</c>.
            /// </summary>
            public string menuName;
            /// <summary>
            /// Style used by <c>ThemesCodeGenerator.Theme</c>.
            /// </summary>
            public string style;

        }

        /// <summary>
        /// Themes used by <c>ThemesCodeGenerator</c>.
        /// </summary>
        public static readonly Theme[] themes = new Theme[] {
            new Theme { menuName = "Default", style = "ME.BECS.Resources/Styles/Themes/Default.uss" },
            new Theme { menuName = "Classic", style = "ME.BECS.Resources/Styles/Themes/Classic.uss" },
            new Theme { menuName = "Alternative", style = "ME.BECS.Resources/Styles/Themes/Alternative.uss" },
        };
        
        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static readonly string DEFAULT = themes[0].style;

        private Theme[] collected;
        private int builtInCount;

        /// <summary>
        /// Adds this feature's registration inputs to the source-generator export.
        /// </summary>
        public override void AppendSourceGeneratorInputs(System.Text.StringBuilder manifest) {
            // Inherited compatibility hooks must not export the global menu twice.
            if (!this.editorAssembly || this.GetType() != typeof(ThemesCodeGenerator)) return;
            var plan = this.Collect();
            void Append(string kind, int ordinal, string payload) => manifest.Append(kind).Append('\t')
                .Append(ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload))).Append('\n');
            Append("theme-menu-schema", 0, "v1\n" + plan.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                this.builtInCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (var index = 0; index < plan.Length; ++index)
                Append("theme-menu", index, plan[index].menuName + "\n" + plan[index].style);
        }

        /// <summary>
        /// Adds the assembly references required by this feature's generated code.
        /// </summary>
        public override void AddSourceGeneratorReferences(System.Collections.Generic.List<System.Type> references) {
            if (this.editorAssembly && this.GetType() == typeof(ThemesCodeGenerator)) references.Add(typeof(Themes));
        }

        /// <summary>
        /// Returns retired source files.
        /// </summary>
        public override System.Collections.Generic.IEnumerable<string> GetRetiredSourceFiles() =>
            this.editorAssembly ? new[] { "MenuThemes" } : System.Array.Empty<string>();

        private Theme[] Collect() {
            if (this.collected != null) return this.collected;
            var custom = new System.Collections.Generic.Dictionary<string, Theme>(System.StringComparer.Ordinal);
            var guids = AssetDatabase.FindAssets("t:Object ME.BECS.CustomThemes");
            foreach (var guid in guids.OrderBy(value => value, System.StringComparer.Ordinal)) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path)) continue;
                var styleSheets = AssetDatabase.FindAssets("t:StyleSheet", new string[] { path });
                foreach (var assetGuid in styleSheets) {
                    var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                    if (custom.ContainsKey(assetPath)) continue;
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(assetPath);
                    if (asset != null) custom.Add(assetPath, new Theme { menuName = asset.name, style = assetPath });
                }
            }
            var builtIns = themes.ToArray();
            if (builtIns.Length == 0) throw new System.InvalidOperationException("The theme menu requires a default built-in theme.");
            var plan = builtIns.Concat(custom.Values.OrderBy(theme => theme.menuName, System.StringComparer.Ordinal)
                .ThenBy(theme => theme.style, System.StringComparer.Ordinal)).ToArray();
            var names = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var theme in plan) {
                if (string.IsNullOrWhiteSpace(theme.menuName) || string.IsNullOrWhiteSpace(theme.style) ||
                    theme.menuName.Any(char.IsControl) || theme.style.Any(char.IsControl))
                    throw new System.InvalidOperationException("Invalid theme menu name or stylesheet path: " + theme.style);
                if (names.TryGetValue(theme.menuName, out var previous))
                    throw new System.InvalidOperationException("Duplicate theme menu '" + theme.menuName + "': " + previous + " and " + theme.style + ". Rename one theme.");
                names.Add(theme.menuName, theme.style);
            }
            this.builtInCount = builtIns.Length;
            return this.collected = plan;
        }

    }
    
    /// <summary>
    /// Defines themes state and operations.
    /// </summary>
    public static class Themes {

        /// <summary>
        /// Raised when changed is reported by this API.
        /// </summary>
        public static event System.Action Changed;
        
        /// <summary>
        /// Current theme used by <c>Themes</c>.
        /// </summary>
        public static string CurrentTheme {
            get => EditorPrefs.GetString("ME.BECS.Editor.Theme", ThemesCodeGenerator.DEFAULT);
            set {
                EditorPrefs.SetString("ME.BECS.Editor.Theme", value);
                EditorUIUtils.RefreshStyles();
                Changed?.Invoke();
            }
        }

    }
    
    /// <summary>
    /// Defines threading toggle state and operations.
    /// </summary>
    public static class ThreadingToggle {

        private const string MENU_NAME = "ME.BECS/Jobs/Enable Multithreading";
        private const string MENU_NAME_ONE_THREAD = "ME.BECS/Jobs/Enable Multithreading (1 Thread)";
        
        [MenuItem(MENU_NAME, priority = 100)]
        private static void MultithreadingOn() {
            if (Menu.GetChecked(MENU_NAME) == false) {
                JobsUtility.ResetJobWorkerCount();
            } else {
                JobsUtility.JobWorkerCount = 0;
            }
        }

        [MenuItem(MENU_NAME_ONE_THREAD, priority = 101)]
        private static void MultithreadingSingleOn() {
            if (Menu.GetChecked(MENU_NAME) == false) {
                JobsUtility.JobWorkerCount = 1;
            } else {
                JobsUtility.ResetJobWorkerCount();
            }
        }

        [MenuItem(MENU_NAME, true)]
        private static bool SwitchOffValidate() {
            Menu.SetChecked(MENU_NAME, JobsUtility.JobWorkerCount != 0);
            Menu.SetChecked(MENU_NAME_ONE_THREAD, JobsUtility.JobWorkerCount == 1);
            return true;
        }
        
    }

    /// <summary>
    /// Configures native-allocation leak diagnostics.
    /// </summary>
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
