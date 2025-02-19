namespace ME.BECS.Addons.Prototype.Attack.Editor {

    using UnityEngine.UIElements;

    [UnityEditor.CustomPropertyDrawer(typeof(ME.BECS.Units.Layer))]
    public class LayerPropertyDrawer : UnityEditor.PropertyDrawer {

        private const string ZERO_ALIAS = "None";

        private static System.Collections.Generic.List<string> choises;

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(UnityEditor.SerializedProperty property) {

            if (choises == null) {
                choises = new System.Collections.Generic.List<string> {
                    ZERO_ALIAS,
                };
                choises.AddRange(ME.BECS.Units.Editor.LayerAliasUtils.GetAliases());
            }
            
            var valueSp = property.FindPropertyRelative(nameof(ME.BECS.Units.Layer.value));
            var enumField = new UnityEngine.UIElements.DropdownField("Layer", choises, valueSp.uintValue == 0u ? 0 : ME.BECS.Units.Editor.LayerAliasUtils.GetIndexOf(valueSp.uintValue) + 1);
            enumField.RegisterValueChangedCallback(evt => {
                
                valueSp.uintValue = evt.newValue.Equals(ZERO_ALIAS) ? 0 : ME.BECS.Units.Editor.LayerAliasUtils.GetLayerByAlias(evt.newValue).value;
                valueSp.serializedObject.ApplyModifiedProperties();
            });

            return enumField;
        }

    }
    
}