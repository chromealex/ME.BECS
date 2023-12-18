using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ME.BECS.Editor;

namespace ME.BECS.Views.Editor {

    [CustomPropertyDrawer(typeof(ViewsModuleProperties))]
    public class ViewsPropertiesDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (ViewsPropertiesDrawer.styleSheetBase == null) {
                ViewsPropertiesDrawer.styleSheetBase = ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldProperties.uss");
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(ViewsPropertiesDrawer.styleSheetBase);

            this.DrawProperties(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void DrawProperties(VisualElement root, SerializedProperty property) {

            var so = property.serializedObject;
            var foldout = new Foldout();
            foldout.AddToClassList("foldout");
            foldout.text = "Views Module Properties";
            { // State properties
                var container = new VisualElement();
                container.AddToClassList("properties-block");
                foldout.Add(container);
                var instancesRegistryCapacity = property.FindPropertyRelative(nameof(ViewsModuleProperties.instancesRegistryCapacity));
                var renderingObjectsCapacity = property.FindPropertyRelative(nameof(ViewsModuleProperties.renderingObjectsCapacity));
                var viewsGameObjects = property.FindPropertyRelative(nameof(ViewsModuleProperties.viewsGameObjects));
                var viewsDrawMeshes = property.FindPropertyRelative(nameof(ViewsModuleProperties.viewsDrawMeshes));
                var interpolateState = property.FindPropertyRelative(nameof(ViewsModuleProperties.interpolateState));
                ME.BECS.Editor.EditorUIUtils.DrawUIntField(container, instancesRegistryCapacity);
                ME.BECS.Editor.EditorUIUtils.DrawUIntField(container, renderingObjectsCapacity);
                ME.BECS.Editor.EditorUIUtils.DrawToggleField(container, viewsGameObjects);
                ME.BECS.Editor.EditorUIUtils.DrawToggleField(container, viewsDrawMeshes);
                ME.BECS.Editor.EditorUIUtils.DrawToggleField(container, interpolateState);
            }
            root.Add(foldout);

        }

    }

}