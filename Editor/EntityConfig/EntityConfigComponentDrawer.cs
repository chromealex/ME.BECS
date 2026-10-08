using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ME.BECS.Editor;

namespace ME.BECS.Editor {
    
    /// <summary>
    /// Draws entity config ID values in the Unity Inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(EntityConfigIdAttribute))]
    public class EntityConfigIdDrawer : UnityEditor.PropertyDrawer {

        /// <summary>
        /// Builds the UI Toolkit editor for the supplied serialized property.
        /// </summary>
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