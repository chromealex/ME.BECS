namespace ME.BECS.Editor {
    
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Defines background circles state and operations.
    /// </summary>
    public class BackgroundCircles : VisualElement {

        private static readonly CustomStyleProperty<Color> styleColor = new CustomStyleProperty<Color>("--lines-color");
        
        /// <summary>
        /// Defines circle state and operations for <c>BackgroundCircles</c>.
        /// </summary>
        public class Circle : VisualElement {

            /// <summary>
            /// Root entry of the represented hierarchy.
            /// </summary>
            public BackgroundCircles root;
            /// <summary>
            /// Offset index used to locate the associated entry.
            /// </summary>
            public int offsetIndex;
            
            /// <summary>
            /// Initializes <c>Circle</c> from the supplied defaults.
            /// </summary>
            public Circle() {
                this.generateVisualContent += this.OnGenerateVisualContent;
                this.pickingMode = PickingMode.Ignore;
            }

            private void OnGenerateVisualContent(MeshGenerationContext mgc) {

                var painter = mgc.painter2D;

                painter.strokeColor = this.root.color;
                painter.lineJoin = LineJoin.Miter;
                painter.lineCap = LineCap.Butt;
                painter.lineWidth = this.root.lineWidth;
                var radius = this.offsetIndex < this.root.radiuses.Length ? this.root.radiuses[this.offsetIndex] * this.root.scale : 0f;
                if (radius <= 0f) return;
                painter.BeginPath();
                painter.Arc(this.root.center, radius, new Angle(0f), new Angle(360f), ArcDirection.Clockwise);
                painter.ClosePath();
                painter.Stroke();
            
            }
            
        }
        
        /// <summary>
        /// Center of the represented bounds.
        /// </summary>
        public Vector2 center;
        /// <summary>
        /// Scale used by <c>BackgroundCircles</c>.
        /// </summary>
        public float scale;
        /// <summary>
        /// Line width used by <c>BackgroundCircles</c>.
        /// </summary>
        public float lineWidth = 1f;
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public Color color = Color.white;
        /// <summary>
        /// Radiuses used by <c>BackgroundCircles</c>.
        /// </summary>
        public float[] radiuses;

        private Circle[] circles;
        
        /// <summary>
        /// Initializes <c>BackgroundCircles</c> from the supplied count.
        /// </summary>
        public BackgroundCircles(int count) {
            this.circles = new Circle[count];
            for (int i = 0; i < count; ++i) {
                this.circles[i] = new Circle() {
                    root = this,
                    offsetIndex = i,
                };
                this.Add(this.circles[i]);
            }
            this.pickingMode = PickingMode.Ignore;
            this.generateVisualContent += this.OnGenerateVisualContent;
            this.RegisterCallback<CustomStyleResolvedEvent>(this.OnStylesResolved);
        }

        private void OnStylesResolved(CustomStyleResolvedEvent evt) {
            
            if (evt.customStyle.TryGetValue(styleColor, out var color)) {
                this.color = color;
            }
        }
        
        private void OnGenerateVisualContent(MeshGenerationContext mgc) {

            if (this.radiuses.Length > this.circles.Length) {
                System.Array.Resize(ref this.circles, this.radiuses.Length);
                for (int i = 0; i < this.radiuses.Length; ++i) {
                    if (this.circles[i] == null) {
                        this.circles[i] = new Circle() {
                            root = this,
                            offsetIndex = i,
                        };
                        var idx = i;
                        UnityEditor.EditorApplication.delayCall += () => this.Add(this.circles[idx]);
                    }
                }
            }
            
        }

        /// <summary>
        /// Sets dirty.
        /// </summary>
        public void SetDirty() {
            this.MarkDirtyRepaint();
            for (int i = 0; i < this.circles.Length; ++i) {
                this.circles[i]?.MarkDirtyRepaint();
            }
        }
        
    }

}