namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEditor.UIElements;
    
    [CustomPropertyDrawer(typeof(TooltipAttribute))]
    public class TooltipAttributeDrawer : DecoratorDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI() {

            var attr = (TooltipAttribute)this.attribute;
            var container = new UnityEngine.UIElements.VisualElement();
            container.AddToClassList("tooltip-decorator");
            EditorUIUtils.DrawTooltip(container, attr.text);
            return container;

        }
        
    }

}