namespace ME.BECS.Units.Editor {

    using UnityEngine.UIElements;

    [UnityEditor.CustomPropertyDrawer(typeof(ME.BECS.Units.LayerMask))]
    public class LayerMaskPropertyDrawer : UnityEditor.PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(UnityEditor.SerializedProperty property) {

            var maskSp = property.FindPropertyRelative(nameof(ME.BECS.Units.LayerMask.mask));
            var maskFields = new UnityEditor.UIElements.MaskField("Layer Mask", LayerAliasUtils.GetAliases(), (int)maskSp.uintValue);
            maskFields.RegisterValueChangedCallback(evt => {
                maskSp.uintValue = (uint)evt.newValue; 
                maskSp.serializedObject.ApplyModifiedProperties();
            });
            return maskFields;
        }

    }

}