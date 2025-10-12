using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ME.BECS.Editor;

namespace ME.BECS.Editor {
    
    [CustomPropertyDrawer(typeof(EntityConfigComponent))]
    public class EntityConfigComponentDrawer : UnityEditor.PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();

            this.Draw(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void Draw(VisualElement rootVisualElement, SerializedProperty property) {

            var entityConfigComponent = (EntityConfigComponent)property.boxedValue;
            var obj = new UnityEditor.UIElements.ObjectField(property.displayName);
            obj.SetEnabled(false);
            obj.value = EntityConfigsRegistry.GetEntityConfigBySourceId(entityConfigComponent.id);
            rootVisualElement.Add(obj);

        }

    }

}