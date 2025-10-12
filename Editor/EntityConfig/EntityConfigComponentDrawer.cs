using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ME.BECS.Editor;

namespace ME.BECS.Editor {
    
    [CustomPropertyDrawer(typeof(Config))]
    public class EntityConfigDrawer : UnityEditor.PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();

            this.Draw(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void Draw(VisualElement rootVisualElement, SerializedProperty property) {

            var entityConfigComponent = (Config)property.boxedValue;
            var obj = new UnityEditor.UIElements.ObjectField(property.displayName);
            obj.SetEnabled(false);
            obj.value = EntityConfigsRegistry.GetEntityConfigBySourceId(entityConfigComponent.sourceId);
            rootVisualElement.Add(obj);

        }

    }

    [CustomPropertyDrawer(typeof(EntityConfigIdAttribute))]
    public class EntityConfigIdDrawer : UnityEditor.PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();

            this.Draw(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void Draw(VisualElement rootVisualElement, SerializedProperty property) {

            var id = property.uintValue;
            var obj = new UnityEditor.UIElements.ObjectField("Config");
            obj.SetEnabled(false);
            obj.value = EntityConfigsRegistry.GetEntityConfigBySourceId(id);
            rootVisualElement.Add(obj);

        }

    }

}