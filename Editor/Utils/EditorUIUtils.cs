using System.Linq;

namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    using UnityEditor;

    public static class EditorUIUtils {
        
        public static uint ValidateMin(DropdownField dropdown, IntegerField field) {

            var minSizeInKb = (int)(MemoryAllocator.MIN_ZONE_SIZE / 1024);
            
            var c = dropdown.index + 1;
            if (c == 1 && field.value < minSizeInKb) {
                field.value = minSizeInKb;
            }
            
            if (c == 2 && field.value <= 0) {
                field.value = minSizeInKb;
                dropdown.index = 0;
                c = 1;
            }
            
            return EditorUtils.IntToBytes(field.value, c);

        }

        public static void DrawBytesField(VisualElement foldout, SerializedProperty property) {

            var choices = new System.Collections.Generic.List<string>() {
                "KB",
                "MB",
            };
            
            var container = new VisualElement();
            container.AddToClassList("field");
            foldout.Add(container);
            
            var so = property.serializedObject;
            var prop = property;
            var nameField = new IntegerField(prop.displayName);
            nameField.value = EditorUtils.BytesToInt(prop.uintValue, out var cat);
            var dropdown = new DropdownField(choices, cat - 1);
            nameField.RegisterValueChangedCallback((evt) => {
                so.Update();
                var bytes = ValidateMin(dropdown, nameField);
                prop.uintValue = bytes;
                so.ApplyModifiedProperties();
                so.Update();
            });
            container.Add(nameField);

            dropdown.RegisterValueChangedCallback((evt) => {
                so.Update();
                prop.uintValue = ValidateMin(dropdown, nameField);
                so.ApplyModifiedProperties();
                so.Update();
            });
            container.Add(dropdown);
            
            ValidateMin(dropdown, nameField);
            
            var tooltip = property.tooltip;
            if (string.IsNullOrEmpty(tooltip) == false) {

                tooltip = tooltip.Replace("{MIN_ZONE_SIZE_IN_KB}", MemoryAllocator.MIN_ZONE_SIZE_IN_KB.ToString());

                container.AddToClassList("has-tooltip");
                var tooltipElement = new Label($"<b>{property.displayName}</b>\n{tooltip}");
                tooltipElement.AddToClassList("tooltip-text");
                tooltipElement.pickingMode = PickingMode.Ignore;
                var tooltipButton = new Label("?");
                tooltipButton.AddToClassList("tooltip");
                container.Add(tooltipElement);
                container.Add(tooltipButton);

            }

        }

        public static void DrawTooltip(VisualElement container, SerializedProperty property) {
            
            var tooltip = property.tooltip;
            if (string.IsNullOrEmpty(tooltip) == false) {

                DrawTooltip(container, $"<b>{property.displayName}</b>\n{tooltip}");

            }

        }

        public static VisualElement DrawTooltip(VisualElement container, string tooltip) {
            return DrawTooltip(container, tooltip, default);
        }
        

        public static VisualElement DrawTooltip(VisualElement container, string tooltip, StyleLength width) {
            
            if (string.IsNullOrEmpty(tooltip) == false) {

                container.AddToClassList("has-tooltip");
                var tooltipElement = new Label(tooltip);
                tooltipElement.AddToClassList("tooltip-text");
                tooltipElement.pickingMode = PickingMode.Ignore;
                if (width != default) {
                    tooltipElement.style.width = width;
                    tooltipElement.AddToClassList("custom-width");
                }
                var tooltipButton = new Label("?");
                tooltipButton.AddToClassList("tooltip");
                tooltipButton.style.flexGrow = new StyleFloat(0f);
                container.Add(tooltipElement);
                container.Add(tooltipButton);
                return tooltipElement;

            }

            return null;

        }

        public static VisualElement DrawTooltip(VisualElement container, System.Func<VisualElement> tooltip, StyleLength width) {
            
            if (tooltip != null) {

                container.AddToClassList("has-tooltip");
                var tooltipElement = new VisualElement();
                tooltipElement.AddToClassList("tooltip-text");
                tooltipElement.pickingMode = PickingMode.Ignore;
                if (width != default) {
                    tooltipElement.style.width = width;
                    tooltipElement.AddToClassList("custom-width");
                }
                tooltipElement.Add(tooltip.Invoke());
                var tooltipButton = new Label("?");
                tooltipButton.AddToClassList("tooltip");
                tooltipButton.style.flexGrow = new StyleFloat(0f);
                container.Add(tooltipElement);
                container.Add(tooltipButton);
                return tooltipElement;

            }

            return null;

        }

        public static void RemoveTooltip(VisualElement container) {
            container.RemoveFromClassList("has-tooltip");
            container.Q(className: "tooltip-text").RemoveFromHierarchy();
            container.Q(className: "tooltip").RemoveFromHierarchy();
        }

        public static void DrawPropertyField(VisualElement root, SerializedProperty property) {
            
            var container = new VisualElement();
            container.AddToClassList("field");
            root.Add(container);

            var prop = new UnityEditor.UIElements.PropertyField(property.Copy());
            prop.BindProperty(property.Copy());
            container.Add(prop);

            DrawTooltip(container, property);
            
        }

        public static void DrawUIntField(VisualElement foldout, SerializedProperty property, int minValue = 0) {

            var container = new VisualElement();
            container.AddToClassList("field");
            foldout.Add(container);
            
            var so = property.serializedObject;
            var prop = property;
            var nameField = new IntegerField(prop.displayName);
            nameField.value = (int)prop.uintValue;
            nameField.RegisterValueChangedCallback((evt) => {
                so.Update();
                var val = evt.newValue;
                if (val <= minValue) val = minValue;
                prop.uintValue = (uint)val;
                nameField.value = val;
                so.ApplyModifiedProperties();
                so.Update();
            });
            container.Add(nameField);
            
            DrawTooltip(container, property);

        }

        public static void DrawEnumField<T>(VisualElement foldout, SerializedProperty property) where T : struct, System.Enum {

            var container = new VisualElement();
            container.AddToClassList("field");
            foldout.Add(container);
            
            var so = property.serializedObject;
            var prop = property;
            var nameField = new PopupField<string>(prop.displayName, prop.enumNames.ToList(), 0, (str) => {
                return prop.enumDisplayNames[System.Array.IndexOf(System.Enum.GetNames(typeof(T)), str)];
            });
            nameField.value = prop.enumNames[prop.enumValueIndex];
            nameField.RegisterValueChangedCallback((evt) => {
                so.Update();
                var val = evt.newValue;
                prop.enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(T)), val);
                nameField.value = val;
                so.ApplyModifiedProperties();
                so.Update();
            });
            container.Add(nameField);
            
            DrawTooltip(container, property);

        }

        public static void DrawToggleField(VisualElement foldout, SerializedProperty property) {

            var container = new VisualElement();
            container.AddToClassList("field");
            foldout.Add(container);
            
            var so = property.serializedObject;
            var prop = property;
            var nameField = new Toggle(prop.displayName);
            nameField.value = prop.boolValue;
            nameField.RegisterValueChangedCallback((evt) => {
                so.Update();
                var val = evt.newValue;
                prop.boolValue = val;
                nameField.value = val;
                so.ApplyModifiedProperties();
                so.Update();
            });
            container.Add(nameField);
            
            DrawTooltip(container, property);

        }

        public static System.Collections.Generic.List<VisualElement> DrawAspects(VisualElement root, System.Collections.Generic.IEnumerable<EditorUtils.AspectItem> aspects, System.Action<VisualElement, Label, EditorUtils.AspectItem> onEdit = null) {

            var result = new System.Collections.Generic.List<VisualElement>();
            foreach (var aspect in aspects) {
                
                var label = aspect.value;
                var fields = EditorUtils.GetAspectTypes(aspect.type);

                var fieldContainer = new VisualElement();
                fieldContainer.AddToClassList("field");
                
                var labelField = new Foldout();
                fieldContainer.Add(labelField);
                labelField.text = label;
                labelField.AddToClassList("aspect-component-container-field");
                
                var foldoutLabel = labelField.Q<Toggle>();
                var tooltip = (Label)EditorUIUtils.DrawTooltip(foldoutLabel, aspect.info.GetEditorComment());
                foldoutLabel.RegisterCallback<ClickEvent>(x => {
                    if (x.clickCount == 2) {
                        onEdit?.Invoke(foldoutLabel, tooltip, aspect);
                    }
                });
                
                {
                    var header = new VisualElement();
                    header.AddToClassList("header");
                    {
                        var column = new VisualElement();
                        column.AddToClassList("first-column");
                        var headerLabel = new Label("Component Name");
                        headerLabel.AddToClassList("main-label");
                        column.Add(headerLabel);
                        header.Add(column);
                    }
                    {
                        var column = new VisualElement();
                        column.AddToClassList("column");
                        var headerLabel = new Label("Query");
                        column.Add(headerLabel);
                        header.Add(column);
                    }
                    {
                        var column = new VisualElement();
                        column.AddToClassList("column");
                        var headerLabel = new Label("Auto");
                        column.Add(headerLabel);
                        header.Add(column);
                    }
                    labelField.Add(header);
                }

                for (var index = 0; index < fields.Length; ++index) {

                    var field = fields[index];
                    var labelFieldItem = new VisualElement();
                    labelFieldItem.AddToClassList("aspect-component-container");
                    if (index == fields.Length - 1) labelFieldItem.AddToClassList("last");
                    var componentLabel = EditorUtils.GetComponentName(field.fieldType);
                    {
                        var column = new VisualElement();
                        column.AddToClassList("first-column");
                        var mainLabel = new Label(componentLabel);
                        mainLabel.AddToClassList("main-label");
                        column.Add(mainLabel);
                        labelFieldItem.Add(column);
                    }

                    {
                        var column = new VisualElement();
                        column.AddToClassList("column");
                        var text = $"When you use <b>{label}</b> aspect in query, <b>{componentLabel}</b> will be <b>skipped</b>.";
                        if (field.required == true) {
                            text = $"When you use <b>{label}</b> aspect in query, <b>{componentLabel}</b> will be <b>used</b> for this operation.";
                        }

                        EditorUIUtils.DrawTooltip(column, text, new StyleLength(new Length(200f, LengthUnit.Pixel)));
                        var toggle = new Toggle();
                        toggle.SetEnabled(false);
                        toggle.value = field.required;
                        column.Add(toggle);
                        labelFieldItem.Add(column);
                    }

                    {
                        var column = new VisualElement();
                        column.AddToClassList("column");
                        var text = $"Some of aspect methods may create <b>{componentLabel}</b> at runtime.";
                        if (field.config == true) {
                            text = $"<b>{componentLabel}</b> automatically added onto entity while applying <b>{label}</b> aspect.";
                        }

                        EditorUIUtils.DrawTooltip(column, text, new StyleLength(new Length(200f, LengthUnit.Pixel)));
                        var toggle = new Toggle();
                        toggle.SetEnabled(false);
                        toggle.value = field.config;
                        column.Add(toggle);
                        labelFieldItem.Add(column);
                    }

                    labelField.Add(labelFieldItem);

                }

                root.Add(fieldContainer);
                result.Add(fieldContainer);

            }

            return result;

        }

        public static bool IsDarkColor(UnityEngine.Color backColor) {
            UnityEngine.Color color = backColor;
            color = UnityEngine.Color.Lerp(new UnityEngine.Color32(16, 16, 16, 255), color, color.a);
            double l = 0.2126d * color.r + 0.7152d * color.g + 0.0722d * color.b;
            return l > 0.4d;
        }

    }

}