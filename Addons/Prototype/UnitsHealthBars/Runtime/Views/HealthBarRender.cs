namespace ME.BECS.UnitsHealthBars {

    using UnityEngine;
    using Unity.Mathematics;
    
    public class HealthBarsRender : MonoBehaviour {

        public ME.BECS.NativeCollections.NativeParallelList<DrawHealthBarsSystem.BarItem> bars;
        public Material material;
        public Color bordersColor = new Color(0.06f, 0.06f, 0.06f);
        public Color backColor = new Color(0.16f, 0.16f, 0.16f);
        public Color minHealthColor = new Color(1f, 0.01f, 0f);
        public Color maxHealthColor = new Color(0.13f, 1f, 0f, 1f);
        
        public void OnPostRender() {

            var screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
            this.material.SetPass(0);
            var onePixelSize = ScreenToView(1f);
            var borderSize = ScreenToView(1f);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS);
            var list = this.bars.ToList(Unity.Collections.Allocator.Temp);
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
                GL.Color(this.backColor);
                var backRect = rect;
                backRect.xMin -= borderSize;
                backRect.yMin -= borderSize;
                backRect.xMax += borderSize;
                backRect.yMax += borderSize;
                DrawQuad(backRect, onePixelSize);
                {
                    GL.Color(this.bordersColor);
                    DrawQuad(new Rect(left - onePixelSize, bottom, onePixelSize, ScreenToView(barSettings.Height)), onePixelSize);
                }
                for (int i = 0; i < barSettings.Sections; ++i) {
                    rect.width = ScreenToView(barSettings.sectionWidth);
                    var color = Color.Lerp(this.minHealthColor, this.maxHealthColor, math.pow(barRect.healthPercent, 2));
                    if (i < barRect.barLerpIndex) {
                        GL.Color(color);
                    } else if (i > barRect.barLerpIndex) {
                        GL.Color(Color.clear);
                    } else {
                        GL.Color(Color.Lerp(this.backColor, color, barRect.barLerpValue));
                    }

                    DrawQuad(rect, onePixelSize);
                    rect.xMin += rect.width;
                    {
                        GL.Color(this.bordersColor);
                        DrawQuad(new Rect(rect.xMin, rect.yMin, onePixelSize, ScreenToView(barSettings.Height)), onePixelSize);
                    }
                    rect.xMin += onePixelSize;
                }
                {
                    GL.Color(this.bordersColor);
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

}