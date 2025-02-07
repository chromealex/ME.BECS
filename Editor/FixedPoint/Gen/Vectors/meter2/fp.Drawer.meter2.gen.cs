namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    
    [UnityEditor.CustomPropertyDrawer(typeof(meter2))]
    public class Fpmeter2PropertyDrawer : UnityEditor.PropertyDrawer {

        private int type;

        private static StyleSheet styleSheet;
        
        private static void LoadStyles() {

            styleSheet ??= ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>("EditorResources/FpPropertyDrawers");

        }

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(UnityEditor.SerializedProperty property) {

            LoadStyles();
            
            var root = new VisualElement();
            root.AddToClassList("root");
            root.styleSheets.Add(styleSheet);
            var propX = property.FindPropertyRelative(nameof(meter2.x));
            var valueX = propX.intValue;
            var propY = property.FindPropertyRelative(nameof(meter2.y));
            var valueY = propY.intValue;
            var container = new Label(property.displayName);
            container.AddToClassList("container");
            root.Add(container);
            var sfX = new FloatField("X:");
            sfX.AddToClassList("sf");
            container.Add(sfX);
            var siX = new IntegerField("X:");
            siX.AddToClassList("si");
            container.Add(siX);
            var sfY = new FloatField("Y:");
            sfY.AddToClassList("sf");
            container.Add(sfY);
            var siY = new IntegerField("Y:");
            siY.AddToClassList("si");
            container.Add(siY);
            {
                this.UpdateValue(sfX, siX, sfY, siY);
                {
                    sfX.value = new meter(valueX).ToFloat();
                    sfX.RegisterCallback<ChangeEvent<float>>((evt) => {
                        var val = evt.newValue;
                        if (val < 0f) val = 0f;
                        property.serializedObject.Update();
                        propX.intValue = meter.FromFloat(val).value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY);
                    });
                }
                {
                    siX.value = valueX;
                    siX.RegisterCallback<ChangeEvent<int>>((evt) => {
                        var val = evt.newValue;
                        property.serializedObject.Update();
                        propX.intValue = val;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY);
                    });
                }
                {
                    sfY.value = new meter(valueY).ToFloat();
                    sfY.RegisterCallback<ChangeEvent<float>>((evt) => {
                        var val = evt.newValue;
                        if (val < 0f) val = 0f;
                        property.serializedObject.Update();
                        propY.intValue = meter.FromFloat(val).value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY);
                    });
                }
                {
                    siY.value = valueY;
                    siY.RegisterCallback<ChangeEvent<int>>((evt) => {
                        var val = evt.newValue;
                        property.serializedObject.Update();
                        propY.intValue = val;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY);
                    });
                }
            }

            var items = new System.Collections.Generic.List<string>() { "meters", "original" };
            var e = new DropdownField(items, 0);
            e.AddToClassList("type");
            container.Add(e);
            {
                e.index = this.type;
                e.RegisterValueChangedCallback((evt) => {
                    this.type = items.IndexOf(evt.newValue);
                    this.UpdateValue(sfX, siX, sfY, siY);
                });
            }

            return root;
            
        }

        private static void UpdateValues(UnityEditor.SerializedProperty property, FloatField sfX, IntegerField siX, FloatField sfY, IntegerField siY) {
            
            var prop = property.FindPropertyRelative(nameof(umeter.value));
            var value = prop.intValue;
            sfX.SetValueWithoutNotify(new meter(value).ToFloat());
            siX.SetValueWithoutNotify(value);
            sfY.SetValueWithoutNotify(new meter(value).ToFloat());
            siY.SetValueWithoutNotify(value);
            
        }
        
        private void UpdateValue(FloatField sfX, IntegerField siX, FloatField sfY, IntegerField siY) {
            
            if (this.type == 0) {
                sfX.style.display = DisplayStyle.Flex;
                siX.style.display = DisplayStyle.None;
                sfY.style.display = DisplayStyle.Flex;
                siY.style.display = DisplayStyle.None;
            } else {
                sfX.style.display = DisplayStyle.None;
                siX.style.display = DisplayStyle.Flex;
                sfY.style.display = DisplayStyle.None;
                siY.style.display = DisplayStyle.Flex;
            }

        }

    }

}