
namespace ME.BECS.UnitsHealthBars {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections;
    using UnityEngine;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
    public static class HealthBarUtils {

        public struct BarZSorting : System.Collections.Generic.IComparer<DrawHealthBarsSystem.BarItem> {
            
            public int Compare(DrawHealthBarsSystem.BarItem x, DrawHealthBarsSystem.BarItem y) {
                return y.position.y.CompareTo(x.position.y);
            }

        }

        //[BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public static void Render(ref Color bordersColor, ref Color backColor, ref Color minHealthColor, ref Color maxHealthColor, ref ME.BECS.NativeCollections.NativeParallelList<DrawHealthBarsSystem.BarItem> bars) {
            

            GL.Begin(GL.TRIANGLES);
            GL.Color(Color.red);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, Screen.height / 2, 0);
            GL.Vertex3(Screen.width / 2, Screen.height / 2, 0);
            GL.End();

            return;
            
            var screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
            var onePixelSize = ScreenToView(1f);
            var borderSize = ScreenToView(1f);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS);
            var list = bars.ToList(Unity.Collections.Allocator.Temp);
            list.Sort(new BarZSorting());
            foreach (var item in list) {
                var barRect = item;
                var barSettings = barRect.settings;
                var width = barSettings.Width;
                var height = barSettings.Height;
                var unitHeightPos = barRect.heightPosition;
                var left = barRect.position.x - ScreenToView(width * 0.5f);
                var bottom = barRect.position.y + (unitHeightPos.y - barRect.position.y) * 4f;
                var top = bottom + ScreenToView(height);
                var rect = new Rect(left, bottom, ScreenToView(width), ScreenToView(height));
                if (rect.Overlaps(screenRect) == false) continue;
                //rect.position *= 2f;
                rect.position -= new Vector2(screenRect.width * 0.5f, screenRect.height * 0.5f);
                GL.Color(backColor);
                var backRect = rect;
                backRect.xMin -= borderSize;
                backRect.yMin -= borderSize;
                backRect.xMax += borderSize;
                backRect.yMax += borderSize;
                DrawQuad(backRect, onePixelSize);
                {
                    GL.Color(bordersColor);
                    DrawQuad(new Rect(left - onePixelSize, bottom, onePixelSize, ScreenToView(barSettings.Height)), onePixelSize);
                }
                for (int i = 0; i < barSettings.Sections; ++i) {
                    rect.width = ScreenToView(barSettings.sectionWidth);
                    var color = Color.Lerp(minHealthColor, maxHealthColor, math.pow(barRect.healthPercent, 2));
                    if (i < barRect.barLerpIndex) {
                        GL.Color(color);
                    } else if (i > barRect.barLerpIndex) {
                        GL.Color(Color.clear);
                    } else {
                        GL.Color(Color.Lerp(backColor, color, barRect.barLerpValue));
                    }

                    DrawQuad(rect, onePixelSize);
                    rect.xMin += rect.width;
                    {
                        GL.Color(bordersColor);
                        DrawQuad(new Rect(rect.xMin, rect.yMin, onePixelSize, ScreenToView(barSettings.Height)), onePixelSize);
                    }
                    rect.xMin += onePixelSize;
                }
                {
                    GL.Color(bordersColor);
                    DrawQuad(new Rect(left - onePixelSize, bottom - onePixelSize, ScreenToView(barSettings.Width) + onePixelSize * 2f, onePixelSize), onePixelSize);
                    DrawQuad(new Rect(left - onePixelSize, top, ScreenToView(barSettings.Width) + onePixelSize * 2f, onePixelSize), onePixelSize);
                }
            }
            GL.End();
            GL.PopMatrix();
            
        }
        
        private static float ScreenToView(float value) {
            return value;
        }

        private static void DrawQuad(Rect rect, float onePixelSize) {

            rect.width = math.max(rect.width, onePixelSize);
            rect.height = math.max(rect.height, onePixelSize);
            GL.TexCoord(new Vector3(0f, 0f, 0f));
            GL.Vertex(GetVertex(rect.xMin, rect.yMin));
            GL.TexCoord(new Vector3(0f, 1f, 0f));
            GL.Vertex(GetVertex(rect.xMin, rect.yMax));
            GL.TexCoord(new Vector3(1f, 1f, 0f));
            GL.Vertex(GetVertex(rect.xMax, rect.yMax));
            GL.TexCoord(new Vector3(1f, 0f, 0f));
            GL.Vertex(GetVertex(rect.xMax, rect.yMin));
            
        }

        private static Vector3 GetVertex(float x, float y) {
            return new Vector3(x, y, 0f);
        }

    }
    
    [DefaultExecutionOrder(110)]
    public class HealthBarsRender : MonoBehaviour {

        public ME.BECS.NativeCollections.NativeParallelList<DrawHealthBarsSystem.BarItem> bars;
        public Material material;
        public Color bordersColor = new Color(0.06f, 0.06f, 0.06f);
        public Color backColor = new Color(0.16f, 0.16f, 0.16f);
        public Color minHealthColor = new Color(1f, 0.01f, 0f);
        public Color maxHealthColor = new Color(0.13f, 1f, 0f, 1f);

        public void OnEnable() {
            
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += this.EndCameraRendering;

        }

        public void OnDisable() {
            
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= this.EndCameraRendering;

        }
        
        private void EndCameraRendering(UnityEngine.Rendering.ScriptableRenderContext src, Camera camera) {
            if (this.CheckFilter(camera)) {
                this.DrawLines();
            }
        }
        
        protected void OnCameraRender(Camera camera) {
            if (this.CheckFilter(camera)) {
                this.DrawLines();
            }
        }
 
        private bool CheckFilter(Camera camera) {
            return (camera.cullingMask & (1 << this.gameObject.layer)) != 0;
        }

        private void DrawLines() {

            GL.PushMatrix();
            GL.LoadPixelMatrix();
            this.material.SetPass(0);
            {
                HealthBarUtils.Render(ref this.bordersColor, ref this.backColor, ref this.minHealthColor, ref this.maxHealthColor, ref this.bars);
            }
            GL.PopMatrix();
            
        }

    }

}