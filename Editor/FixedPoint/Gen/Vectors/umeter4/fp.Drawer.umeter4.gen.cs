namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    
    [UnityEditor.CustomPropertyDrawer(typeof(umeter4))]
    public class Fpumeter4PropertyDrawer : UnityEditor.PropertyDrawer {

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
            var propX = property.FindPropertyRelative(nameof(umeter4.x));
            var valueX = propX.uintValue;
            var propY = property.FindPropertyRelative(nameof(umeter4.y));
            var valueY = propY.uintValue;
            var propZ = property.FindPropertyRelative(nameof(umeter4.z));
            var valueZ = propZ.uintValue;
            var propW = property.FindPropertyRelative(nameof(umeter4.z));
            var valueW = propW.uintValue;
            var container = new Label(property.displayName);
            container.AddToClassList("container");
            root.Add(container);
            var sfX = new FloatField("X:");
            sfX.AddToClassList("sf");
            container.Add(sfX);
            var siX = new UnsignedIntegerField("X:");
            siX.AddToClassList("si");
            container.Add(siX);
            var sfY = new FloatField("Y:");
            sfY.AddToClassList("sf");
            container.Add(sfY);
            var siY = new UnsignedIntegerField("Y:");
            siY.AddToClassList("si");
            container.Add(siY);
            var sfZ = new FloatField("Z:");
            sfZ.AddToClassList("sf");
            container.Add(sfZ);
            var siZ = new UnsignedIntegerField("Z:");
            siZ.AddToClassList("si");
            container.Add(siZ);
            var sfW = new FloatField("W:");
            sfW.AddToClassList("sf");
            container.Add(sfW);
            var siW = new UnsignedIntegerField("W:");
            siW.AddToClassList("si");
            container.Add(siW);
            {
                this.UpdateValue(sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                {
                    sfX.value = new umeter(valueX).ToFloat();
                    sfX.RegisterCallback<ChangeEvent<float>>((evt) => {
                        var val = evt.newValue;
                        if (val < 0f) val = 0f;
                        property.serializedObject.Update();
                        propX.uintValue = umeter.FromFloat(val).value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                    });
                }
                {
                    siX.value = valueX;
                    siX.RegisterCallback<ChangeEvent<uint>>((evt) => {
                        var val = evt.newValue;
                        property.serializedObject.Update();
                        propX.uintValue = val;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                    });
                }
                {
                    sfY.value = new umeter(valueY).ToFloat();
                    sfY.RegisterCallback<ChangeEvent<float>>((evt) => {
                        var val = evt.newValue;
                        if (val < 0f) val = 0f;
                        property.serializedObject.Update();
                        propY.uintValue = umeter.FromFloat(val).value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                    });
                }
                {
                    siY.value = valueY;
                    siY.RegisterCallback<ChangeEvent<uint>>((evt) => {
                        var val = evt.newValue;
                        property.serializedObject.Update();
                        propY.uintValue = val;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                    });
                }
                {
                    sfZ.value = new umeter(valueZ).ToFloat();
                    sfZ.RegisterCallback<ChangeEvent<float>>((evt) => {
                        var val = evt.newValue;
                        if (val < 0f) val = 0f;
                        property.serializedObject.Update();
                        propZ.uintValue = umeter.FromFloat(val).value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                    });
                }
                {
                    siZ.value = valueZ;
                    siZ.RegisterCallback<ChangeEvent<uint>>((evt) => {
                        var val = evt.newValue;
                        property.serializedObject.Update();
                        propZ.uintValue = val;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                    });
                }
                {
                    sfW.value = new umeter(valueW).ToFloat();
                    sfW.RegisterCallback<ChangeEvent<float>>((evt) => {
                        var val = evt.newValue;
                        if (val < 0f) val = 0f;
                        property.serializedObject.Update();
                        propW.uintValue = umeter.FromFloat(val).value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                    });
                }
                {
                    siW.value = valueW;
                    siW.RegisterCallback<ChangeEvent<uint>>((evt) => {
                        var val = evt.newValue;
                        property.serializedObject.Update();
                        propW.uintValue = val;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
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
                    this.UpdateValue(sfX, siX, sfY, siY, sfZ, siZ, sfW, siW);
                });
            }

            return root;
            
        }

        private static void UpdateValues(UnityEditor.SerializedProperty property, FloatField sfX, UnsignedIntegerField siX, FloatField sfY, UnsignedIntegerField siY, FloatField sfZ, UnsignedIntegerField siZ, FloatField sfW, UnsignedIntegerField siW) {
            
            var prop = property.FindPropertyRelative(nameof(umeter.value));
            var value = prop.uintValue;
            sfX.SetValueWithoutNotify(new umeter(value).ToFloat());
            siX.SetValueWithoutNotify(value);
            sfY.SetValueWithoutNotify(new umeter(value).ToFloat());
            siY.SetValueWithoutNotify(value);
            sfZ.SetValueWithoutNotify(new umeter(value).ToFloat());
            siZ.SetValueWithoutNotify(value);
            sfW.SetValueWithoutNotify(new umeter(value).ToFloat());
            siW.SetValueWithoutNotify(value);
            
        }
        
        private void UpdateValue(FloatField sfX, UnsignedIntegerField siX, FloatField sfY, UnsignedIntegerField siY, FloatField sfZ, UnsignedIntegerField siZ, FloatField sfW, UnsignedIntegerField siW) {
            
            if (this.type == 0) {
                sfX.style.display = DisplayStyle.Flex;
                siX.style.display = DisplayStyle.None;
                sfY.style.display = DisplayStyle.Flex;
                siY.style.display = DisplayStyle.None;
                sfZ.style.display = DisplayStyle.Flex;
                siZ.style.display = DisplayStyle.None;
                sfW.style.display = DisplayStyle.Flex;
                siW.style.display = DisplayStyle.None;
            } else {
                sfX.style.display = DisplayStyle.None;
                siX.style.display = DisplayStyle.Flex;
                sfY.style.display = DisplayStyle.None;
                siY.style.display = DisplayStyle.Flex;
                sfZ.style.display = DisplayStyle.None;
                siZ.style.display = DisplayStyle.Flex;
                sfW.style.display = DisplayStyle.None;
                siW.style.display = DisplayStyle.Flex;
            }

        }

    }

}