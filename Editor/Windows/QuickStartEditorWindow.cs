using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;
    
    [InitializeOnLoad]
    public static class QuickStartEditorWindowStartUp {
        static QuickStartEditorWindowStartUp() {
            if (SessionState.GetBool("ME.BECS.Editor.QuickStartEditorWindowStartUp", false) == false) {
                if (QuickStartEditorWindow.IsShowOnStartUp() == true) {
                    EditorApplication.delayCall += () => {
                        QuickStartEditorWindow.ShowWindow();
                    };
                }
                SessionState.SetBool("ME.BECS.Editor.QuickStartEditorWindowStartUp", true);
            }
        }
    }

    public class QuickStartEditorWindow : EditorWindow {

        public struct TutorialInfo {

            public string caption;
            public string description;
            public string url;
            public Texture2D texture;
            
        }
        
        private StyleSheet styleSheet;
        
        public static void SetShowOnStartUp(bool state) {
            EditorPrefs.SetBool("ME.BECS.Editor.QuickStartEditorWindow.ShowOnStartUp", state);
        }

        public static bool IsShowOnStartUp() {
            return EditorPrefs.GetBool("ME.BECS.Editor.QuickStartEditorWindow.ShowOnStartUp", true);
        }

        public static void ShowWindow() {
            var win = WorldEntityEditorWindow.CreateInstance<QuickStartEditorWindow>();
            win.titleContent = new GUIContent("Quick Start", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-quickstart.png"));
            win.maxSize = new Vector2(900f, 700f);
            win.minSize = new Vector2(900f, 700f);
            win.ShowUtility();
        }

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/QuickStart.uss");
            }
        }
        
        public void CreateGUI() {

            this.LoadStyle();

            var tutorials = new TutorialInfo[] {
                new TutorialInfo() {
                    caption = "Project Initialization",
                    description = "How to initialize project.",
                    url = "https://youtu.be/PCdhnXEjRQI",
                    texture = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/QuickStart/tutorial-1.jpg"),
                },
                new TutorialInfo() {
                    caption = "Basics",
                    description = "Core API, entities API, how to read and write data.",
                    url = "https://youtu.be/5clulSSIrco",
                    texture = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/QuickStart/tutorial-2.jpg"),
                },
                new TutorialInfo() {
                    caption = "Views",
                    description = "How to create and use views.",
                    url = "https://youtu.be/PWGOLubeh3k",
                    texture = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/QuickStart/tutorial-3.jpg"),
                },
                new TutorialInfo() {
                    caption = "Network Events, Jobs, Queries",
                    description = "How to send network events through transport, how to schedule queries and jobs.",
                    url = "https://youtu.be/dzUZorAW_uI",
                    texture = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/QuickStart/tutorial-4.jpg"),
                },
                new TutorialInfo() {
                    caption = "Entity Configs",
                    description = "How to use Entity Configs with different joins.",
                    url = "https://youtu.be/vItAprfcc0A",
                    texture = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/QuickStart/tutorial-5.jpg"),
                },
                new TutorialInfo() {
                    caption = "Addressables and Global Events",
                    description = "When and where addressables will be used. How to use Global Events.",
                    url = "https://youtu.be/4mbOPXLfArM",
                    texture = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/QuickStart/tutorial-6.jpg"),
                },
                new TutorialInfo() {
                    caption = "Players and Teams",
                    description = "How to use PlayersSystem and initialize and change teams.",
                    url = "https://youtu.be/6syQWOWxUwY",
                    texture = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/QuickStart/tutorial-7.jpg"),
                },
                new TutorialInfo() {
                    caption = "Trees",
                    description = "How to use Trees queries.",
                    url = "https://youtu.be/TilgB9G1G3g",
                    texture = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/QuickStart/tutorial-8.jpg"),
                },
            };

            var root = this.rootVisualElement;
            EditorUIUtils.ApplyDefaultStyles(root);
            root.styleSheets.Add(this.styleSheet);
            
            EditorUIUtils.AddLogoLine(root);
            
            var scrollView = new ScrollView();
            root.Add(scrollView);

            var showOnStartup = new Toggle("Show on startup");
            showOnStartup.AddToClassList("showonstartup");
            showOnStartup.value = IsShowOnStartUp();
            showOnStartup.RegisterValueChangedCallback((evt) => {
                QuickStartEditorWindow.SetShowOnStartUp(evt.newValue);
            });
            root.Add(showOnStartup);
            
            var container = new VisualElement();
            container.AddToClassList("container");
            scrollView.Add(container);
            var number = 1;
            foreach (var tutorial in tutorials) {
                var info = tutorial;
                var item = new VisualElement();
                item.pickingMode = PickingMode.Position;
                item.AddToClassList("tutorial-item");
                var button = new Button(() => {
                    Application.OpenURL(info.url);
                });
                item.Add(button);
                var lbl = new Label($"#{number}");
                lbl.AddToClassList("label-number");
                item.Add(lbl);
                button.iconImage = Background.FromTexture2D(info.texture);
                var labelContainer = new VisualElement();
                labelContainer.pickingMode = PickingMode.Ignore;
                labelContainer.AddToClassList("label-container");
                item.Add(labelContainer);
                var label = new VisualElement();
                labelContainer.Add(label);
                label.AddToClassList("label");
                {
                    var caption = new Label(info.caption);
                    caption.pickingMode = PickingMode.Ignore;
                    caption.AddToClassList("caption");
                    label.Add(caption);
                    var description = new Label(info.description);
                    description.pickingMode = PickingMode.Ignore;
                    description.AddToClassList("description");
                    label.Add(description);
                }
                var play = new Label("\u25B6");
                item.Add(play);
                play.AddToClassList("play-icon");
                play.pickingMode = PickingMode.Ignore;
                container.Add(item);
                ++number;
            }

        }

    }

}