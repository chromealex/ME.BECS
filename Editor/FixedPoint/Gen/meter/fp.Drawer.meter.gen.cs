namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    
    [UnityEditor.CustomPropertyDrawer(typeof(meter))]
    public class FpmeterPropertyDrawer : UnityEditor.PropertyDrawer {

        private int type;

        public string customName;
        public System.Func<int, int> onValueSet;
        public System.Func<meter, meter> onValueChanged;
        
        private static StyleSheet styleSheet;
        
        private static void LoadStyles() {

            styleSheet ??= ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>("EditorResources/FpPropertyDrawers");

        }

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(UnityEditor.SerializedProperty property) {

            LoadStyles();
            
            var root = new VisualElement();
            root.AddToClassList("root");
            root.styleSheets.Add(styleSheet);
            var prop = property.FindPropertyRelative(nameof(meter.value));
            var container = new VisualElement();
            container.AddToClassList("container");
            root.Add(container);
            var sf = new FloatField(this.customName ?? property.displayName);
            sf.AddToClassList("sf");
            container.Add(sf);
            var si = new IntegerField(this.customName ?? property.displayName);
            si.AddToClassList("si");
            container.Add(si);
            {
                this.UpdateValue(sf, si);
                {
                    sf.RegisterCallback<ChangeEvent<float>>((evt) => {
                        var val = evt.newValue;
                        if (val < 0f) val = 0f;
                        property.serializedObject.Update();
                        var orig = meter.FromFloat(val);
                        if (this.onValueChanged != null) orig = this.onValueChanged.Invoke(orig);
                        prop.intValue = orig.value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        this.UpdateValues(property, sf, si);
                    });
                }
                {
                    si.RegisterCallback<ChangeEvent<int>>((evt) => {
                        var val = evt.newValue;
                        property.serializedObject.Update();
                        var orig = new meter(val);
                        if (this.onValueChanged != null) orig = this.onValueChanged.Invoke(orig);
                        prop.intValue = orig.value;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                        this.UpdateValues(property, sf, si);
                    });
                }
            }

            this.UpdateValues(property, sf, si);
            var items = new System.Collections.Generic.List<string>() { "meters", "mmeters" };
            var e = new DropdownField(items, 0);
            e.AddToClassList("type");
            container.Add(e);
            {
                e.index = this.type;
                e.RegisterValueChangedCallback((evt) => {
                    this.type = items.IndexOf(evt.newValue);
                    this.UpdateValue(sf, si);
                });
            }

            return root;
            
        }

        private void UpdateValues(UnityEditor.SerializedProperty property, FloatField sf, IntegerField si) {
            
            var prop = property.FindPropertyRelative(nameof(meter.value));
            var value = this.onValueSet != null ? this.onValueSet.Invoke(prop.intValue) : prop.intValue;
            sf.SetValueWithoutNotify(new meter(value).ToFloat());
            si.SetValueWithoutNotify(value);
            
        }
        
        private void UpdateValue(FloatField sf, IntegerField si) {
            
            if (this.type == 0) {
                sf.style.display = DisplayStyle.Flex;
                si.style.display = DisplayStyle.None;
            } else {
                sf.style.display = DisplayStyle.None;
                si.style.display = DisplayStyle.Flex;
            }

        }

    }

}