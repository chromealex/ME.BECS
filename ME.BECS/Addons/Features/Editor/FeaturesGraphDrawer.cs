using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(ME.BECS.FeaturesGraph.SystemsGraph))]
    public class FeaturesGraphDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (FeaturesGraphDrawer.styleSheetBase == null) {
                FeaturesGraphDrawer.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/FeaturesGraph.uss");
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(FeaturesGraphDrawer.styleSheetBase);

            this.DrawField(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void DrawField(VisualElement root, SerializedProperty property) {

            var so = property.serializedObject;

            var field = new ObjectField(property.displayName);
            field.value = property.objectReferenceValue;
            field.AddToClassList("systems-graph-field");
            if (field.value != null) field.AddToClassList("set");
            field.allowSceneObjects = false;
            field.objectType = typeof(ME.BECS.FeaturesGraph.SystemsGraph);
            field.RegisterValueChangedCallback((evt) => {
                so.Update();
                property.objectReferenceValue = evt.newValue;
                so.ApplyModifiedProperties();
                so.Update();
                if (property.objectReferenceValue != null) {
                    field.AddToClassList("set");
                } else {
                    field.RemoveFromClassList("set");
                }
            });
            
            root.Add(field);

        }

    }

}