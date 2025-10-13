using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using scg = System.Collections.Generic;

namespace ME.BECS.Editor {

    public class CreateProjectEditorWindow : EditorWindow {

        private StyleSheet styleSheet;
        public string path;
        private string projectName;
        private int genreIndex = -1;
        private int modeIndex = -1;
        private ListView modesList;
        private TemplateInfo[] allModes;
        private scg::List<TemplateInfo> currentModes;

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
                this.projectName = projectName.value = "NewProject";
                projectName.RegisterValueChangedCallback((evt) => {
                    this.projectName = evt.newValue;
                    this.projectName = projectName.value = EditorUtils.UpperFirstLetter(this.projectName);
                    if (EditorUtils.IsValidProjectName(this.projectName) == false) {
                        projectName.AddToClassList("fail");
                    } else {
                        projectName.RemoveFromClassList("fail");
                    }

                    this.UpdateButton(createButton);
                });
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
            var pathRoot = path;
            path = $"{path}/{projectName}";
            path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);
            var newName = System.IO.Path.GetFileName(path);
            var dirGuid = UnityEditor.AssetDatabase.CreateFolder(pathRoot, newName);
            var dirPath = UnityEditor.AssetDatabase.GUIDToAssetPath(dirGuid);
            {
                CreateTemplateScript(dirPath, newName, template, mode);
            }
        }

        [System.Serializable]
        public struct TemplateJson {

            [System.Serializable]
            public struct SystemJson {

                public string name;

            }

            [System.Serializable]
            public struct SystemGroupJson {

                public string name;
                public SystemJson[] systems;

            }

            [System.Serializable]
            public struct GraphJson {

                [System.Serializable]
                public struct GraphSystem {

                    public string name;
                    public string guid;

                }
                
                public string name;
                public GraphSystem[] systems;
                
            }

            [System.Serializable]
            public struct ModeJson {

                public string directory;
                public string[] initializers;
                public string[] dependencies;
                public SystemGroupJson[] systems;
                public GraphJson[] graphs;
                public string[] files;
                public int mode;

            }

            public ModeJson[] modes;

        }

        private static void CreateTemplateScript(string path, string name, string template, Mode mode) {

            var dir = $"ME.BECS.Resources/Templates/{template}";
            var json = EditorUtils.LoadResource<UnityEngine.TextAsset>($"{dir}/template.json").text;
            var templateJson = JsonUtility.FromJson<TemplateJson>(json);

            var modeJson = templateJson.modes.FirstOrDefault(x => x.mode == (int)mode);
            CreateTemplate(modeJson, dir, path, name);

        }

        private static void CreateTemplate(TemplateJson.ModeJson templateJson, string dir, string targetPath, string projectName) {
            
            CreateAssembly(targetPath, projectName, templateJson.dependencies);
            
            for (int i = 0; i < templateJson.initializers?.Length; ++i) {
                var templateName = templateJson.initializers[i];
                var initializerName = $"{templateName}Initializer";
                var scriptContent = EditorUtils.LoadResource<UnityEngine.TextAsset>($"{dir}/{templateJson.directory}/{initializerName}.txt").text;
                scriptContent = scriptContent.Replace("{{PROJECT_NAME}}", projectName);
                scriptContent = scriptContent.Replace("{{SCRIPT_NAME}}", initializerName);
                scriptContent = scriptContent.Replace("{{SCRIPT_NAME_SHORT}}", templateName);

                var assetPath = $"{targetPath}/{initializerName}Initializer.cs";
                WriteFile(assetPath, scriptContent);
            }

            for (int i = 0; i < templateJson.systems?.Length; ++i) {
                var groupName = templateJson.systems[i].name;
                for (int j = 0; j < templateJson.systems[i].systems.Length; ++j) {
                    var systemName = templateJson.systems[i].systems[j].name;
                    var scriptContent = EditorUtils.LoadResource<UnityEngine.TextAsset>($"{dir}/{templateJson.directory}/Systems/{groupName}/{systemName}.txt").text;
                    scriptContent = scriptContent.Replace("{{PROJECT_NAME}}", projectName);
                    scriptContent = scriptContent.Replace("{{SCRIPT_NAME}}", systemName);

                    var writeDir = $"{targetPath}/Systems/{groupName}";
                    WriteDir(writeDir);
                    var assetPath = $"{writeDir}/{systemName}.cs";
                    WriteFile(assetPath, scriptContent);
                }
            }

            WriteDir($"{targetPath}/Graphs");
            for (int i = 0; i < templateJson.graphs?.Length; ++i) {
                var graph = templateJson.graphs[i];
                var graphName = graph.name;
                Copy<ME.BECS.FeaturesGraph.SystemsGraph>($"{dir}/{templateJson.directory}/Graphs/{graphName}Graph.asset", $"{targetPath}/Graphs/{projectName}-{graphName}Graph.asset", (text) => {
                    foreach (var system in graph.systems) {
                        text = text.Replace(system.guid, "type: {class: " + system.name + ", ns: " + projectName + @", asm: " + projectName + "}");
                    }
                    text = text.Replace("builtInGraph: 1", "builtInGraph: 0");
                    return text;
                });
            }

            for (int i = 0; i < templateJson.files?.Length; ++i) {
                var file = templateJson.files[i];
                var res = EditorUtils.LoadResource<TextAsset>($"{dir}/{templateJson.directory}/{file}");
                var path = AssetDatabase.GetAssetPath(res);
                var localDir = System.IO.Path.GetDirectoryName(file);
                WriteDir($"{targetPath}/{localDir}");
                var targetFilePath = $"{targetPath}/{localDir}/{System.IO.Path.GetFileNameWithoutExtension(path)}";
                System.IO.File.Copy(path, targetFilePath, true);
                var text = System.IO.File.ReadAllText(targetFilePath);
                text = text.Replace("{{PROJECT_NAME}}", projectName);
                System.IO.File.WriteAllText(targetFilePath, text);
                AssetDatabase.ImportAsset(targetFilePath);
            }

        }

        private static T Copy<T>(string from, string to, System.Func<string, string> onProcess) where T : Object {
            var res = EditorUtils.LoadResource<T>(from);
            var path = AssetDatabase.GetAssetPath(res);
            System.IO.File.Copy(path, to, true);
            var text = System.IO.File.ReadAllText(to);
            text = onProcess.Invoke(text);
            text = text.Replace($"m_Name: {System.IO.Path.GetFileNameWithoutExtension(from)}", $"m_Name: {System.IO.Path.GetFileNameWithoutExtension(to)}");
            System.IO.File.WriteAllText(to, text);
            AssetDatabase.ImportAsset(to);
            var asset = AssetDatabase.LoadAssetAtPath<T>(to);
            return asset;
        }

        private static void WriteDir(string path) {

            System.IO.Directory.CreateDirectory(path);

        }

        private static string WriteFile(string assetPath, string scriptContent) {
            System.IO.File.WriteAllText(assetPath, scriptContent);
            UnityEditor.AssetDatabase.ImportAsset(assetPath);
            UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(assetPath), out var guid, out long localId);
            var metaAssetPath = $"{assetPath}.meta";
            var meta = EditorUtils.LoadResource<UnityEngine.TextAsset>($"ME.BECS.Resources/Templates/DefaultScriptMeta-Template.txt").text;
            meta = meta.Replace("{{GUID}}", guid);
            System.IO.File.WriteAllText(metaAssetPath, meta);
            UnityEditor.AssetDatabase.ImportAsset(assetPath);
            return guid;
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