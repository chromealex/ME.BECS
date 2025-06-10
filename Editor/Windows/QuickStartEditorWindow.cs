using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;

    public class QuickStartEditorWindow : EditorWindow {

        private StyleSheet styleSheet;
        
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

            var root = this.rootVisualElement;
            EditorUIUtils.ApplyDefaultStyles(root);
            root.styleSheets.Add(this.styleSheet);
            
            EditorUIUtils.AddLogoLine(root);

            var container = new VisualElement();
            container.AddToClassList("container");
            root.Add(container);
            {
                var button = new Button();
                container.Add(button);
            }
            {
                var button = new Button();
                container.Add(button);
            }
            {
                var button = new Button();
                container.Add(button);
            }
            {
                var button = new Button();
                container.Add(button);
            }
            {
                var button = new Button();
                container.Add(button);
            }
            {
                var button = new Button();
                container.Add(button);
            }
            {
                var button = new Button();
                container.Add(button);
            }
            {
                var button = new Button();
                container.Add(button);
            }
            {
                var button = new Button();
                container.Add(button);
            }

        }

    }

}