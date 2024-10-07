namespace ME.BECS {

    using UnityEngine;
    
    public class TooltipAttribute : PropertyAttribute {

        public string text;
        public TooltipAttribute(string text) {
            this.text = text;
        }

    }

}