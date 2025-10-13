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

        public enum Mode {
            SinglePlayer = 0,
            Multiplayer  = 1,
        }
        
        public struct TemplateInfo {

            public Texture icon;
            public string caption;
            public string description;
            public string template;

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
            };
            
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
                    },
                    new TemplateInfo() {
                        caption = "Multiplayer",
                        description = "Several players template",
                        icon = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/Templates/Modes/Multiplayer.png"),
                    },
                };

                var templatesContainer = new ListView(modes);
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
                CreateAssembly(dirPath, newName, mode);
                CreateTemplateScript(dirPath, newName, template, mode);
            }
        }

        [System.Serializable]
        public struct TemplateJson {

            [System.Serializable]
            public struct ModeJson {

                public string directory;
                public string[] initializers;

            }

            public ModeJson singleplayer;
            public ModeJson multiplayer;

        }

        private static void CreateTemplateScript(string path, string name, string template, Mode mode) {

            var dir = $"ME.BECS.Resources/Templates/{template}";
            var json = ME.BECS.Editor.EditorUtils.LoadResource<UnityEngine.TextAsset>($"{dir}/Template.txt").text;
            var templateJson = JsonUtility.FromJson<TemplateJson>(json);

            if (mode == Mode.Multiplayer) {

                CreateTemplate(templateJson.multiplayer, dir, path, name);

            } else {

                CreateTemplate(templateJson.singleplayer, dir, path, name);

            }

        }

        private static void CreateTemplate(TemplateJson.ModeJson templateJson, string dir, string targetPath, string projectName) {
            for (int i = 0; i < templateJson.initializers.Length; ++i) {
                var templateName = templateJson.initializers[i];
                var scriptContent = ME.BECS.Editor.EditorUtils.LoadResource<UnityEngine.TextAsset>($"{dir}/{templateJson.directory}/{templateName}.txt").text;
                scriptContent = scriptContent.Replace("{{PROJECT_NAME}}", projectName);
                scriptContent = scriptContent.Replace("{{SCRIPT_NAME}}", templateName);

                var assetPath = $"{targetPath}/{templateName}Initializer.cs";
                System.IO.File.WriteAllText(assetPath, scriptContent);
                UnityEditor.AssetDatabase.ImportAsset(assetPath);

                UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(assetPath), out var guid, out long localId);
                var metaAssetPath = $"{assetPath}.meta";
                var meta = ME.BECS.Editor.EditorUtils.LoadResource<UnityEngine.TextAsset>($"ME.BECS.Resources/Templates/DefaultScriptMeta-Template.txt").text;
                meta = meta.Replace("{{GUID}}", guid);
                System.IO.File.WriteAllText(metaAssetPath, meta);
            }
        }

        private static void CreateAssembly(string path, string name, Mode mode) {

            var asmContent = ME.BECS.Editor.EditorUtils.LoadResource<UnityEngine.TextAsset>($"ME.BECS.Resources/Templates/{mode.ToString()}Assembly-Template.txt").text;
            asmContent = asmContent.Replace("{{PROJECT_NAME}}", name);

            var assetPath = $"{path}/{name}.asmdef";
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