using tfloat = sfloat;
using ME.BECS.FixedPoint;

namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;

    [CustomPropertyDrawer(typeof(float4))]
    public class FpFloat4PropertyDrawer : PropertyDrawer {

        private static StyleSheet styleSheet;

        private static void LoadStyle() {
            if (styleSheet == null) {
                styleSheet = EditorUtils.LoadResource<StyleSheet>("EditorResources/FpFloatPropertyDrawers");
            }
        }

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            LoadStyle();

            var vec = (UnityEngine.Vector4)(float4)property.boxedValue;
            var value = vec;
            var root = new VisualElement();
            root.styleSheets.Add(styleSheet);
            root.AddToClassList("vector-field");
            var field = new Label(property.displayName);
            root.Add(field);
            FloatField _xField = null;
            FloatField _yField = null;
            FloatField _zField = null;
            FloatField _wField = null;
            {
                var xField = new FloatField("X:");
                _xField = xField;
                xField.value = value.x;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    var value = (UnityEngine.Vector4)(float4)property.boxedValue;
                    property.boxedValue = new float4(evt.newValue, value.y, value.z, value.w);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            {
                var xField = new FloatField("Y:");
                _yField = xField;
                xField.value = value.y;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    var value = (UnityEngine.Vector4)(float4)property.boxedValue;
                    property.boxedValue = new float4(value.x, evt.newValue, value.z, value.w);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            {
                var xField = new FloatField("Z:");
                _zField = xField;
                xField.value = value.z;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    var value = (UnityEngine.Vector4)(float4)property.boxedValue;
                    property.boxedValue = new float4(value.x, value.y, evt.newValue, value.w);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            {
                var xField = new FloatField("W:");
                _wField = xField;
                xField.value = value.w;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    var value = (UnityEngine.Vector4)(float4)property.boxedValue;
                    property.boxedValue = new float4(value.x, value.y, value.w, evt.newValue);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            root.AddManipulator(new ContextualMenuManipulator((evt) => {
                evt.menu.AppendAction("From sfloat to F32", (action) => {
                    var x = property.FindPropertyRelative("x").FindPropertyRelative("rawValue").uintValue;
                    var y = property.FindPropertyRelative("y").FindPropertyRelative("rawValue").uintValue;
                    var z = property.FindPropertyRelative("z").FindPropertyRelative("rawValue").uintValue;
                    var w = property.FindPropertyRelative("w").FindPropertyRelative("rawValue").uintValue;
                    unsafe {
                        var valX = *(float*)&x;
                        var valY = *(float*)&y;
                        var valZ = *(float*)&z;
                        var valW = *(float*)&w;
                        _xField.value = valX;
                        _yField.value = valY;
                        _zField.value = valZ;
                        _wField.value = valW;
                        property.boxedValue = new float4(valX, valY, valZ, valW);
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                    }
                });
            }));
            return root;

        }

    }

}