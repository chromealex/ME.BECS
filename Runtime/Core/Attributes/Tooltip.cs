namespace ME.BECS {

    using UnityEngine;
    
    /// <summary>
    /// Supplies tooltip metadata to annotated declarations.
    /// </summary>
    public class TooltipAttribute : PropertyAttribute {

        /// <summary>
        /// Text displayed or processed by this entry.
        /// </summary>
        public string text;
        /// <summary>
        /// Initializes <c>TooltipAttribute</c> from the supplied text.
        /// </summary>
        public TooltipAttribute(string text) {
            this.text = text;
        }

    }

}