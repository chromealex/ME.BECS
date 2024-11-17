using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {
    
    [CustomPropertyDrawer(typeof(ObjectReference<>))]
    public class ObjectReferenceDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            ObjectReferenceRegistry.Initialize();
            
            var id = property.FindPropertyRelative(nameof(ObjectReference<UnityEngine.Object>.id));
            var obj = ObjectReferenceRegistry.GetObjectBySourceId<UnityEngine.Object>(id.uintValue);
            
            var container = new UnityEngine.UIElements.VisualElement();
            var objectField = new ObjectField(property.displayName);
            var type = this.fieldInfo.FieldType.GenericTypeArguments[0];

            objectField.objectType = type;
            objectField.allowSceneObjects = false;
            objectField.value = obj;
            objectField.RegisterValueChangedCallback((evt) => {
                if (evt.newValue == evt.previousValue) return;
                var pId = ObjectReferenceRegistryUtils.Assign(evt.previousValue, evt.newValue);
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