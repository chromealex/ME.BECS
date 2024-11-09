namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEngine.UIElements;
    using Unity.Mathematics;
    
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
                root.Add(CreateField(new FloatField(), property, (p) => math.sqrt(p.floatValue), (p, v) => p.floatValue = v * v));
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