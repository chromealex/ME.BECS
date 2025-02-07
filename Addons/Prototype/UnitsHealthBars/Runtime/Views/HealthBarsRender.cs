#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.UnitsHealthBars {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections;

    [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
    public static class HealthBarUtils {

        public struct BarZSorting : System.Collections.Generic.IComparer<DrawHealthBarsSystem.BarItem> {
            
            public int Compare(DrawHealthBarsSystem.BarItem x, DrawHealthBarsSystem.BarItem y) {
                return y.position.y.CompareTo(x.position.y);
            }

        }

        //[BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public static void Render(tfloat referenceScale, ref UnityEngine.Color bordersColor, ref UnityEngine.Color backColor, ref UnityEngine.Color minHealthColor, ref UnityEngine.Color maxHealthColor, ref ME.BECS.NativeCollections.NativeParallelList<DrawHealthBarsSystem.BarItem> bars) {

            var scale = referenceScale > 0f ? UnityEngine.Screen.height / referenceScale : 1f;
            
            var screenRect = new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);
            var onePixelSize = ScreenToView(1f);
            var borderSize = ScreenToView(2f);
            UnityEngine.GL.Begin(UnityEngine.GL.QUADS);
            var list = bars.ToList(Unity.Collections.Allocator.Temp);
            list.Sort(new BarZSorting());
            foreach (var item in list) {
                var barRect = item;
                var barSettings = barRect.settings;
                var width = barSettings.GetWidth(scale);
                var height = barSettings.GetHeight(scale);
                var unitHeightPos = barRect.heightPosition;
                var left = barRect.position.x - ScreenToView(width * 0.5f);
                var bottom = barRect.position.y + (unitHeightPos.y - barRect.position.y) * 4f;
                var top = bottom + ScreenToView(height);
                var rect = new Rect(left, bottom, ScreenToView(width), ScreenToView(height));
                if (rect.Overlaps(screenRect) == false) continue;
                //rect.position *= 2f;
                //rect.position -= new Vector2(screenRect.width * 0.5f, screenRect.height * 0.5f);
                UnityEngine.GL.Color(backColor);
                var backRect = rect;
                backRect.xMin -= borderSize;
                backRect.yMin -= borderSize;
                backRect.xMax += borderSize;
                backRect.yMax += borderSize;
                DrawQuad(backRect, onePixelSize);
                {
                    UnityEngine.GL.Color(bordersColor);
                    DrawQuad(new Rect(left - onePixelSize, bottom, onePixelSize, ScreenToView(height)), onePixelSize);
                }
                for (int i = 0; i < barSettings.Sections; ++i) {
                    rect.width = ScreenToView(barSettings.sectionWidth * scale);
                    var color = UnityEngine.Color.Lerp(minHealthColor, maxHealthColor, (float)math.pow(barRect.healthPercent, 2));
                    if (i < barRect.barLerpIndex) {
                        UnityEngine.GL.Color(color);
                    } else if (i > barRect.barLerpIndex) {
                        UnityEngine.GL.Color(UnityEngine.Color.clear);
                    } else {
                        UnityEngine.GL.Color(UnityEngine.Color.Lerp(backColor, color, (float)barRect.barLerpValue));
                    }

                    DrawQuad(rect, onePixelSize);
                    rect.xMin += rect.width;
                    {
                        UnityEngine.GL.Color(bordersColor);
                        DrawQuad(new Rect(rect.xMin, rect.yMin, onePixelSize, ScreenToView(height)), onePixelSize);
                    }
                    rect.xMin += onePixelSize;
                }
                {
                    UnityEngine.GL.Color(bordersColor);
                    DrawQuad(new Rect(left - onePixelSize, bottom - onePixelSize, ScreenToView(width) + onePixelSize * 2f, onePixelSize), onePixelSize);
                    DrawQuad(new Rect(left - onePixelSize, top, ScreenToView(width) + onePixelSize * 2f, onePixelSize), onePixelSize);
                }
            }
            UnityEngine.GL.End();
            
        }
        
        private static tfloat ScreenToView(tfloat value) {
            return value;
        }

        private static void DrawQuad(Rect rect, tfloat onePixelSize) {

            rect.width = math.max(rect.width, onePixelSize);
            rect.height = math.max(rect.height, onePixelSize);
            UnityEngine.GL.TexCoord(new UnityEngine.Vector3(0f, 0f, 0f));
            UnityEngine.GL.Vertex(GetVertex(rect.xMin, rect.yMin));
            UnityEngine.GL.TexCoord(new UnityEngine.Vector3(0f, 1f, 0f));
            UnityEngine.GL.Vertex(GetVertex(rect.xMin, rect.yMax));
            UnityEngine.GL.TexCoord(new UnityEngine.Vector3(1f, 1f, 0f));
            UnityEngine.GL.Vertex(GetVertex(rect.xMax, rect.yMax));
            UnityEngine.GL.TexCoord(new UnityEngine.Vector3(1f, 0f, 0f));
            UnityEngine.GL.Vertex(GetVertex(rect.xMax, rect.yMin));

        }

        private static UnityEngine.Vector3 GetVertex(tfloat x, tfloat y) {
            return new UnityEngine.Vector3((float)x, (float)y, 0f);
        }

    }
    
    [UnityEngine.DefaultExecutionOrder(110)]
    public class HealthBarsRender : UnityEngine.MonoBehaviour {

        public ME.BECS.NativeCollections.NativeParallelList<DrawHealthBarsSystem.BarItem> bars;
        public UnityEngine.Material material;
        public tfloat referenceScale;
        public UnityEngine.Color bordersColor = new UnityEngine.Color(0.06f, 0.06f, 0.06f);
        public UnityEngine.Color backColor = new UnityEngine.Color(0.16f, 0.16f, 0.16f);
        public UnityEngine.Color minHealthColor = new UnityEngine.Color(1f, 0.01f, 0f);
        public UnityEngine.Color maxHealthColor = new UnityEngine.Color(0.13f, 1f, 0f, 1f);

        public void OnEnable() {
            
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += this.EndCameraRendering;

        }

        public void OnDisable() {
            
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= this.EndCameraRendering;

        }
        
        private void EndCameraRendering(UnityEngine.Rendering.ScriptableRenderContext src, UnityEngine.Camera camera) {
            if (this.CheckFilter(camera)) {
                this.DrawLines();
            }
        }
        
        protected void OnCameraRender(UnityEngine.Camera camera) {
            if (this.CheckFilter(camera)) {
                this.DrawLines();
            }
        }

        public void OnPostRender() {
            this.DrawLines();
        }

        private bool CheckFilter(UnityEngine.Camera camera) {
            return (camera.cullingMask & (1 << this.gameObject.layer)) != 0;
        }

        private void DrawLines() {

            UnityEngine.GL.PushMatrix();
            UnityEngine.GL.LoadPixelMatrix();
            this.material.SetPass(0);
            {
                HealthBarUtils.Render(this.referenceScale, ref this.bordersColor, ref this.backColor, ref this.minHealthColor, ref this.maxHealthColor, ref this.bars);
            }
            UnityEngine.GL.PopMatrix();
            
        }

    }

}