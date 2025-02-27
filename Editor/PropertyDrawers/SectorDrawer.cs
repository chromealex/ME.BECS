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

namespace ME.BECS.Attack.Editor {

    using UnityEditor;
    using UnityEngine.UIElements;
    using ME.BECS.Editor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(Sector))]
    public class SectorDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        private static StyleSheet styleSheet;

        private static void LoadStyle() {
            if (SectorDrawer.styleSheetBase == null) {
                SectorDrawer.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/SectorPropertyDrawer.uss");
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            SectorDrawer.LoadStyle();

            var root = new VisualElement();
            root.styleSheets.Add(SectorDrawer.styleSheetBase);
            root.AddToClassList("root");
            var label = new Label(property.displayName);
            root.Add(label);
            SectorPreview sectorPreview = null;
            {
                sectorPreview = new SectorPreview();
                sectorPreview.AddToClassList("preview");
                root.Add(sectorPreview);
            }
            FloatField minField = null;
            FloatField maxField = null;
            {
                #if FIXED_POINT
                var drawer = new FpFloatPropertyDrawer();
                drawer.customName = "Min Range";
                drawer.onValueSet = (value) => {
                    if (value > 0u) value = math.sqrt(value);
                    sectorPreview.minRange = (float)value;
                    sectorPreview.RedrawElement();
                    return value;
                };
                drawer.onValueChanged = (value) => {
                    if (value > maxField.value) {
                        maxField.SetValueWithoutNotify((float)value);
                        sectorPreview.range = (float)value;
                    }
                    sectorPreview.minRange = (float)value;
                    sectorPreview.RedrawElement();
                    return value * value;
                };
                var visualElement = drawer.CreatePropertyGUI(property.FindPropertyRelative(nameof(Sector.minRangeSqr)));
                minField = visualElement.Q<FloatField>();
                root.Add(visualElement);
                #else
                var rangeProp = property.FindPropertyRelative(nameof(Sector.minRangeSqr));
                var rangeField = minField = new FloatField("Min Range");
                rangeField.AddToClassList("range");
                rangeField.value = rangeProp.floatValue > 0f ? math.sqrt(rangeProp.floatValue) : 0f;
                sectorPreview.minRange = rangeField.value;
                rangeField.RegisterValueChangedCallback((evt) => {
                    var val = evt.newValue;
                    if (val <= 0f) val = 0f;
                    rangeField.SetValueWithoutNotify(val);
                    if (val > maxField.value) {
                        maxField.SetValueWithoutNotify(val);
                    }
                    rangeProp.serializedObject.Update();
                    rangeProp.floatValue = evt.newValue * evt.newValue;
                    sectorPreview.minRange = evt.newValue;
                    sectorPreview.RedrawElement();
                    rangeProp.serializedObject.ApplyModifiedProperties();
                    rangeProp.serializedObject.Update();
                });
                root.Add(rangeField);
                #endif
            }
            {
                #if FIXED_POINT
                var drawer = new FpFloatPropertyDrawer();
                drawer.customName = "Range";
                drawer.onValueSet = (value) => {
                    if (value > 0u) value = math.sqrt(value);
                    sectorPreview.range = (float)value;
                    sectorPreview.RedrawElement();
                    return value;
                };
                drawer.onValueChanged = (value) => {
                    if (value < minField.value) {
                        minField.SetValueWithoutNotify((float)value);
                        sectorPreview.minRange = (float)value;
                    }
                    sectorPreview.range = (float)value;
                    sectorPreview.RedrawElement();
                    return value * value;
                };
                var visualElement = drawer.CreatePropertyGUI(property.FindPropertyRelative(nameof(Sector.rangeSqr)));
                maxField = visualElement.Q<FloatField>();
                root.Add(visualElement);
                #else
                var rangeProp = property.FindPropertyRelative(nameof(Sector.rangeSqr));
                var rangeField = maxField = new FloatField("Range");
                rangeField.AddToClassList("range");
                rangeField.value = rangeProp.floatValue > 0f ? math.sqrt(rangeProp.floatValue) : 0f;
                sectorPreview.range = rangeField.value;
                rangeField.RegisterValueChangedCallback((evt) => {
                    var val = evt.newValue;
                    if (val <= 0f) val = 0f;
                    rangeField.SetValueWithoutNotify(val);
                    if (val < minField.value) {
                        minField.SetValueWithoutNotify(val);
                    }
                    rangeProp.serializedObject.Update();
                    rangeProp.floatValue = val * val;
                    sectorPreview.range = val;
                    sectorPreview.RedrawElement();
                    rangeProp.serializedObject.ApplyModifiedProperties();
                    rangeProp.serializedObject.Update();
                });
                root.Add(rangeField);
                #endif
            }

            var sectorRoot = new VisualElement();
            sectorRoot.AddToClassList("sector-root");
            root.Add(sectorRoot);
            {
                #if FIXED_POINT
                var valProp = property.FindPropertyRelative(nameof(Sector.sector));
                var prop = (sfloat)valProp.boxedValue;
                var sector = new Slider("Sector", 0f, 360f, pageSize: 1f);
                var sectorValueField = new FloatField();
                sectorValueField.AddToClassList("sector-field");
                sector.AddToClassList("sector");
                sector.value = (float)prop;
                sector.label = $"Sector ({sector.value}\u00b0):";
                sectorPreview.angle = sector.value;
                sector.RegisterValueChangedCallback((evt) => {
                    sector.label = $"Sector ({evt.newValue}\u00b0):";
                    sectorValueField.SetValueWithoutNotify(evt.newValue);
                    valProp.serializedObject.Update();
                    valProp.boxedValue = (sfloat)evt.newValue;
                    sectorPreview.angle = evt.newValue;
                    sectorPreview.RedrawElement();
                    valProp.serializedObject.ApplyModifiedProperties();
                    valProp.serializedObject.Update();
                });
                sectorRoot.Add(sector);
                #else
                var valProp = property.FindPropertyRelative(nameof(Sector.sector));
                var prop = valProp.floatValue;
                var sector = new Slider("Sector", 0f, 360f, pageSize: 1f);
                var sectorValueField = new FloatField();
                sectorValueField.AddToClassList("sector-field");
                sector.AddToClassList("sector");
                sector.value = prop;
                sector.label = $"Sector ({sector.value}\u00b0):";
                sectorPreview.angle = sector.value;
                sector.RegisterValueChangedCallback((evt) => {
                    sector.label = $"Sector ({evt.newValue}\u00b0):";
                    sectorValueField.SetValueWithoutNotify(evt.newValue);
                    valProp.serializedObject.Update();
                    valProp.floatValue = evt.newValue;
                    sectorPreview.angle = evt.newValue;
                    sectorPreview.RedrawElement();
                    valProp.serializedObject.ApplyModifiedProperties();
                    valProp.serializedObject.Update();
                });
                sectorRoot.Add(sector);
                #endif
                
                sectorValueField.value = sector.value;
                sectorValueField.RegisterValueChangedCallback((evt) => {
                    sector.value = evt.newValue;
                });
                sectorRoot.Add(sectorValueField);
            }
            return root;

        }

    }

    public class SectorPreview : VisualElement {

        public Color angleColor = Color.green;
        public Color fillColor = new Color(0f, 0f, 0f, 0.5f);
        public Color minRangeFillColor = new Color(1f, 0f, 0f, 0.2f);
        public Color borderColor = new Color(0f, 0f, 0f, 0.5f);
        public Color gridBackColor = new Color(1f, 1f, 1f, 0.05f);
        public Color gridMainColor = new Color(1f, 1f, 1f, 0.1f);
        public Color directionColor = new Color(1f, 1f, 0f, 0.3f);

        public float angle;
        public float range;
        public float minRange;

        public SectorPreview() {
            this.generateVisualContent += this.OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc) {
            
            var painter = mgc.painter2D;

            var rect = mgc.visualElement.worldBound;
            var centerPos = (Vector2)new float2(rect.width * 0.5f, rect.height * 0.5f);
            var padding = 20f;
            var scale = (float)(math.min(rect.width, rect.height) - padding) / this.range * 0.5f;
            //var capLeft = centerPos + new float2(0f, 30f);
            var capLeft = math.mul(quaternion.Euler(0f, 0f, -math.radians(this.angle * 0.5f)), new float3(0f, -this.range * scale, 0f));
            var capRight = math.mul(quaternion.Euler(0f, 0f, math.radians(this.angle * 0.5f)), new float3(0f, -this.range * scale, 0f));

            {
                // Draw grid
                DrawGrid(painter, rect, 1f, scale, this.gridBackColor);
                DrawGrid(painter, rect, 5f, scale, this.gridMainColor);
            }

            {
                // Draw border
                painter.strokeColor = this.borderColor;
                painter.lineWidth = 3f;
                painter.lineCap = LineCap.Butt;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, 0f));
                painter.LineTo(new Vector2(rect.width, 0f));
                painter.LineTo(new Vector2(rect.width, rect.height));
                painter.LineTo(new Vector2(0f, rect.height));
                painter.ClosePath();
                painter.Stroke();
            }
            
            if (this.angle >= 360f) {
                painter.strokeColor = this.angleColor;
                painter.fillColor = this.fillColor;
                painter.lineWidth = 1f;
                painter.lineCap = LineCap.Butt;
                painter.BeginPath();
                painter.Arc(centerPos, this.range * scale, new Angle(-90f), new Angle(360f - 90f), ArcDirection.Clockwise);
                painter.ClosePath();
                painter.Fill(FillRule.OddEven);
                painter.Stroke();
                
                if (this.minRange > 0f) {
                    painter.strokeColor = this.angleColor;
                    painter.fillColor = this.minRangeFillColor;
                    painter.lineWidth = 1f;
                    painter.lineCap = LineCap.Butt;
                    painter.BeginPath();
                    painter.Arc(centerPos, this.minRange * scale, new Angle(-90f), new Angle(360f - 90f), ArcDirection.Clockwise);
                    painter.ClosePath();
                    painter.Fill(FillRule.OddEven);
                    painter.Stroke();
                }
            } else {
                painter.strokeColor = this.angleColor;
                painter.fillColor = this.fillColor;
                painter.lineWidth = 1f;
                painter.lineCap = LineCap.Round;
                painter.BeginPath();
                painter.Arc(centerPos, this.minRange * scale, new Angle(-this.angle * 0.5f - 90f), new Angle(this.angle * 0.5f - 90f), ArcDirection.Clockwise);
                painter.LineTo(centerPos + (Vector2)capRight.xy);
                painter.Arc(centerPos, this.range * scale, new Angle(this.angle * 0.5f - 90f), new Angle(-this.angle * 0.5f - 90f), ArcDirection.CounterClockwise);
                painter.LineTo(centerPos + (Vector2)capLeft.xy);
                painter.LineTo(centerPos + (Vector2)math.normalize(capLeft.xy) * this.minRange * scale);
                painter.ClosePath();
                painter.Fill(FillRule.OddEven);
                painter.Stroke();
            }

            {
                // Draw direction
                DrawArrow(30f, this.directionColor, 40f);
                DrawArrow(20f, this.directionColor, 50f);
                DrawArrow(10f, this.directionColor, 60f);

                void DrawArrow(float arrowSize, Color color, float paddingOffset) {
                    painter.strokeColor = color;
                    painter.lineWidth = 2f;
                    painter.lineCap = LineCap.Butt;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(rect.width * 0.5f, rect.height * 0.5f));
                    painter.LineTo(new Vector2(rect.width * 0.5f, padding + paddingOffset));
                    painter.MoveTo(new Vector2(rect.width * 0.5f, padding + paddingOffset));
                    painter.LineTo(new Vector2(rect.width * 0.5f + arrowSize, 0f + padding + paddingOffset + arrowSize));
                    painter.MoveTo(new Vector2(rect.width * 0.5f, padding + paddingOffset));
                    painter.LineTo(new Vector2(rect.width * 0.5f - arrowSize, 0f + padding + paddingOffset + arrowSize));
                    painter.ClosePath();
                    painter.Stroke();
                }
                
            }
            
        }

        private static void DrawGrid(Painter2D painter, Rect rect, float cellSize, float scale, Color color) {
            painter.strokeColor = color;
            painter.lineWidth = 1f;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            var step = cellSize * scale;
            var startOffset = rect.width * 0.5f % step;
            for (float x = startOffset; x <= rect.width; x += step) {
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, rect.height));
            }
            startOffset = rect.height * 0.5f % step;
            for (float y = startOffset; y <= rect.height; y += step) {
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(rect.width, y));
            }
            painter.ClosePath();
            painter.Stroke();
        }

        public void RedrawElement() {
            this.MarkDirtyRepaint();
        }

    }

}