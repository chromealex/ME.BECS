namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEditor.UIElements;
    
    /// <summary>
    /// Draws tooltip attribute values in the Unity Inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(TooltipAttribute))]
    public class TooltipAttributeDrawer : DecoratorDrawer {

        /// <summary>
        /// Builds the UI Toolkit editor for the supplied serialized property.
        /// </summary>
        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI() {

            var attr = (TooltipAttribute)this.attribute;
            var container = new UnityEngine.UIElements.VisualElement();
            container.AddToClassList("tooltip-decorator");
            EditorUIUtils.DrawTooltip(container, attr.text);
            return container;

        }
        
    }

}