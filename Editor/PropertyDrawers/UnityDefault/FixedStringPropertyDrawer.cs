namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;

    public abstract class FixedStringBasePropertyDrawer<T> : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var root = new VisualElement();
            var val = (T)property.boxedValue;
            var field = new TextField(property.displayName);
            field.value = val.ToString();
            field.RegisterValueChangedCallback(evt => {
                property.serializedObject.Update();
                property.boxedValue = this.Cast(evt.newValue);
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            });
            root.Add(field);
            
            return root;

        }

        protected abstract T Cast(string value);

    }
    
    [CustomPropertyDrawer(typeof(Unity.Collections.FixedString32Bytes))] public class FixedString32PropertyDrawer : FixedStringBasePropertyDrawer<Unity.Collections.FixedString32Bytes> { protected override Unity.Collections.FixedString32Bytes Cast(string value) => value; }
    [CustomPropertyDrawer(typeof(Unity.Collections.FixedString64Bytes))] public class FixedString64PropertyDrawer : FixedStringBasePropertyDrawer<Unity.Collections.FixedString64Bytes> { protected override Unity.Collections.FixedString64Bytes Cast(string value) => value; }
    [CustomPropertyDrawer(typeof(Unity.Collections.FixedString128Bytes))] public class FixedString128PropertyDrawer : FixedStringBasePropertyDrawer<Unity.Collections.FixedString128Bytes> { protected override Unity.Collections.FixedString128Bytes Cast(string value) => value; }
    [CustomPropertyDrawer(typeof(Unity.Collections.FixedString512Bytes))] public class FixedString512PropertyDrawer : FixedStringBasePropertyDrawer<Unity.Collections.FixedString512Bytes> { protected override Unity.Collections.FixedString512Bytes Cast(string value) => value; }
    [CustomPropertyDrawer(typeof(Unity.Collections.FixedString4096Bytes))] public class FixedString4096PropertyDrawer : FixedStringBasePropertyDrawer<Unity.Collections.FixedString4096Bytes> { protected override Unity.Collections.FixedString4096Bytes Cast(string value) => value; }

}