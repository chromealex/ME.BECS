using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Addons.Editor {

    [CustomPropertyDrawer(typeof(ME.BECS.Addons.ObjectReference<>))]
    public class ObjectReferenceDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var id = property.FindPropertyRelative(nameof(ME.BECS.Addons.ObjectReference<UnityEngine.Object>.id));
            var worldId = property.FindPropertyRelative(nameof(ME.BECS.Addons.ObjectReference<UnityEngine.Object>.worldId));
            var obj = RuntimeObjectReference.ReadObject<UnityEngine.Object>(id.uintValue, (ushort)worldId.uintValue);
            
            var container = new UnityEngine.UIElements.VisualElement();
            var objectField = new ObjectField(property.displayName);
            objectField.objectType = System.Type.GetType(property.type);//typeof(prop);
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