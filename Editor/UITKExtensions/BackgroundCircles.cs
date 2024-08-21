namespace ME.BECS.Editor {
    
    using UnityEngine;
    using UnityEngine.UIElements;

    public class BackgroundCircles : VisualElement {

        private static readonly CustomStyleProperty<Color> styleColor = new CustomStyleProperty<Color>("--lines-color");
        
        public class Circle : VisualElement {

            public BackgroundCircles root;
            public int offsetIndex;
            
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
        
        public Vector2 center;
        public float scale;
        public float lineWidth = 1f;
        public Color color = Color.white;
        public float[] radiuses;

        private Circle[] circles;
        
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
                        this.Add(this.circles[i]);
                    }
                }
            }
            
        }

        public void SetDirty() {
            this.MarkDirtyRepaint();
            for (int i = 0; i < this.circles.Length; ++i) {
                this.circles[i].MarkDirtyRepaint();
            }
        }
        
    }

}