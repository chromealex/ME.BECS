namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;
    using Unity.Collections;
    using CPD = UnityEditor.CustomPropertyDrawer;
    
    /// <summary>
    /// Draws fixed string base property values in the Unity Inspector.
    /// </summary>
    public abstract class FixedStringBasePropertyDrawer<T> : PropertyDrawer {

        /// <summary>
        /// Builds the UI Toolkit editor for the supplied serialized property.
        /// </summary>
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

        /// <summary>
        /// Reinterprets the value using the requested target type.
        /// </summary>
        protected abstract T Cast(string value);

    }
    
    /// <summary>
    /// Draws fixed string32 property values in the Unity Inspector.
    /// </summary>
    [CPD(typeof(FixedString32Bytes))]   public class FixedString32PropertyDrawer   : FixedStringBasePropertyDrawer<FixedString32Bytes>   {
        /// <summary>
        /// Reinterprets the value using the requested target type.
        /// </summary>
        protected override FixedString32Bytes Cast(string value) => value; }
    /// <summary>
    /// Draws fixed string64 property values in the Unity Inspector.
    /// </summary>
    [CPD(typeof(FixedString64Bytes))]   public class FixedString64PropertyDrawer   : FixedStringBasePropertyDrawer<FixedString64Bytes>   {
        /// <summary>
        /// Reinterprets the value using the requested target type.
        /// </summary>
        protected override FixedString64Bytes Cast(string value) => value; }
    /// <summary>
    /// Draws fixed string128 property values in the Unity Inspector.
    /// </summary>
    [CPD(typeof(FixedString128Bytes))]  public class FixedString128PropertyDrawer  : FixedStringBasePropertyDrawer<FixedString128Bytes>  {
        /// <summary>
        /// Reinterprets the value using the requested target type.
        /// </summary>
        protected override FixedString128Bytes Cast(string value) => value; }
    /// <summary>
    /// Draws fixed string512 property values in the Unity Inspector.
    /// </summary>
    [CPD(typeof(FixedString512Bytes))]  public class FixedString512PropertyDrawer  : FixedStringBasePropertyDrawer<FixedString512Bytes>  {
        /// <summary>
        /// Reinterprets the value using the requested target type.
        /// </summary>
        protected override FixedString512Bytes Cast(string value) => value; }
    /// <summary>
    /// Draws fixed string4096 property values in the Unity Inspector.
    /// </summary>
    [CPD(typeof(FixedString4096Bytes))] public class FixedString4096PropertyDrawer : FixedStringBasePropertyDrawer<FixedString4096Bytes> {
        /// <summary>
        /// Reinterprets the value using the requested target type.
        /// </summary>
        protected override FixedString4096Bytes Cast(string value) => value; }

}