using tfloat = sfloat;
using ME.BECS.FixedPoint;

namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;

    [CustomPropertyDrawer(typeof(float3))]
    public class FpFloat3PropertyDrawer : PropertyDrawer {

        private static StyleSheet styleSheet;

        private static void LoadStyle() {
            if (styleSheet == null) {
                styleSheet = EditorUtils.LoadResource<StyleSheet>("EditorResources/FpFloatPropertyDrawers");
            }
        }

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            LoadStyle();

            var vec = (UnityEngine.Vector3)(float3)property.boxedValue;
            var value = vec;
            var root = new VisualElement();
            root.styleSheets.Add(styleSheet);
            root.AddToClassList("vector-field");
            var field = new Label(property.displayName);
            root.Add(field);
            {
                var xField = new FloatField("X:");
                xField.value = value.x;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    var value = (UnityEngine.Vector3)(float3)property.boxedValue;
                    property.boxedValue = new float3(evt.newValue, value.y, value.z);
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
                    var value = (UnityEngine.Vector3)(float3)property.boxedValue;
                    property.boxedValue = new float3(value.x, evt.newValue, value.z);
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
                    var value = (UnityEngine.Vector3)(float3)property.boxedValue;
                    property.boxedValue = new float3(value.x, value.y, evt.newValue);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            return root;

        }

    }

}