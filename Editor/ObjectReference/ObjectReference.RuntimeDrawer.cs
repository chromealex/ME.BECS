using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {
    
    [CustomPropertyDrawer(typeof(RuntimeObjectReference<>))]
    public class ObjectReferenceRuntimeDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var id = property.FindPropertyRelative(nameof(RuntimeObjectReference<UnityEngine.Object>.id));
            var worldId = property.FindPropertyRelative(nameof(RuntimeObjectReference<UnityEngine.Object>.worldId));
            var obj = RuntimeObjectReference.ReadObject<UnityEngine.Object>(id.uintValue, (ushort)worldId.uintValue);
            
            var container = new UnityEngine.UIElements.VisualElement();
            var objectField = new ObjectField(property.displayName);
            var type = this.fieldInfo.FieldType.GenericTypeArguments[0];

            objectField.objectType = type;
            objectField.allowSceneObjects = false;
            objectField.value = obj;
            objectField.RegisterValueChangedCallback((evt) => {
                if (evt.newValue == evt.previousValue) return;
                var pId = 0u;
                RuntimeObjectReference.GetObject(ref pId, (ushort)worldId.uintValue, evt.newValue);
                var prop = property.serializedObject.FindProperty(id.propertyPath);
                prop.serializedObject.Update();
                id.uintValue = pId;
                prop.serializedObject.ApplyModifiedProperties();
                prop.serializedObject.Update();
            });
            container.Add(objectField);
            return container;
            
        }

    }

}