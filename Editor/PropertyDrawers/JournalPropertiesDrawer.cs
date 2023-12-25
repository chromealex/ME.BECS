using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ME.BECS.Editor.Extensions.SubclassSelector;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(JournalProperties))]
    public class JournalPropertiesDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (JournalPropertiesDrawer.styleSheetBase == null) {
                JournalPropertiesDrawer.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldProperties.uss");
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(JournalPropertiesDrawer.styleSheetBase);

            this.DrawProperties(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void DrawProperties(VisualElement root, SerializedProperty property) {

            var foldout = new Foldout();
            foldout.AddToClassList("foldout");
            foldout.text = "Journal Properties";
            { // State properties
                var container = new VisualElement();
                container.AddToClassList("properties-block");
                foldout.Add(container);
                var capacity = property.FindPropertyRelative(nameof(JournalProperties.capacity));
                EditorUIUtils.DrawUIntField(container, capacity, 1);
                var historyCapacity = property.FindPropertyRelative(nameof(JournalProperties.historyCapacity));
                EditorUIUtils.DrawUIntField(container, historyCapacity, 1);
            }
            root.Add(foldout);

        }

    }

}