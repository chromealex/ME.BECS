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

    [CustomPropertyDrawer(typeof(WorldInitializer.Modules))]
    public class ModulesDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (ModulesDrawer.styleSheetBase == null) {
                ModulesDrawer.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldProperties.uss");
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(ModulesDrawer.styleSheetBase);

            this.Draw(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void Draw(VisualElement root, SerializedProperty property) {

            var prop = property.FindPropertyRelative(nameof(WorldInitializer.modules.list));
            var container = new PropertyField(prop, property.displayName);
            container.AddToClassList("foldout");
            container.BindProperty(prop);
            //foldout.Add(container);
            /*
            var foldout = new Foldout();
            foldout.AddToClassList("foldout");
            foldout.text = "Modules";
            { // List
                var prop = property.FindPropertyRelative(nameof(WorldInitializer.modules.list));
                var container = new PropertyField(prop);
                container.AddToClassList("properties-block");
                container.BindProperty(prop);
                foldout.Add(container);
            }*/
            root.Add(container);

        }

    }

}