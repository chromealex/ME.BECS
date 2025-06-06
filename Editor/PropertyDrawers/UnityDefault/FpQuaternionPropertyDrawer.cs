using tfloat = sfloat;
using ME.BECS.FixedPoint;

namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;

    [CustomPropertyDrawer(typeof(quaternion))]
    public class FpQuaternionPropertyDrawer : PropertyDrawer {

        private static StyleSheet styleSheet;

        private static void LoadStyle() {
            if (styleSheet == null) {
                styleSheet = EditorUtils.LoadResource<StyleSheet>("EditorResources/FpFloatPropertyDrawers");
            }
        }

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            LoadStyle();

            var vec = (UnityEngine.Quaternion)(quaternion)property.boxedValue;
            var value = vec.eulerAngles;
            var root = new VisualElement();
            root.styleSheets.Add(styleSheet);
            root.AddToClassList("quaternion-field");
            var field = new Label(property.displayName);
            root.Add(field);
            FloatField _xField = null;
            FloatField _yField = null;
            FloatField _zField = null;
            {
                var xField = new FloatField("X:");
                _xField = xField;
                xField.value = value.x;
                xField.RegisterValueChangedCallback(evt => {
                    property.serializedObject.Update();
                    var value = (UnityEngine.Quaternion)(quaternion)property.boxedValue;
                    property.boxedValue = (quaternion)UnityEngine.Quaternion.Euler(evt.newValue, value.y, value.z);
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
                    var value = (UnityEngine.Quaternion)(quaternion)property.boxedValue;
                    property.boxedValue = (quaternion)UnityEngine.Quaternion.Euler(value.x, evt.newValue, value.z);
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
                    var value = (UnityEngine.Quaternion)(quaternion)property.boxedValue;
                    property.boxedValue = (quaternion)UnityEngine.Quaternion.Euler(value.x, value.y, evt.newValue);
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                });
                root.Add(xField);
            }
            root.AddManipulator(new ContextualMenuManipulator((evt) => {
                evt.menu.AppendAction("From sfloat to F32", (action) => {
                    var x = property.FindPropertyRelative("value").FindPropertyRelative("x").FindPropertyRelative("rawValue").uintValue;
                    var y = property.FindPropertyRelative("value").FindPropertyRelative("y").FindPropertyRelative("rawValue").uintValue;
                    var z = property.FindPropertyRelative("value").FindPropertyRelative("z").FindPropertyRelative("rawValue").uintValue;
                    var w = property.FindPropertyRelative("value").FindPropertyRelative("w").FindPropertyRelative("rawValue").uintValue;
                    unsafe {
                        var valX = *(float*)&x;
                        var valY = *(float*)&y;
                        var valZ = *(float*)&z;
                        var valW = *(float*)&w;
                        var q = new quaternion(valX, valY, valZ, valW);
                        var e = q.ToEuler();
                        _xField.value = (float)e.x;
                        _yField.value = (float)e.y;
                        _zField.value = (float)e.z;
                        property.boxedValue = q;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                    }
                });
            }));
            return root;

        }

    }

}