namespace ME.BECS.Attack.Editor {

    using UnityEditor;
    using UnityEngine.UIElements;
    using Unity.Mathematics;
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
            SectorPreview sectorPreview = null;
            {
                sectorPreview = new SectorPreview();
                sectorPreview.AddToClassList("preview");
                root.Add(sectorPreview);
            }
            FloatField minField = null;
            FloatField maxField = null;
            {
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
            }
            {
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
            }

            var sectorRoot = new VisualElement();
            sectorRoot.AddToClassList("sector-root");
            root.Add(sectorRoot);
            {
                var valProp = property.FindPropertyRelative(nameof(Sector.sector));
                var sector = new Slider("Sector", 0f, 360f, pageSize: 1f);
                var sectorValueField = new FloatField();
                sectorValueField.AddToClassList("sector-field");
                sector.AddToClassList("sector");
                sector.value = valProp.floatValue;
                sector.label = "Sector (" + valProp.floatValue + "\u00b0):";
                sectorPreview.angle = valProp.floatValue;
                sector.RegisterValueChangedCallback((evt) => {
                    sector.label = "Sector (" + evt.newValue + "\u00b0):";
                    sectorValueField.SetValueWithoutNotify(evt.newValue);
                    valProp.serializedObject.Update();
                    valProp.floatValue = evt.newValue;
                    sectorPreview.angle = evt.newValue;
                    sectorPreview.RedrawElement();
                    valProp.serializedObject.ApplyModifiedProperties();
                    valProp.serializedObject.Update();
                });
                sectorRoot.Add(sector);
                
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

        public float angle;
        public float range;
        public float minRange;

        public SectorPreview() {
            this.generateVisualContent += this.OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc) {
            
            var painter = mgc.painter2D;

            var rect = mgc.visualElement.worldBound;
            var centerPos = new float2(rect.width * 0.5f, rect.height * 0.5f);
            var scale = math.min(rect.width, rect.height) / this.range * 0.5f;
            //var capLeft = centerPos + new float2(0f, 30f);
            var capLeft = math.mul(quaternion.Euler(0f, 0f, -math.radians(this.angle * 0.5f)), new float3(0f, -this.range * scale, 0f));
            var capRight = math.mul(quaternion.Euler(0f, 0f, math.radians(this.angle * 0.5f)), new float3(0f, -this.range * scale, 0f));

            if (this.angle >= 360f) {
                painter.strokeColor = this.angleColor;
                painter.lineWidth = 1f;
                painter.lineCap = LineCap.Butt;
                painter.BeginPath();
                if (this.minRange > 0f) painter.Arc(centerPos, this.minRange * scale, new Angle(-90f), new Angle(360f - 90f), ArcDirection.Clockwise);
                painter.Arc(centerPos, this.range * scale, new Angle(-90f), new Angle(360f - 90f), ArcDirection.Clockwise);
                painter.ClosePath();
                painter.Stroke();
                return;
            }
            
            painter.strokeColor = this.angleColor;
            painter.lineWidth = 1f;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.Arc(centerPos, this.minRange * scale, new Angle(-this.angle * 0.5f - 90f), new Angle(this.angle * 0.5f - 90f), ArcDirection.Clockwise);
            painter.LineTo(centerPos + capRight.xy);
            painter.Arc(centerPos, this.range * scale, new Angle(this.angle * 0.5f - 90f), new Angle(-this.angle * 0.5f - 90f), ArcDirection.CounterClockwise);
            painter.LineTo(centerPos + capLeft.xy);
            painter.LineTo(centerPos + math.normalize(capLeft.xy) * this.minRange * scale);
            painter.ClosePath();
            painter.Fill(FillRule.OddEven);
            painter.Stroke();
            
        }

        public void RedrawElement() {
            this.MarkDirtyRepaint();
        }

    }

}