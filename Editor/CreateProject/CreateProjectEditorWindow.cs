using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using scg = System.Collections.Generic;

namespace ME.BECS.Editor {

    public abstract class CreateProjectDefaultModule {

        public abstract ModeSupport mode { get; }

        public abstract string CreateModule(string projectPath, string projectName);

    }

    public enum ModeSupport {
        SinglePlayer = 1 << 0,
        Multiplayer  = 1 << 1,
    }

    public class CreateProjectEditorWindow : EditorWindow {

        private StyleSheet styleSheet;
        public string path;
        private string projectName;
        private int genreIndex = -1;
        private int modeIndex = -1;
        private ListView modesList;
        private TemplateInfo[] allModes;
        private scg::List<TemplateInfo> currentModes;
        private TextField projectNameField;

        public enum Mode {
            SinglePlayer = 0,
            Multiplayer  = 1,
        }

        public struct TemplateInfo {

            public Texture icon;
            public string caption;
            public string description;
            public string template;
            public Mode[] modes;
            public Mode mode;

            public TemplateInfo(string path, TemplateInfoJson json) {
                this.caption = json.caption;
                this.description = json.description;
                this.icon = EditorUtils.LoadResource<Texture2D>($"{System.IO.Path.GetDirectoryName(path)}/icon.png", isRequired: false);
                if (this.icon == null) this.icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/Other.png");
                this.template = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetDirectoryName(path));
                this.modes = new Mode[json.modes.Length];
                for (int i = 0; i < json.modes.Length; ++i) {
                    this.modes[i] = (Mode)json.modes[i].mode;
                }
                this.mode = default;
            }

            public scg::List<TemplateInfo> GetModes(TemplateInfo[] allModes) {
                var modes = this.modes;
                return allModes.Where(x => System.Array.IndexOf(modes, x.mode) >= 0).ToList();
            }
            
        }

        public struct TemplateInfoJson {

            [System.Serializable]
            public struct ModeJson {

                public int mode;

            }

            public string caption;
            public string description;
            public ModeJson[] modes;

            public bool IsValid() {
                return string.IsNullOrEmpty(this.caption) == false;
            }

        }

        [System.Serializable]
        public struct TemplateJson {

            [System.Serializable]
            public struct ModeJson {

                public string package;
                public string[] dependencies;
                public string[] defines;
                public int mode;

            }

            public ModeJson[] modes;

        }

        [System.Serializable]
        public struct TemplateInnerJson {

            [System.Serializable]
            public struct File {

                public string search;
                public string target;
                public string file;

            }

            public File[] files;
            public File[] configs;
            public File[] views;

        }

        public static void ShowWindow(string pathRoot) {
            var win = WorldEntityEditorWindow.CreateInstance<CreateProjectEditorWindow>();
            win.titleContent = new GUIContent("New Project", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-quickstart.png"));
            win.maxSize = new Vector2(650f, 420f);
            win.minSize = new Vector2(650f, 420f);
            win.path = pathRoot;
            win.ShowUtility();
        }

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/CreateProject.uss");
            }
        }

        public void CreateGUI() {

            this.LoadStyle();

            this.genreIndex = -1;
            this.modeIndex = -1;

            var genres = new scg::List<TemplateInfo>();
            var allTemplates = AssetDatabase.FindAssets("t:TextAsset template");
            foreach (var templateGuid in allTemplates) {
                var path = AssetDatabase.GUIDToAssetPath(templateGuid);
                var json = System.IO.File.ReadAllText(path);
                try {
                    var templateInfo = JsonUtility.FromJson<TemplateInfoJson>(json);
                    if (templateInfo.IsValid() == true) {
                        genres.Add(new TemplateInfo(path, templateInfo));
                    }
                } catch (System.Exception) {}
            }
            /*
            var genres = new TemplateInfo[] {
                new TemplateInfo() {
                    caption = "RTS",
                    description = "Real-time strategy template",
                    icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/RTS.png"),
                    template = "RTS",
                },
                new TemplateInfo() {
                    caption = "Top-down",
                    description = "Top-down shooter template",
                    icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/TopDown.png"),
                    template = "TopDown",
                },
                new TemplateInfo() {
                    caption = "FPS",
                    description = "First person shooter template",
                    icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/FPS.png"),
                    template = "FPS",
                },
                new TemplateInfo() {
                    caption = "Other",
                    description = "Other game template (Empty template)",
                    icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/Other.png"),
                    template = "Empty",
                },
            };*/
            
            var root = this.rootVisualElement;
            EditorUIUtils.ApplyDefaultStyles(root);
            root.styleSheets.Add(this.styleSheet);

            var createButton = new Button(() => {
                CreateProject(this.path, this.projectName, genres[this.genreIndex].template, (Mode)this.modeIndex);
            });

            var top = new VisualElement();
            top.AddToClassList("top");
            root.Add(top);
            {

                var path = new Label($"{this.path}/");
                path.AddToClassList("path");
                top.Add(path);

                var projectName = new TextField();
                this.projectNameField = projectName;
                projectName.RegisterValueChangedCallback((evt) => {
                    this.projectName = evt.newValue;
                    this.projectName = projectName.value = EditorUtils.UpperFirstLetter(this.projectName);
                    this.UpdateButton(createButton);
                });
                this.projectName = projectName.value = "NewProject";
                projectName.AddToClassList("project-name");
                top.Add(projectName);
            }

            var content = new VisualElement();
            content.AddToClassList("content");
            root.Add(content);
            {

                var templatesContainer = new ListView(genres);
                content.Add(templatesContainer);
                templatesContainer.AddToClassList("templates-container");
                templatesContainer.AddToClassList("templates-genre-container");

                templatesContainer.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
                templatesContainer.makeHeader = () => {
                    var header = new Label("Genre");
                    header.AddToClassList("header");
                    return header;
                };
                templatesContainer.selectionChanged += (_) => {
                    this.genreIndex = templatesContainer.selectedIndex;
                    this.modesList.itemsSource = genres[this.genreIndex].GetModes(this.allModes);
                    this.modesList.RefreshItems();
                    this.UpdateButton(createButton);
                };
                templatesContainer.makeItem = () => {
                    var templateRoot = new VisualElement();
                    templateRoot.AddToClassList("template-container");

                    var icon = new Image();
                    icon.AddToClassList("icon");
                    templateRoot.Add(icon);
                    
                    var caption = new Label();
                    caption.AddToClassList("caption");
                    templateRoot.Add(caption);

                    var description = new Label();
                    description.AddToClassList("description");
                    templateRoot.Add(description);

                    return templateRoot;
                };
                templatesContainer.bindItem = (element, i) => {

                    var template = genres[i];
                    element.Q<Label>(className: "caption").text = template.caption;
                    element.Q<Label>(className: "description").text = template.description;
                    element.Q<Image>(className: "icon").image = template.icon;

                };
                templatesContainer.RefreshItems();
            }
            {
                var modes = new TemplateInfo[] {
                    new TemplateInfo() {
                        caption = "Single-player",
                        description = "Single player template",
                        icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/Modes/Singleplayer.png"),
                        mode = Mode.SinglePlayer,
                    },
                    new TemplateInfo() {
                        caption = "Multiplayer",
                        description = "Several players template",
                        icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/Modes/Multiplayer.png"),
                        mode = Mode.Multiplayer,
                    },
                };
                this.allModes = modes;

                var templatesContainer = new ListView();
                this.modesList = templatesContainer;
                content.Add(templatesContainer);
                templatesContainer.AddToClassList("templates-container");
                templatesContainer.AddToClassList("templates-modes-container");

                templatesContainer.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
                templatesContainer.makeHeader = () => {
                    var header = new Label("Mode");
                    header.AddToClassList("header");
                    return header;
                };
                templatesContainer.selectionChanged += (_) => {
                    this.modeIndex = templatesContainer.selectedIndex;
                    this.UpdateButton(createButton);
                };
                templatesContainer.makeItem = () => {
                    var templateRoot = new VisualElement();
                    templateRoot.AddToClassList("template-container");

                    var icon = new Image();
                    icon.AddToClassList("icon");
                    templateRoot.Add(icon);
                    
                    var caption = new Label();
                    caption.AddToClassList("caption");
                    templateRoot.Add(caption);

                    var description = new Label();
                    description.AddToClassList("description");
                    templateRoot.Add(description);

                    return templateRoot;
                };
                templatesContainer.bindItem = (element, i) => {

                    var template = modes[i];
                    element.Q<Label>(className: "caption").text = template.caption;
                    element.Q<Label>(className: "description").text = template.description;
                    element.Q<Image>(className: "icon").image = template.icon;

                };
                templatesContainer.RefreshItems();
            }

            var bottom = new VisualElement();
            bottom.AddToClassList("bottom-container");
            createButton.AddToClassList("create-button");
            createButton.text = "Create Project";
            bottom.Add(createButton);
            root.Add(bottom);
            
            this.UpdateButton(createButton);

        }

        private static void CreateProject(string path, string projectName, string template, Mode mode) {
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
                var index = 0;
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
                if (packageImport.mode.defines != null) {
                    BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                    var name = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
                    PlayerSettings.GetScriptingDefineSymbols(name, out var currentDefines);
                    var list = new scg::List<string>(packageImport.mode.defines.Length);
                    foreach (var define in packageImport.mode.defines) {
                        if (currentDefines.Any(x => x == define) == true) {
                            // skip
                            continue;
                        }
                        list.Add(define);
                    }
                    currentDefines = currentDefines.Concat(list).ToArray();
                    PlayerSettings.SetScriptingDefineSymbols(name, currentDefines);
                }
                
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
                //EditorUtility.ClearProgressBar();
            }
            
            static string Patch(string root, string projectName, TemplateInnerJson data, string text) {
                text = text.Replace("namespace NewProject", $"namespace {projectName}");
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

        public struct PackageImport {

            public TemplateJson.ModeJson mode;
            public string targetPath;
            public string projectName;

        }

        [UnityEditor.Callbacks.DidReloadScripts]
        public static void OnScriptReloaded() {
            if (EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData") == false) return;
            var data = EditorPrefs.GetString("ME.BECS.Editor.AwaitPackageImportData");
            EditorPrefs.DeleteKey("ME.BECS.Editor.AwaitPackageImportData");
            EditorApplication.delayCall += () => {
                OnPackageImport(JsonUtility.FromJson<PackageImport>(data));
            };
        }

        private static void CreateTemplate(TemplateJson.ModeJson templateJson, string templatePath, string dir, string targetPath, string projectName) {
            
            //EditorUtility.DisplayProgressBar("Creating Project", "Creating Project", 0f);
            
            EditorPrefs.SetString("ME.BECS.Editor.AwaitPackageImport", System.IO.Path.GetFileNameWithoutExtension(templateJson.package));
            EditorPrefs.SetString("ME.BECS.Editor.AwaitPackageImportData", JsonUtility.ToJson(new PackageImport() {
                projectName = projectName,
                mode = templateJson,
                targetPath = targetPath,
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
        
        private void UpdateButton(Button createButton) {
            bool state = EditorUtils.IsValidProjectName(this.projectName);
            if (System.IO.Directory.Exists($"{this.path}/{this.projectName}") == true) {
                state = false;
            }
            if (state == false) {
                this.projectNameField.AddToClassList("fail");
            } else {
                this.projectNameField.RemoveFromClassList("fail");
            }
            if (this.genreIndex == -1) {
                state = false;
            }
            if (this.modeIndex == -1) {
                state = false;
            }
            createButton.SetEnabled(state);
        }

    }

}