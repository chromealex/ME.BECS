using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace ME.BECS.Editor.ComponentsViewer {

    public class ComponentMetadataEditorWindow : EditorWindow {

        public static void ShowWindow(VisualElement element, EditorUtils.ComponentGroupItem.ComponentMetaInfo info, System.Action onChanged) {

            var win = ComponentMetadataEditorWindow.CreateInstance<ComponentMetadataEditorWindow>();
            win.titleContent = new GUIContent(info.type.Name);
            win.info = info;
            win.onChanged = onChanged;
            var buttonRect = GUIUtility.GUIToScreenRect(element.worldBound);
            win.ShowAsDropDown(buttonRect, new Vector2(element.worldBound.width, 100f));

        }

        private static StyleSheet styleSheetBase;
        private static StyleSheet styleSheetTooltip;
        private static StyleSheet styleSheet;
        
        private void LoadStyle() {
            if (styleSheetBase == null) {
                styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/ComponentsViewerEditorWindow.uss");
            }
            if (styleSheetTooltip == null) {
                styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }
            if (styleSheet == null) {
                styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfig.uss");
            }
        }

        private EditorUtils.ComponentGroupItem.ComponentMetaInfo info;
        private System.Action onChanged;

        private void OnLostFocus() {
            this.onChanged?.Invoke();
        }

        private void OnDestroy() {
            this.onChanged?.Invoke();
        }

        private void CreateGUI() {

            this.LoadStyle();
            
            this.rootVisualElement.styleSheets.Add(styleSheetBase);
            this.rootVisualElement.styleSheets.Add(styleSheet);
            this.rootVisualElement.styleSheets.Add(styleSheetTooltip);

            if (this.info == null) {
                this.rootVisualElement.Add(new Label("Component Meta Info is null"));
                return;
            }
            var textField = new TextField();
            textField.AddToClassList("component-meta-text-field");
            textField.label = null;
            textField.multiline = true;
            textField.value = this.info.editorComment;
            textField.RegisterValueChangedCallback((evt) => {
                this.info.editorComment = evt.newValue;
            });

            this.rootVisualElement.Add(textField);

        }

    }

}