using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ME.BECS.Editor;

namespace ME.BECS.Views.Editor {
    
    /// <summary>
    /// Draws view source values in the Unity Inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(ViewSource))]
    public class ViewSourceDrawer : UnityEditor.PropertyDrawer {

        /// <summary>
        /// Builds the UI Toolkit editor for the supplied serialized property.
        /// </summary>
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            ObjectReferenceRegistry.LoadForced();
            
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();

            this.Draw(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void Draw(VisualElement rootVisualElement, SerializedProperty property) {

            var viewSource = (ViewSource)property.boxedValue;
            var obj = new UnityEditor.UIElements.ObjectField(property.displayName);
            obj.SetEnabled(false);
            obj.value = ObjectReferenceRegistry.GetObjectBySourceId<EntityView>(viewSource.prefabId);
            rootVisualElement.Add(obj);

        }

    }

}