namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    
    [UnityEditor.CustomPropertyDrawer(typeof(umeter3))]
    public class Fpumeter3PropertyDrawer : UnityEditor.PropertyDrawer {

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
            var propX = property.FindPropertyRelative(nameof(umeter3.x));
            var valueX = propX.uintValue;
            var propY = property.FindPropertyRelative(nameof(umeter3.y));
            var valueY = propY.uintValue;
            var propZ = property.FindPropertyRelative(nameof(umeter3.z));
            var valueZ = propZ.uintValue;
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
            {
                this.UpdateValue(sfX, siX, sfY, siY, sfZ, siZ);
                {
                    sfX.value = new umeter(valueX).ToFloat();
                    sfX.RegisterCallback<ChangeEvent<float>>((evt) => {
                        var val = evt.newValue;
                        if (val < 0f) val = 0f;
                        property.serializedObject.Update();
                        propX.uintValue = umeter.FromFloat(val).value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ);
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
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ);
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
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ);
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
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ);
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
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ);
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
                        UpdateValues(property, sfX, siX, sfY, siY, sfZ, siZ);
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
                    this.UpdateValue(sfX, siX, sfY, siY, sfZ, siZ);
                });
            }

            return root;
            
        }

        private static void UpdateValues(UnityEditor.SerializedProperty property, FloatField sfX, UnsignedIntegerField siX, FloatField sfY, UnsignedIntegerField siY, FloatField sfZ, UnsignedIntegerField siZ) {
            
            var prop = property.FindPropertyRelative(nameof(umeter.value));
            var value = prop.uintValue;
            sfX.SetValueWithoutNotify(new umeter(value).ToFloat());
            siX.SetValueWithoutNotify(value);
            sfY.SetValueWithoutNotify(new umeter(value).ToFloat());
            siY.SetValueWithoutNotify(value);
            sfZ.SetValueWithoutNotify(new umeter(value).ToFloat());
            siZ.SetValueWithoutNotify(value);
            
        }
        
        private void UpdateValue(FloatField sfX, UnsignedIntegerField siX, FloatField sfY, UnsignedIntegerField siY, FloatField sfZ, UnsignedIntegerField siZ) {
            
            if (this.type == 0) {
                sfX.style.display = DisplayStyle.Flex;
                siX.style.display = DisplayStyle.None;
                sfY.style.display = DisplayStyle.Flex;
                siY.style.display = DisplayStyle.None;
                sfZ.style.display = DisplayStyle.Flex;
                siZ.style.display = DisplayStyle.None;
            } else {
                sfX.style.display = DisplayStyle.None;
                siX.style.display = DisplayStyle.Flex;
                sfY.style.display = DisplayStyle.None;
                siY.style.display = DisplayStyle.Flex;
                sfZ.style.display = DisplayStyle.None;
                siZ.style.display = DisplayStyle.Flex;
            }

        }

    }

}