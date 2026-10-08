namespace ME.BECS.Editor.CreateProject {
    
    using UnityEditor;
    using UnityEngine;
    using System.Linq;
    using scg = System.Collections.Generic;

    /// <summary>
    /// Defines package import state and operations.
    /// </summary>
    public struct PackageImport {

        /// <summary>
        /// Selected execution or presentation mode.
        /// </summary>
        public TemplateJson.ModeJson mode;
        /// <summary>
        /// Target path used by <c>PackageImport</c>.
        /// </summary>
        public string targetPath;
        /// <summary>
        /// Project name used by <c>PackageImport</c>.
        /// </summary>
        public string projectName;
        /// <summary>
        /// Options used by <c>PackageImport</c>.
        /// </summary>
        public OptionJson[] options;

    }
    
    /// <summary>
    /// Defines the supported mode values.
    /// </summary>
    public enum Mode {
        /// <summary>
        /// Single player option for <c>Mode</c>.
        /// </summary>
        SinglePlayer = 0,
        /// <summary>
        /// Multiplayer option for <c>Mode</c>.
        /// </summary>
        Multiplayer  = 1,
    }

    /// <summary>
    /// Stores option info for the associated editor API.
    /// </summary>
    public struct OptionInfo {

        /// <summary>
        /// Text displayed as the entry's caption.
        /// </summary>
        public string caption;
        /// <summary>
        /// Human-readable explanation of the associated entry.
        /// </summary>
        public string description;
        /// <summary>
        /// Callback invoked for action.
        /// </summary>
        public System.Action<bool> action;
        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public bool state;

    }

    /// <summary>
    /// Stores template info for the associated editor API.
    /// </summary>
    public struct TemplateInfo {

        /// <summary>
        /// Icon used by <c>TemplateInfo</c>.
        /// </summary>
        public Texture icon;
        /// <summary>
        /// Text displayed as the entry's caption.
        /// </summary>
        public string caption;
        /// <summary>
        /// Human-readable explanation of the associated entry.
        /// </summary>
        public string description;
        /// <summary>
        /// Template used by <c>TemplateInfo</c>.
        /// </summary>
        public string template;
        /// <summary>
        /// Supported modes for the associated operation.
        /// </summary>
        public TemplateInfoJson.ModeJson[] modes;
        /// <summary>
        /// Selected execution or presentation mode.
        /// </summary>
        public Mode mode;
        /// <summary>
        /// Template path used by <c>TemplateInfo</c>.
        /// </summary>
        public string templatePath;

        /// <summary>
        /// Initializes <c>TemplateInfo</c> from the supplied path, JSON.
        /// </summary>
        public TemplateInfo(string path, TemplateInfoJson json) {
            this.caption = json.caption;
            this.description = json.description;
            this.templatePath = System.IO.Path.GetDirectoryName(path);
            this.icon = EditorUtils.LoadResource<Texture2D>($"{this.templatePath}/icon.png", isRequired: false);
            if (this.icon == null) this.icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/Other.png");
            this.template = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetDirectoryName(path));
            this.modes = json.modes;
            this.mode = default;
        }

        /// <summary>
        /// Returns modes.
        /// </summary>
        public scg::List<TemplateInfo> GetModes(TemplateInfo[] allModes) {
            var modes = this.modes;
            var templatePath = this.templatePath;
            return allModes.Where(x => {
                foreach (var m in modes) {
                    if (System.IO.File.Exists($"{templatePath}/{m.package}") == true && m.mode == (int)x.mode) {
                        return true;
                    }
                }
                return false;
            }).ToList();
        }
        
    }

    /// <summary>
    /// Defines template info JSON state and operations.
    /// </summary>
    public struct TemplateInfoJson {

        /// <summary>
        /// Defines mode JSON state and operations for <c>TemplateInfoJson</c>.
        /// </summary>
        [System.Serializable]
        public struct ModeJson {

            /// <summary>
            /// Selected execution or presentation mode.
            /// </summary>
            public int mode;
            /// <summary>
            /// Package used by <c>TemplateInfoJson.ModeJson</c>.
            /// </summary>
            public string package;

        }

        /// <summary>
        /// Text displayed as the entry's caption.
        /// </summary>
        public string caption;
        /// <summary>
        /// Human-readable explanation of the associated entry.
        /// </summary>
        public string description;
        /// <summary>
        /// Supported modes for the associated operation.
        /// </summary>
        public ModeJson[] modes;

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        public bool IsValid() {
            return string.IsNullOrEmpty(this.caption) == false;
        }

    }

    /// <summary>
    /// Defines template JSON state and operations.
    /// </summary>
    [System.Serializable]
    public struct TemplateJson {

        /// <summary>
        /// Defines mode JSON state and operations for <c>TemplateJson</c>.
        /// </summary>
        [System.Serializable]
        public struct ModeJson {

            /// <summary>
            /// Package used by <c>TemplateJson.ModeJson</c>.
            /// </summary>
            public string package;
            /// <summary>
            /// Dependencies used by <c>TemplateJson.ModeJson</c>.
            /// </summary>
            public string[] dependencies;
            /// <summary>
            /// Defines used by <c>TemplateJson.ModeJson</c>.
            /// </summary>
            public string[] defines;
            /// <summary>
            /// Selected execution or presentation mode.
            /// </summary>
            public int mode;

        }

        /// <summary>
        /// Supported modes for the associated operation.
        /// </summary>
        public ModeJson[] modes;

    }

    /// <summary>
    /// Defines template inner JSON state and operations.
    /// </summary>
    [System.Serializable]
    public struct TemplateInnerJson {

        /// <summary>
        /// Defines file state and operations for <c>TemplateInnerJson</c>.
        /// </summary>
        [System.Serializable]
        public struct File {

            /// <summary>
            /// Search used by <c>TemplateInnerJson.File</c>.
            /// </summary>
            public string search;
            /// <summary>
            /// Destination or target of the associated operation.
            /// </summary>
            public string target;
            /// <summary>
            /// File used by <c>TemplateInnerJson.File</c>.
            /// </summary>
            public string file;

        }

        /// <summary>
        /// Files used by <c>TemplateInnerJson</c>.
        /// </summary>
        public File[] files;
        /// <summary>
        /// Configurations supplying values for this operation.
        /// </summary>
        public File[] configs;
        /// <summary>
        /// Views used by <c>TemplateInnerJson</c>.
        /// </summary>
        public File[] views;

    }

    /// <summary>
    /// Defines option JSON state and operations.
    /// </summary>
    [System.Serializable]
    public struct OptionJson {

        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public bool state;

        /// <summary>
        /// Initializes <c>OptionJson</c> from the supplied option info.
        /// </summary>
        public OptionJson(OptionInfo optionInfo) {
            this.state = optionInfo.state;
        }

    }
    
    /// <summary>
    /// Defines new project state and operations.
    /// </summary>
    public static class NewProject {

        /// <summary>
        /// Options used by <c>NewProject</c>.
        /// </summary>
        public static readonly OptionInfo[] options = new OptionInfo[] {
            new OptionInfo() {
                state = false,
                caption = "Fixed-Point Mathematics",
                description = "Use fixed-point mathematics for calculations instead of Unity.Mathematics.",
                action = (state) => {
                    if (state == true) {
                        NewProject.AddDefines(new[] {
                            "FIXED_POINT",
                        });
                    }
                }
            },
            new OptionInfo() {
                state = true,
                caption = "Default exceptions",
                description = "[Recommended] Add all default exceptions.",
                action = (state) => {
                    if (state == true) {
                        NewProject.AddDefines(new[] {
                            "EXCEPTIONS_CONTEXT",
                            "EXCEPTIONS_THREAD_SAFE",
                            "EXCEPTIONS_COLLECTIONS",
                            "EXCEPTIONS_COMMAND_BUFFER",
                            "EXCEPTIONS_ENTITIES",
                            "EXCEPTIONS_QUERY_BUILDER",
                            "EXCEPTIONS_INTERNAL",
                            "EXCEPTIONS",
                            "EXCEPTIONS_ASPECTS",
                        });
                    }
                },
            },
            new OptionInfo() {
                state = false,
                caption = "Debug checks",
                description = "Enables pointers checking and debugging of system jobs dependencies.",
                action = (state) => {
                    if (state == true) {
                        NewProject.AddDefines(new[] {
                            "ENABLE_BECS_COLLECTIONS_CHECKS",
                        });
                    }
                },
            },
        };

        /// <summary>
        /// Initializes the supplied storage or context.
        /// </summary>
        public static void Create(string path, string projectName, string template, Mode mode) {
            path = $"{path}/{projectName}";
            path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);
            {
                CreateTemplateScript(path, projectName, template, mode);
            }
        }

        private static void CreateTemplateScript(string path, string name, string template, Mode mode) {

            var dir = $"ME.BECS.Resources/Templates/{template}";
            var templateAsset = EditorUtils.LoadResource<UnityEngine.TextAsset>($"{dir}/template.json");
            var json = templateAsset.text;
            var templateJson = JsonUtility.FromJson<TemplateJson>(json);

            var modeJson = templateJson.modes.FirstOrDefault(x => x.mode == (int)mode);
            CreateTemplate(modeJson, System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(templateAsset)), dir, path, name);

        }

        private static void OnPackageImport(PackageImport packageImport) {
            AssetDatabase.Refresh();
            Debug.Log("Importing package...");
            var root = "Assets/__template";
            //try {
                // complete
                var templateInnerFile = $"{root}/template.json";
                var templateText = System.IO.File.ReadAllText(templateInnerFile);
                var templateInner = JsonUtility.FromJson<TemplateInnerJson>(templateText);
                var allFiles = System.IO.Directory.EnumerateFiles(root, "*.*", System.IO.SearchOption.AllDirectories).ToList();
                var localRoot = $"{root}/data";

                //EditorUtility.DisplayProgressBar("Creating Project", "Patching", 0f);

                // patch files
                //var index = 0;
                foreach (var file in allFiles) {

                    //EditorUtility.DisplayProgressBar("Creating Project", "Patching", ++index / (float)allFiles.Count);

                    if (System.IO.File.Exists(file) == false) continue;
                    var data = System.IO.File.ReadAllText(file);
                    data = Patch(localRoot, packageImport.projectName, templateInner, data);
                    System.IO.File.WriteAllText(file, data);
                    var filename = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (filename.Contains("__template_name__") == true) {
                        AssetDatabase.RenameAsset(file, filename.Replace("__template_name__", packageImport.projectName));
                    }
                }
                
                // add defines
                AddDefines(packageImport.mode.defines);
                
                // apply options
                ApplyOptions(packageImport.options);
                
                //EditorUtility.DisplayProgressBar("Creating Project", "Clean up", 1f);
                
                // move files
                {
                    AssetDatabase.MoveAsset($"{root}/data", packageImport.targetPath);
                    CreateAssembly(packageImport.targetPath, packageImport.projectName, packageImport.mode.dependencies);
                }
            /*} catch (System.Exception ex) {
                Debug.LogException(ex);
            } finally */{
                // remove template
                AssetDatabase.DeleteAsset(root);

                // Remove orphaned csproj file generated from template asmdef
                var csprojPath = "NewProject.csproj";
                if (System.IO.File.Exists(csprojPath)) {
                    System.IO.File.Delete(csprojPath);
                }
                //EditorUtility.ClearProgressBar();
            }
            
            static string Patch(string root, string projectName, TemplateInnerJson data, string text) {
                text = text.Replace("namespace NewProject", $"namespace {projectName}");
                // Patch Unity serialized asset type references (YAML format)
                text = text.Replace("ns: NewProject", $"ns: {projectName}");
                text = text.Replace("asm: NewProject", $"asm: {projectName}");
                // Patch assembly definition name (prevents regeneration with wrong name)
                text = text.Replace("\"name\": \"NewProject\"", $"\"name\": \"{projectName}\"");
                if (data.files != null) {
                    foreach (var file in data.files) {
                        var obj = AssetDatabase.LoadAssetAtPath<Object>($"{root}/{file.file}");
                        var newId = ObjectReferenceRegistry.data.Add(obj, out _);
                        text = text.Replace(file.search, string.Format(file.target, newId));
                    }
                }
                if (data.configs != null) {
                    foreach (var file in data.configs) {
                        var obj = AssetDatabase.LoadAssetAtPath<EntityConfig>($"{root}/{file.file}");
                        var newId = ObjectReferenceRegistry.data.Add(obj, out _);
                        text = text.Replace(file.search, string.Format(file.target, newId));
                    }
                }
                if (data.views != null) {
                    foreach (var file in data.views) {
                        var obj = AssetDatabase.LoadAssetAtPath<GameObject>($"{root}/{file.file}").GetComponent<ME.BECS.Views.EntityView>();
                        var newId = ObjectReferenceRegistry.data.Add(obj, out _);
                        text = text.Replace(file.search, string.Format(file.target, newId));
                    }
                }

                return text;
            }

        }

        /// <summary>
        /// Applies options.
        /// </summary>
        public static void ApplyOptions(OptionJson[] options) {

            for (int i = 0; i < options.Length; ++i) {
                var option = options[i];
                NewProject.options[i].action.Invoke(option.state);
            }

        }

        /// <summary>
        /// Adds defines.
        /// </summary>
        public static void AddDefines(string[] defines) {
            if (defines == null) return;
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var name = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(name, out var currentDefines);
            var list = new scg::List<string>(defines.Length);
            foreach (var define in defines) {
                if (currentDefines.Any(x => x == define) == true) {
                    // skip
                    continue;
                }
                list.Add(define);
            }
            currentDefines = currentDefines.Concat(list).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(name, currentDefines);
        }

        /// <summary>
        /// Handles the script reloaded callback.
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        public static void OnScriptReloaded() {
            if (EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData") == false) return;
            EditorApplication.delayCall += () => {
                var data = EditorPrefs.GetString("ME.BECS.Editor.AwaitPackageImportData");
                EditorPrefs.DeleteKey("ME.BECS.Editor.AwaitPackageImportData");
                OnPackageImport(JsonUtility.FromJson<PackageImport>(data));
            };
        }

        private static void CreateTemplate(TemplateJson.ModeJson templateJson, string templatePath, string dir, string targetPath, string projectName) {
            
            //EditorUtility.DisplayProgressBar("Creating Project", "Creating Project", 0f);
            
            EditorPrefs.SetString("ME.BECS.Editor.AwaitPackageImportData", JsonUtility.ToJson(new PackageImport() {
                projectName = projectName,
                mode = templateJson,
                targetPath = targetPath,
                options = NewProject.options.Select(x => new OptionJson(x)).ToArray(),
            }));
            
            // unpack template
            AssetDatabase.importPackageFailed += (packageName, error) => {
                Debug.LogError(error);
                //EditorUtility.ClearProgressBar();
            };
            AssetDatabase.importPackageCompleted += (packageName) => {
                OnScriptReloaded();
            };
            AssetDatabase.ImportPackage($"{templatePath}/{templateJson.package}", false);

        }

        private static void CreateAssembly(string targetPath, string projectName, string[] dependencies) {

            var asmContent = EditorUtils.LoadResource<UnityEngine.TextAsset>($"ME.BECS.Resources/Templates/DefaultAssembly-Template.txt").text;
            asmContent = asmContent.Replace("{{PROJECT_NAME}}", projectName);
            asmContent = asmContent.Replace("{{DEPENDENCIES}}", dependencies != null ? $",\n\"{string.Join("\",\n\"", dependencies)}\"" : string.Empty);

            var assetPath = $"{targetPath}/{projectName}.asmdef";
            System.IO.File.WriteAllText(assetPath, asmContent);
            UnityEditor.AssetDatabase.ImportAsset(assetPath);

        }
        
    }

}
