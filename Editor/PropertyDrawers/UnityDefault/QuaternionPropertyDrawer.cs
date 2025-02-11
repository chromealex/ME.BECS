using tfloat = System.Single;
using Unity.Mathematics;

namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;

    [CustomPropertyDrawer(typeof(quaternion))]
    public class QuaternionPropertyDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var vec = (UnityEngine.Quaternion)(quaternion)property.boxedValue;
            var value = vec.eulerAngles;
            var root = new Label(property.displayName);
            root.AddToClassList("quaternion-field");
            {
                var xField = new FloatField("X:");
                xField.value = value.x;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    property.boxedValue = (quaternion)UnityEngine.Quaternion.Euler(evt.newValue, value.y, value.z);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            {
                var xField = new FloatField("Y:");
                xField.value = value.y;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    property.boxedValue = (quaternion)UnityEngine.Quaternion.Euler(value.x, evt.newValue, value.z);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            {
                var xField = new FloatField("Z:");
                xField.value = value.z;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    property.boxedValue = (quaternion)UnityEngine.Quaternion.Euler(value.x, value.y, evt.newValue);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            return root;

        }

    }

}