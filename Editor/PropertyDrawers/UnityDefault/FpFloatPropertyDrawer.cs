using tfloat = sfloat;
using ME.BECS.FixedPoint;

namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;

    [CustomPropertyDrawer(typeof(sfloat))]
    public class FpFloatPropertyDrawer : PropertyDrawer {

        public string customName;
        public System.Func<sfloat, sfloat> onValueSet;
        public System.Func<sfloat, sfloat> onValueChanged;
        
        private static StyleSheet styleSheet;

        private static void LoadStyle() {
            if (styleSheet == null) {
                styleSheet = EditorUtils.LoadResource<StyleSheet>("EditorResources/FpFloatPropertyDrawers");
            }
        }

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            LoadStyle();

            var root = new VisualElement();
            var vec = (sfloat)property.boxedValue;
            var value = vec;
            root.AddToClassList("fp-field");
            root.styleSheets.Add(styleSheet);
            var xField = new FloatField(this.customName ?? property.displayName);
            xField.value = this.onValueSet != null ? (float)this.onValueSet?.Invoke(value) : (float)value;
            xField.RegisterValueChangedCallback(evt => {
                property.serializedObject.Update();
                property.boxedValue = this.onValueChanged != null ? this.onValueChanged.Invoke((sfloat)evt.newValue) : (sfloat)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            });
            root.Add(xField);
            
            return root;

        }

    }

}