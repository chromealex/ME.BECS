using UnityEditor;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(bbool))]
    public class BBoolPropertyDrawer : PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            var val = property.FindPropertyRelative(nameof(bbool.value));
            
            var container = new Toggle(property.displayName);
            container.SetValueWithoutNotify(val.intValue == 1 ? true : false);
            container.RegisterValueChangedCallback((evt) => {
                property.serializedObject.Update();
                val.intValue = evt.newValue == true ? 1 : 0;
                property.serializedObject.ApplyModifiedProperties();
            });
            return container;
            
        }
        
    }

}