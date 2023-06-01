namespace ME.BECS.Editor {
    
    using UnityEngine;
    using UnityEngine.UIElements;
    
    /// <summary>
    /// Resizes a <see cref="TextElement"/> font size with respect to its <see cref="VisualElement.contentRect"/>.
    /// </summary>
    public static class TextElementAutoFitter
    {
        /// <summary>
        /// Registers callbacks for the <see cref="TextElement"/> to update its font size.
        /// </summary>
        public static void RegisterAutoFitCallbacks(TextElement textElement)
        {
            textElement.RegisterCallback<AttachToPanelEvent, TextElement>(OnAttachToPanel, textElement);
            textElement.RegisterCallback<DetachFromPanelEvent, TextElement>(OnDetachFromPanel, textElement);
        }
 
        private static void OnAttachToPanel(AttachToPanelEvent _, TextElement textElement)
        {
            textElement.RegisterCallback<ChangeEvent<string>, TextElement>(OnChange, textElement);
            textElement.RegisterCallback<ChangeEvent<StyleFont>, TextElement>(OnChange, textElement);
            textElement.RegisterCallback<ChangeEvent<StyleFontDefinition>, TextElement>(OnChange, textElement);
            textElement.RegisterCallback<ChangeEvent<StyleLength>, TextElement>(OnChange, textElement);
            textElement.RegisterCallback<GeometryChangedEvent, TextElement>(OnChange, textElement);
            textElement.UnregisterCallback<AttachToPanelEvent, TextElement>(OnAttachToPanel);
            // and any other callbacks that may impact scaling that I may have missed
        }
 
        private static void OnDetachFromPanel(DetachFromPanelEvent _, TextElement textElement)
        {
            textElement.UnregisterCallback<ChangeEvent<string>, TextElement>(OnChange);
            textElement.UnregisterCallback<ChangeEvent<StyleFont>, TextElement>(OnChange);
            textElement.UnregisterCallback<ChangeEvent<StyleFontDefinition>, TextElement>(OnChange);
            textElement.UnregisterCallback<ChangeEvent<StyleLength>, TextElement>(OnChange);
            textElement.UnregisterCallback<GeometryChangedEvent, TextElement>(OnChange);
            textElement.UnregisterCallback<DetachFromPanelEvent, TextElement>(OnDetachFromPanel);
        }
 
        /// <summary>
        /// Resize the text element when geometry is changed.
        /// </summary>
        internal static void UpdateFontSize(TextElement textElement)
        {
            if (textElement.text == string.Empty) return;
     
            var textSize = textElement.MeasureTextSize(
                textElement.text,
                textElement.contentRect.width,
                VisualElement.MeasureMode.Undefined,
                textElement.contentRect.height,
                VisualElement.MeasureMode.Undefined
            );
            var fontSize = Mathf.Max(textElement.resolvedStyle.fontSize, 1);
            var targetFontSize = Mathf.FloorToInt(Mathf.Max(
                Mathf.Min(Mathf.Abs(textElement.contentRect.height / textSize.y * fontSize), Mathf.Abs(textElement.contentRect.width / textSize.x * fontSize)), 1
            ));
     
            if (Mathf.FloorToInt(textSize.y) == targetFontSize) return;
            textElement.style.fontSize = new StyleLength(new Length(targetFontSize));
        }
 
        /// <summary>
        /// Generic callback handler use to <see cref="UpdateFontSize"/>.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        private static void OnChange<TEvent>(TEvent eventBase, TextElement textElement) where TEvent: EventBase<TEvent>, new()
        {
            // eventBase.StopPropagation(); I don't think this is needed but leaving it in
            UpdateFontSize(textElement);
        }
    }

    /// <summary>
    /// Auto-sizing <see cref="Label"/>.
    /// </summary>
    public class LabelAutoFit : Label
    {
        [UnityEngine.Scripting.Preserve] public new class UxmlFactory : UxmlFactory<LabelAutoFit, UxmlTraits>{}
     
        public LabelAutoFit() => TextElementAutoFitter.RegisterAutoFitCallbacks(this);
    }
    
    /// <summary>
    /// Auto-sizing <see cref="Button"/>.
    /// </summary>
    public class ButtonAutoFit : Button
    {
        [UnityEngine.Scripting.Preserve]
        public new class UxmlFactory : UxmlFactory<ButtonAutoFit, UxmlTraits>
        {
            public override string uxmlNamespace => "Custom"; // neat tip for anyone who doesn't know
        }
 
        public ButtonAutoFit(System.Action clickEvent) : base(clickEvent) {
            TextElementAutoFitter.UpdateFontSize(this);
            //TextElementAutoFitter.RegisterAutoFitCallbacks(this);
            // bug - button alignment becomes broken
            //style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.UpperCenter);
        }
        public ButtonAutoFit(): this(null) {}
    }

}