using System.Linq;

namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    using UnityEditor;
    using UnityEngine;

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

        private static StyleSheet defaultStyleSheet;
        public static void ApplyDefaultStyles(VisualElement container) {
            defaultStyleSheet ??= ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/DefaultStyles.uss");
            container.styleSheets.Add(defaultStyleSheet);
        }

        public class LogoLine : VisualElement {
            
            private static CustomStyleProperty<UnityEngine.Color> leftColorProp = new CustomStyleProperty<UnityEngine.Color>("--left-color");
            private static CustomStyleProperty<UnityEngine.Color> rightColorProp = new CustomStyleProperty<UnityEngine.Color>("--right-color");
            private static CustomStyleProperty<UnityEngine.Color> leftTopColorProp = new CustomStyleProperty<UnityEngine.Color>("--left-top-color");
            private static CustomStyleProperty<UnityEngine.Color> rightTopColorProp = new CustomStyleProperty<UnityEngine.Color>("--right-top-color");
            private static CustomStyleProperty<UnityEngine.Color> leftBottomColorProp = new CustomStyleProperty<UnityEngine.Color>("--left-bottom-color");
            private static CustomStyleProperty<UnityEngine.Color> rightBottomColorProp = new CustomStyleProperty<UnityEngine.Color>("--right-bottom-color");
            private static CustomStyleProperty<float> speedProp = new CustomStyleProperty<float>("--speed");

            private Color leftTop;
            private Color rightTop;
            private Color leftBottom;
            private Color rightBottom;
            private bool animate;
            private IVisualElementScheduledItem updateItem;
            private float offset;
            private float speed;
            private bool stopOnNext;

            private static readonly Vertex[] vertices = new Vertex[4];
            private static readonly Vertex[] animVertices = new Vertex[4];
            private static readonly Vertex[] animVerticesBack = new Vertex[4];
            private static readonly ushort[] indexes = { 0, 1, 2, 2, 3, 0 };

            public LogoLine() {
                this.generateVisualContent += this.OnGenerateVisualContent;
                this.RegisterCallback<CustomStyleResolvedEvent>(new EventCallback<CustomStyleResolvedEvent>(this.OnCustomStyleResolved));
            }
            
            private void OnCustomStyleResolved(CustomStyleResolvedEvent e) {
                var customStyle = e.customStyle;
                if (customStyle.TryGetValue(leftColorProp, out var left) == true) {
                    this.leftTop = left;
                    this.leftBottom = left;
                }
                if (customStyle.TryGetValue(rightColorProp, out var right) == true) {
                    this.rightTop = right;
                    this.rightBottom = right;
                }
                if (customStyle.TryGetValue(leftTopColorProp, out var leftTop) == true) this.leftTop = leftTop;
                if (customStyle.TryGetValue(rightTopColorProp, out var rightTop) == true) this.rightTop = rightTop;
                if (customStyle.TryGetValue(leftBottomColorProp, out var leftBottom) == true) this.leftBottom = leftBottom;
                if (customStyle.TryGetValue(rightBottomColorProp, out var rightBottom) == true) this.rightBottom = rightBottom;
                
                if (customStyle.TryGetValue(speedProp, out var speed) == true) this.speed = speed;
            }
            
            private void OnGenerateVisualContent(MeshGenerationContext mgc) {
                var r = this.contentRect;
                if (r.width < 0.01f || r.height < 0.01f) return;

                vertices[0].tint = this.leftTop;
                vertices[1].tint = this.leftTop;
                vertices[2].tint = this.leftTop;
                vertices[3].tint = this.leftTop;

                animVertices[0].tint = this.leftBottom;
                animVertices[1].tint = this.leftTop;
                animVertices[2].tint = this.rightTop;
                animVertices[3].tint = this.rightBottom;
                
                animVerticesBack[0].tint = this.rightBottom;
                animVerticesBack[1].tint = this.rightTop;
                animVerticesBack[2].tint = this.leftTop;
                animVerticesBack[3].tint = this.leftBottom;
                
                float left = 0f;
                float right = r.width;
                float top = 0f;
                float bottom = r.height;

                vertices[0].position = new UnityEngine.Vector3(left, bottom, Vertex.nearZ);
                vertices[1].position = new UnityEngine.Vector3(left, top, Vertex.nearZ);
                vertices[2].position = new UnityEngine.Vector3(right, top, Vertex.nearZ);
                vertices[3].position = new UnityEngine.Vector3(right, bottom, Vertex.nearZ);

                animVertices[0].position = new UnityEngine.Vector3(left + this.offset, bottom, Vertex.nearZ);
                animVertices[1].position = new UnityEngine.Vector3(left + this.offset, top, Vertex.nearZ);
                animVertices[2].position = new UnityEngine.Vector3(right * 0.5f + this.offset, top, Vertex.nearZ);
                animVertices[3].position = new UnityEngine.Vector3(right * 0.5f + this.offset, bottom, Vertex.nearZ);
                
                animVerticesBack[0].position = new UnityEngine.Vector3(right * 0.5f + this.offset, bottom, Vertex.nearZ);
                animVerticesBack[1].position = new UnityEngine.Vector3(right * 0.5f + this.offset, top, Vertex.nearZ);
                animVerticesBack[2].position = new UnityEngine.Vector3(right + this.offset, top, Vertex.nearZ);
                animVerticesBack[3].position = new UnityEngine.Vector3(right + this.offset, bottom, Vertex.nearZ);

                { // bg
                    MeshWriteData mwd = mgc.Allocate(vertices.Length, indexes.Length);
                    mwd.SetAllVertices(vertices);
                    mwd.SetAllIndices(indexes);
                }

                {
                    MeshWriteData mwd = mgc.Allocate(animVertices.Length, indexes.Length);
                    mwd.SetAllVertices(animVertices);
                    mwd.SetAllIndices(indexes);
                }
                {
                    MeshWriteData mwd = mgc.Allocate(animVerticesBack.Length, indexes.Length);
                    mwd.SetAllVertices(animVerticesBack);
                    mwd.SetAllIndices(indexes);
                }
            }

            public void ThinkStart() {
                if (this.animate == true) return;
                this.animate = true;
                this.stopOnNext = false;
                this.updateItem ??= this.schedule.Execute(this.Update).Every(1);
                this.updateItem.Resume();
            }

            public void ThinkEnd() {
                if (this.animate == false) return;
                this.animate = false;
                this.stopOnNext = false;
            }
            
            public void ThinkOnce() {
                if (this.animate == true) return;
                this.ThinkStart();
                this.ThinkEnd();
            }

            private void Update(TimerState timer) {
                this.offset += (timer.deltaTime / 1000f) * this.speed;
                if (this.animate == false && this.stopOnNext == true) {
                    if (this.offset >= 0f) {
                        this.offset = 0f;
                        this.updateItem.Pause();
                        return;
                    }
                }
                var r = this.contentRect;
                if (this.offset > r.width) {
                    this.offset = -r.width;
                    if (this.animate == false) this.stopOnNext = true;
                }
                this.ApplyOffset();
            }

            private void ApplyOffset() {
                this.MarkDirtyRepaint();
            }

        }
        
        public static LogoLine AddLogoLine(VisualElement root) {
            var logo = root.Q<LogoLine>(className: "top-line");
            if (logo == null) {
                logo = new LogoLine();
                logo.AddToClassList("top-line");
                root.Add(logo);
            }
            return logo;
        }

        public static void AddWindowContent(VisualElement root, VisualElement currentContent) {
            var newRoot = new LogoLine();
            newRoot.AddToClassList("window-content-back");
            var newRootContent = new VisualElement();
            newRoot.Add(newRootContent);
            newRootContent.AddToClassList("window-content");
            newRootContent.Add(currentContent);
            root.Add(newRoot);
        }

    }

}