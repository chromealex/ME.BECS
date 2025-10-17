using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using scg = System.Collections.Generic;

namespace ME.BECS.Editor {
    
    using CreateProject;

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
        private ListView optionsList;

        public static void ShowWindow(string pathRoot) {
            var win = WorldEntityEditorWindow.CreateInstance<CreateProjectEditorWindow>();
            win.titleContent = new GUIContent("New Project", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-quickstart.png"));
            win.maxSize = new Vector2(860f, 720f);
            win.minSize = new Vector2(860f, 520f);
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
            
            var root = this.rootVisualElement;
            EditorUIUtils.ApplyDefaultStyles(root);
            root.styleSheets.Add(this.styleSheet);

            var createButton = new Button(() => {
                CreateProject.NewProject.Create(this.path, this.projectName, genres[this.genreIndex].template, ((TemplateInfo)this.modesList.itemsSource[this.modeIndex]).mode);
                this.Close();
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

                    var template = (TemplateInfo)this.modesList.itemsSource[i];
                    element.Q<Label>(className: "caption").text = template.caption;
                    element.Q<Label>(className: "description").text = template.description;
                    element.Q<Image>(className: "icon").image = template.icon;

                };
                templatesContainer.RefreshItems();
            }
            {
                var templatesContainer = new ListView(NewProject.options);
                templatesContainer.selectionType = SelectionType.None;
                this.optionsList = templatesContainer;
                content.Add(templatesContainer);
                templatesContainer.AddToClassList("templates-container");
                templatesContainer.AddToClassList("templates-modes-container");

                templatesContainer.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
                templatesContainer.makeHeader = () => {
                    var header = new Label("Options");
                    header.AddToClassList("header");
                    return header;
                };
                templatesContainer.makeItem = () => {
                    var templateRoot = new VisualElement();
                    templateRoot.AddToClassList("template-container-toggle");

                    var caption = new Toggle();
                    caption.AddToClassList("caption");
                    templateRoot.Add(caption);

                    var description = new Label();
                    description.AddToClassList("description");
                    templateRoot.Add(description);

                    return templateRoot;
                };
                templatesContainer.bindItem = (element, i) => {

                    var template = NewProject.options[i];
                    var toggle = element.Q<Toggle>(className: "caption");
                    toggle.text = template.caption;
                    toggle.value = NewProject.options[i].state;
                    toggle.RegisterValueChangedCallback((evt) => {
                        NewProject.options[i].state = evt.newValue;
                    });
                    element.Q<Label>(className: "description").text = template.description;

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