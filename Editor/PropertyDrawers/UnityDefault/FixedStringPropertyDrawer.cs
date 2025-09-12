namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;
    using Unity.Collections;
    using CPD = UnityEditor.CustomPropertyDrawer;
    
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
    
    [CPD(typeof(FixedString32Bytes))]   public class FixedString32PropertyDrawer   : FixedStringBasePropertyDrawer<FixedString32Bytes>   { protected override FixedString32Bytes Cast(string value) => value; }
    [CPD(typeof(FixedString64Bytes))]   public class FixedString64PropertyDrawer   : FixedStringBasePropertyDrawer<FixedString64Bytes>   { protected override FixedString64Bytes Cast(string value) => value; }
    [CPD(typeof(FixedString128Bytes))]  public class FixedString128PropertyDrawer  : FixedStringBasePropertyDrawer<FixedString128Bytes>  { protected override FixedString128Bytes Cast(string value) => value; }
    [CPD(typeof(FixedString512Bytes))]  public class FixedString512PropertyDrawer  : FixedStringBasePropertyDrawer<FixedString512Bytes>  { protected override FixedString512Bytes Cast(string value) => value; }
    [CPD(typeof(FixedString4096Bytes))] public class FixedString4096PropertyDrawer : FixedStringBasePropertyDrawer<FixedString4096Bytes> { protected override FixedString4096Bytes Cast(string value) => value; }

}