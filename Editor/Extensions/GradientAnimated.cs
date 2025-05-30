namespace ME.BECS.Editor {

    using UnityEngine.UIElements;
    using UnityEngine;
    
    public class GradientAnimated : VisualElement {
        
        private static CustomStyleProperty<UnityEngine.Color> leftColorProp = new CustomStyleProperty<UnityEngine.Color>("--left-color");
        private static CustomStyleProperty<UnityEngine.Color> rightColorProp = new CustomStyleProperty<UnityEngine.Color>("--right-color");
        private static CustomStyleProperty<UnityEngine.Color> leftTopColorProp = new CustomStyleProperty<UnityEngine.Color>("--left-top-color");
        private static CustomStyleProperty<UnityEngine.Color> rightTopColorProp = new CustomStyleProperty<UnityEngine.Color>("--right-top-color");
        private static CustomStyleProperty<UnityEngine.Color> leftBottomColorProp = new CustomStyleProperty<UnityEngine.Color>("--left-bottom-color");
        private static CustomStyleProperty<UnityEngine.Color> rightBottomColorProp = new CustomStyleProperty<UnityEngine.Color>("--right-bottom-color");
        private static CustomStyleProperty<float> speedProp = new CustomStyleProperty<float>("--speed");

        private Color leftTop;
        private Color rightTop;
        private Color leftBottom;
        private Color rightBottom;
        private bool animate;
        private IVisualElementScheduledItem updateItem;
        private float offset;
        private float speed;
        private bool stopOnNext;

        private static readonly Vertex[] vertices = new Vertex[4];
        private static readonly Vertex[] animVertices = new Vertex[4];
        private static readonly Vertex[] animVerticesBack = new Vertex[4];
        private static readonly ushort[] indexes = { 0, 1, 2, 2, 3, 0 };

        public GradientAnimated() {
            this.generateVisualContent += this.OnGenerateVisualContent;
            this.RegisterCallback<CustomStyleResolvedEvent>(new EventCallback<CustomStyleResolvedEvent>(this.OnCustomStyleResolved));
        }
        
        private void OnCustomStyleResolved(CustomStyleResolvedEvent e) {
            var customStyle = e.customStyle;
            if (customStyle.TryGetValue(leftColorProp, out var left) == true) {
                this.leftTop = left;
                this.leftBottom = left;
            }
            if (customStyle.TryGetValue(rightColorProp, out var right) == true) {
                this.rightTop = right;
                this.rightBottom = right;
            }
            if (customStyle.TryGetValue(leftTopColorProp, out var leftTop) == true) this.leftTop = leftTop;
            if (customStyle.TryGetValue(rightTopColorProp, out var rightTop) == true) this.rightTop = rightTop;
            if (customStyle.TryGetValue(leftBottomColorProp, out var leftBottom) == true) this.leftBottom = leftBottom;
            if (customStyle.TryGetValue(rightBottomColorProp, out var rightBottom) == true) this.rightBottom = rightBottom;
            
            if (customStyle.TryGetValue(speedProp, out var speed) == true) this.speed = speed;
        }
        
        private void OnGenerateVisualContent(MeshGenerationContext mgc) {
            var r = this.contentRect;
            if (r.width < 0.01f || r.height < 0.01f) return;

            vertices[0].tint = this.leftTop;
            vertices[1].tint = this.leftTop;
            vertices[2].tint = this.leftTop;
            vertices[3].tint = this.leftTop;

            animVertices[0].tint = this.leftBottom;
            animVertices[1].tint = this.leftTop;
            animVertices[2].tint = this.rightTop;
            animVertices[3].tint = this.rightBottom;
            
            animVerticesBack[0].tint = this.rightBottom;
            animVerticesBack[1].tint = this.rightTop;
            animVerticesBack[2].tint = this.leftTop;
            animVerticesBack[3].tint = this.leftBottom;
            
            float left = 0f;
            float right = r.width;
            float top = 0f;
            float bottom = r.height;

            vertices[0].position = new UnityEngine.Vector3(left, bottom, Vertex.nearZ);
            vertices[1].position = new UnityEngine.Vector3(left, top, Vertex.nearZ);
            vertices[2].position = new UnityEngine.Vector3(right, top, Vertex.nearZ);
            vertices[3].position = new UnityEngine.Vector3(right, bottom, Vertex.nearZ);

            animVertices[0].position = new UnityEngine.Vector3(left + this.offset, bottom, Vertex.nearZ);
            animVertices[1].position = new UnityEngine.Vector3(left + this.offset, top, Vertex.nearZ);
            animVertices[2].position = new UnityEngine.Vector3(right * 0.5f + this.offset, top, Vertex.nearZ);
            animVertices[3].position = new UnityEngine.Vector3(right * 0.5f + this.offset, bottom, Vertex.nearZ);
            
            animVerticesBack[0].position = new UnityEngine.Vector3(right * 0.5f + this.offset, bottom, Vertex.nearZ);
            animVerticesBack[1].position = new UnityEngine.Vector3(right * 0.5f + this.offset, top, Vertex.nearZ);
            animVerticesBack[2].position = new UnityEngine.Vector3(right + this.offset, top, Vertex.nearZ);
            animVerticesBack[3].position = new UnityEngine.Vector3(right + this.offset, bottom, Vertex.nearZ);

            { // bg
                MeshWriteData mwd = mgc.Allocate(vertices.Length, indexes.Length);
                mwd.SetAllVertices(vertices);
                mwd.SetAllIndices(indexes);
            }

            {
                MeshWriteData mwd = mgc.Allocate(animVertices.Length, indexes.Length);
                mwd.SetAllVertices(animVertices);
                mwd.SetAllIndices(indexes);
            }
            {
                MeshWriteData mwd = mgc.Allocate(animVerticesBack.Length, indexes.Length);
                mwd.SetAllVertices(animVerticesBack);
                mwd.SetAllIndices(indexes);
            }
        }

        public void ThinkStart() {
            if (this.animate == true) return;
            this.animate = true;
            this.stopOnNext = false;
            this.updateItem ??= this.schedule.Execute(this.Update).Every(1);
            this.updateItem.Resume();
        }

        public void ThinkEnd() {
            if (this.animate == false) return;
            this.animate = false;
            this.stopOnNext = false;
        }
        
        public void ThinkOnce() {
            if (this.animate == true) return;
            this.ThinkStart();
            this.ThinkEnd();
        }

        private void Update(TimerState timer) {
            this.offset += (timer.deltaTime / 1000f) * this.speed;
            if (this.animate == false && this.stopOnNext == true) {
                if (this.offset >= 0f) {
                    this.offset = 0f;
                    this.updateItem.Pause();
                    return;
                }
            }
            var r = this.contentRect;
            if (this.offset > r.width) {
                this.offset = -r.width;
                if (this.animate == false) this.stopOnNext = true;
            }
            this.ApplyOffset();
        }

        private void ApplyOffset() {
            this.MarkDirtyRepaint();
        }

    }
    
}