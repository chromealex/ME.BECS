#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEngine.UIElements;
    
    [CustomPropertyDrawer(typeof(ValueSqrAttribute))]
    public class ValueSqrDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var root = new VisualElement();
            var type = this.fieldInfo.FieldType;
            if (type == typeof(int)) {
                root.Add(CreateField(new IntegerField(), property, (p) => (int)math.sqrt(p.intValue), (p, v) => p.intValue = v * v));
            } else if (type == typeof(uint)) {
                root.Add(CreateField(new UnsignedIntegerField(), property, (p) => (uint)math.sqrt(p.uintValue), (p, v) => p.uintValue = v * v));
            } else if (type == typeof(long)) {
                root.Add(CreateField(new LongField(), property, (p) => (long)math.sqrt(p.longValue), (p, v) => p.longValue = v * v));
            } else if (type == typeof(ulong)) {
                root.Add(CreateField(new UnsignedLongField(), property, (p) => (ulong)math.sqrt(p.ulongValue), (p, v) => p.ulongValue = v * v));
            } else if (type == typeof(float)) {
                root.Add(CreateField(new FloatField(), property, (p) => (float)math.sqrt(p.floatValue), (p, v) => p.floatValue = (float)(v * v)));
            } else if (type == typeof(sfloat)) {
                root.Add(CreateField(new FloatField(), property, (p) => (float)ME.BECS.FixedPoint.math.sqrt((sfloat)p.boxedValue), (p, v) => p.boxedValue = (sfloat)(v * v)));
            } else {
                var label = new Label($"{property.displayName} type {type} not supported for sqr attribute");
                root.Add(label);
            }
            return root;

        }

        private static VisualElement CreateField<TField, TValue>(TField field, SerializedProperty property, System.Func<SerializedProperty, TValue> getValue, System.Action<SerializedProperty, TValue> setValue) where TField : TextValueField<TValue> {
            var displayName = property.displayName.Replace("Sqr", string.Empty).Replace("Sq", string.Empty);
            field.label = displayName;
            field.SetValueWithoutNotify(getValue(property));
            field.RegisterValueChangedCallback((evt) => {
                property.serializedObject.Update();
                setValue.Invoke(property, evt.newValue);
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            });
            return field;
        }

    }

}