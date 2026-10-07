using System.Linq;

namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    using UnityEditor;

    public static class EditorUIUtils {

        public static void ApplyComponentGroupColor(VisualElement element, System.Type componentType) {
            if (EditorUtils.TryGetComponentGroupColor(componentType, out var color) == false) return;
            // Display vivid group colors as subdued accents without changing their source values.
            var alpha = color.a;
            UnityEngine.Color.RGBToHSV(color, out var hue, out var saturation, out var brightness);
            color = UnityEngine.Color.HSVToRGB(hue, saturation * 0.75f, brightness * 0.6f);
            color.a = alpha;
            var header = element is Foldout foldout ? foldout.Q<Toggle>() : element;
            if (header == null) return;
            var accent = new VisualElement { pickingMode = PickingMode.Ignore };
            accent.AddToClassList("becs-component-group-accent");
            accent.style.borderLeftColor = color;
            accent.generateVisualContent += context => {
                var rect = accent.contentRect;
                if (rect.width <= 0f || rect.height <= 0f) return;
                var near = color;
                near.a *= 0.09f;
                var far = color;
                far.a = 0f;
                var mesh = context.Allocate(4, 6);
                mesh.SetAllVertices(new[] {
                    new Vertex { position = new UnityEngine.Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = near },
                    new Vertex { position = new UnityEngine.Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = far },
                    new Vertex { position = new UnityEngine.Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = far },
                    new Vertex { position = new UnityEngine.Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = near },
                });
                mesh.SetAllIndices(new ushort[] { 0, 1, 2, 2, 3, 0 });
            };
            header.Insert(0, accent);
        }

        
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
                var tooltipButton = new Label("?") { pickingMode = PickingMode.Position };
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
                var tooltipButton = new Label("?") { pickingMode = PickingMode.Position };
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
                var tooltipButton = new Label("?") { pickingMode = PickingMode.Position };
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

        // Read the resolved USS accent, so custom themes also get matching tab icons.
        private static readonly CustomStyleProperty<UnityEngine.Color> iconAccent = new CustomStyleProperty<UnityEngine.Color>("--becs-accent");
        private static readonly System.Collections.Generic.Dictionary<string, UnityEngine.Texture2D> themedIcons = new System.Collections.Generic.Dictionary<string, UnityEngine.Texture2D>();
        private static readonly System.Collections.Generic.Dictionary<EditorWindow, System.Action> iconWindows = new System.Collections.Generic.Dictionary<EditorWindow, System.Action>();

        static EditorUIUtils() {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseThemedIcons;
            EditorApplication.quitting += ReleaseThemedIcons;
            EditorApplication.projectChanged += RefreshWindowIcons;
        }

        public static void ApplyWindowIcon(EditorWindow window, string title, string resourcePath) {
            void UpdateIcon() {
                if (window == null) return;
                var accent = window.rootVisualElement.customStyle.TryGetValue(iconAccent, out var resolved)
                    ? resolved : (UnityEngine.Color)new UnityEngine.Color32(113, 150, 255, 255);
                window.titleContent = new UnityEngine.GUIContent(title, GetThemedIcon(resourcePath, accent));
            }
            foreach (var closed in iconWindows.Keys.Where(item => item == null).ToArray()) iconWindows.Remove(closed);
            if (!iconWindows.ContainsKey(window)) {
                window.rootVisualElement.RegisterCallback<CustomStyleResolvedEvent>(evt => UpdateIcon());
                // Retrying also covers icons imported after a domain reload.
                window.rootVisualElement.RegisterCallback<AttachToPanelEvent>(evt => UpdateIcon());
            }
            iconWindows[window] = UpdateIcon;
            UpdateIcon();
        }

        private static UnityEngine.Texture2D GetThemedIcon(string resourcePath, UnityEngine.Color accent) {
            var key = resourcePath + ":" + UnityEngine.ColorUtility.ToHtmlStringRGBA(accent);
            if (themedIcons.TryGetValue(key, out var cached) && cached != null) return cached;
            var source = EditorUtils.LoadResource<UnityEngine.Texture2D>(resourcePath, false);
            if (source == null) return null;
            // Decode the PNG separately; imported textures need not have Read/Write enabled.
            var path = AssetDatabase.GetAssetPath(source);
            if (!System.IO.File.Exists(path)) return source;
            var readable = new UnityEngine.Texture2D(2, 2);
            try {
                if (!UnityEngine.ImageConversion.LoadImage(readable, System.IO.File.ReadAllBytes(path))) return source;
                var background = readable.GetPixel(0, 0).grayscale;
                var foreground = readable.GetPixels().Max(x => x.grayscale);
                var range = UnityEngine.Mathf.Max(0.001f, foreground - background);
                const int size = 32;
                var pixels = new UnityEngine.Color[size * size];
                for (var y = 0; y < size; ++y) {
                    for (var x = 0; x < size; ++x) {
                        var sample = readable.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
                        var glyph = UnityEngine.Mathf.Clamp01((sample.grayscale - background) / range);
                        var color = UnityEngine.Color.Lerp(accent, new UnityEngine.Color32(232, 234, 240, 255), glyph);
                        color.a = sample.a;
                        pixels[y * size + x] = color;
                    }
                }
                var result = new UnityEngine.Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, false) {
                    name = "BECS themed " + source.name,
                    hideFlags = UnityEngine.HideFlags.HideAndDontSave,
                    filterMode = UnityEngine.FilterMode.Bilinear,
                    wrapMode = UnityEngine.TextureWrapMode.Clamp,
                };
                result.SetPixels(pixels);
                result.Apply(false, true);
                themedIcons[key] = result;
                return result;
            } finally {
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static void RefreshWindowIcons() {
            ClearThemedIconTextures();
            foreach (var entry in iconWindows.ToArray()) {
                if (entry.Key == null) iconWindows.Remove(entry.Key);
                else entry.Value();
            }
        }

        private static void ReleaseThemedIcons() {
            ClearThemedIconTextures();
            iconWindows.Clear();
        }

        private static void ClearThemedIconTextures() {
            foreach (var icon in themedIcons.Values) {
                if (icon != null) UnityEngine.Object.DestroyImmediate(icon);
            }
            themedIcons.Clear();
        }

        public static void AddHorizontalScrollFades(ScrollView scroll) => AddScrollFades(scroll, false);

        public static void AddVerticalScrollFades(ScrollView scroll) => AddScrollFades(scroll, true);

        private static void AddScrollFades(ScrollView scroll, bool vertical) {
            var left = new ScrollEdgeFade(true, vertical);
            var right = new ScrollEdgeFade(false, vertical);
            scroll.contentViewport.Add(left);
            scroll.contentViewport.Add(right);
            void UpdateFades() {
                var extent = vertical ? scroll.contentContainer.layout.height : scroll.contentContainer.layout.width;
                var viewport = vertical ? scroll.contentViewport.layout.height : scroll.contentViewport.layout.width;
                var limit = UnityEngine.Mathf.Max(0, extent - viewport);
                var offset = vertical ? scroll.scrollOffset.y : scroll.scrollOffset.x;
                left.style.display = offset > 0.5f ? DisplayStyle.Flex : DisplayStyle.None;
                right.style.display = offset < limit - 0.5f ? DisplayStyle.Flex : DisplayStyle.None;
            }
            (vertical ? scroll.verticalScroller : scroll.horizontalScroller).valueChanged += _ => UpdateFades();
            scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateFades());
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateFades());
            scroll.schedule.Execute(UpdateFades);
        }

        private sealed class ScrollEdgeFade : VisualElement {
            public ScrollEdgeFade(bool left, bool vertical) {
                this.pickingMode = PickingMode.Ignore;
                this.AddToClassList("becs-scroll-fade");
                this.AddToClassList(vertical ? (left ? "becs-scroll-fade-top" : "becs-scroll-fade-bottom") : (left ? "becs-scroll-fade-left" : "becs-scroll-fade-right"));
                this.generateVisualContent += context => {
                    var rect = this.contentRect;
                    if (rect.width <= 0 || rect.height <= 0) return;
                    var opaque = this.resolvedStyle.color;
                    var transparent = opaque;
                    transparent.a = 0;
                    var near = left ? opaque : transparent;
                    var far = left ? transparent : opaque;
                    var mesh = context.Allocate(4, 6);
                    mesh.SetAllVertices(new[] {
                        new Vertex { position = new UnityEngine.Vector3(0, 0, Vertex.nearZ), tint = near },
                        new Vertex { position = new UnityEngine.Vector3(rect.width, 0, Vertex.nearZ), tint = vertical ? near : far },
                        new Vertex { position = new UnityEngine.Vector3(rect.width, rect.height, Vertex.nearZ), tint = far },
                        new Vertex { position = new UnityEngine.Vector3(0, rect.height, Vertex.nearZ), tint = vertical ? far : near },
                    });
                    mesh.SetAllIndices(new ushort[] { 0, 1, 2, 2, 3, 0 });
                };
            }
        }

        public static Button CreateAddWorldButton(System.Action action, string tooltip) {
            var button = CreateAddButton(action, tooltip);
            button.AddToClassList("add-world-tab");
            return button;
        }

        public static Button CreateAddButton(System.Action action, string tooltip) {
            var button = new Button(action) { tooltip = tooltip };
            ConfigureAddButton(button);
            return button;
        }

        public static void ConfigureAddButton(Button button, string label = null) {
            button.text = string.Empty;
            button.AddToClassList("becs-add-button");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("becs-add-button-icon");
            button.Add(icon);
            if (string.IsNullOrEmpty(label) == false) {
                button.AddToClassList("becs-add-button-labeled");
                button.Add(new Label(label) { pickingMode = PickingMode.Ignore });
            }
            // Draw the plus geometrically; font ascent/baseline can shift the text glyph.
            var horizontal = new VisualElement { pickingMode = PickingMode.Ignore };
            horizontal.AddToClassList("becs-add-button-line");
            var vertical = new VisualElement { pickingMode = PickingMode.Ignore };
            vertical.AddToClassList("becs-add-button-line");
            vertical.AddToClassList("becs-add-button-line-vertical");
            icon.Add(horizontal);
            icon.Add(vertical);
        }

        private const float ScrollbarThickness = 2f;

        private static void TrackThinScrollbars(VisualElement root) {
            if (root.ClassListContains("becs-shared-scrollbars")) return;
            root.AddToClassList("becs-shared-scrollbars");
            var scrollers = root.Query<Scroller>().Build();
            // Children may be built after ApplyCommonStyles or replaced during live refresh.
            // UI Toolkit automatically pauses this item while the root is detached.
            root.schedule.Execute(() => scrollers.ForEach(ApplyThinScrollbar)).Every(250);
        }

        private static void TrackFieldSliders(VisualElement root) {
            if (root.ClassListContains("becs-tracked-field-sliders") == true) return;
            root.AddToClassList("becs-tracked-field-sliders");
            root.schedule.Execute(() => {
                root.Query<VisualElement>(className: "unity-base-field").ForEach(StyleInspectorField);
                root.Query<ListView>().ForEach(StyleInspectorList);
                root.Query<Slider>().ForEach(slider => {
                    if (ConfigureFieldSlider(slider, () => UnityEngine.Mathf.InverseLerp(slider.lowValue, slider.highValue, slider.value), () => slider.direction, () => slider.inverted, out var update) == true) {
                        slider.pageSize = 0f;
                        slider.RegisterValueChangedCallback(evt => update());
                    }
                });
                root.Query<SliderInt>().ForEach(slider => {
                    if (ConfigureFieldSlider(slider, () => UnityEngine.Mathf.InverseLerp(slider.lowValue, slider.highValue, slider.value), () => slider.direction, () => slider.inverted, out var update) == true) {
                        slider.pageSize = 0f;
                        slider.RegisterValueChangedCallback(evt => update());
                    }
                });
            }).Every(250);
        }

        private static void StyleInspectorList(ListView list) {
            var row = list.parent;
            while (row != null && row.ClassListContains("config-field-row") == false) row = row.parent;
            if (row == null) return;
            row.AddToClassList("becs-collection-field-row");
            void StyleButton(string name, bool remove) {
                var button = list.Q<Button>(name);
                if (button == null || button.ClassListContains("becs-list-action") == true) return;
                button.Clear();
                ConfigureAddButton(button);
                button.AddToClassList("becs-list-action");
                button.EnableInClassList("becs-remove-button", remove);
            }
            StyleButton(BaseListView.footerAddButtonName, false);
            StyleButton(BaseListView.footerRemoveButtonName, true);
        }

        private static void StyleInspectorField(VisualElement field) {
            if (field.ClassListContains("becs-inspector-field") == true || field is Foldout || field.ClassListContains("unity-foldout__toggle") == true || field.ClassListContains("config-field-mask") == true) return;
            var ancestor = field.parent;
            var inRow = false;
            var axis = false;
            while (ancestor != null) {
                if (ancestor.ClassListContains("config-field-row") == true) { inRow = true; break; }
                if (ancestor.ClassListContains("unity-composite-field__input") == true || ancestor.ClassListContains("unity-composite-field") == true || ancestor.ClassListContains("vector-field") == true || ancestor.ClassListContains("quaternion-field") == true) axis = true;
                ancestor = ancestor.parent;
            }
            if (inRow == false) return;
            field.AddToClassList("becs-inspector-field");
            field.EnableInClassList("becs-inspector-axis", axis);
            field.RemoveFromClassList("unity-base-field__aligned");
            var label = field.Children().FirstOrDefault(child => child.ClassListContains("unity-base-field__label") == true);
            if (label != null) {
                // Unity's binding alignment writes inline widths, so USS alone cannot unify them.
                var width = axis == true ? 16f : 140f;
                label.style.width = width;
                label.style.minWidth = width;
                label.style.maxWidth = width;
                label.style.flexBasis = width;
                label.style.flexGrow = 0f;
                label.style.flexShrink = 0f;
                label.style.marginRight = axis == true ? 4f : 8f;
            }
        }

        private static bool ConfigureFieldSlider(VisualElement slider, System.Func<float> progress, System.Func<SliderDirection> direction, System.Func<bool> inverted, out System.Action update) {
            update = null;
            if (slider.ClassListContains("becs-filled-slider") == true || slider.GetFirstAncestorOfType<Scroller>() != null) return false;
            var row = slider;
            while (row != null && row.ClassListContains("config-field-row") == false) row = row.parent;
            if (row == null) return false;
            var tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker == null) return false;
            slider.AddToClassList("becs-filled-slider");
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("becs-slider-fill");
            tracker.Add(fill);
            update = () => {
                var percent = UnityEngine.Mathf.Clamp01(progress()) * 100f;
                var vertical = direction() == SliderDirection.Vertical;
                fill.style.left = vertical == true || inverted() == false ? 0f : StyleKeyword.Auto;
                fill.style.right = vertical == true || inverted() == true ? 0f : StyleKeyword.Auto;
                fill.style.top = vertical == false || inverted() == true ? 0f : StyleKeyword.Auto;
                fill.style.bottom = vertical == false || inverted() == false ? 0f : StyleKeyword.Auto;
                fill.style.width = vertical == true ? Length.Percent(100f) : Length.Percent(percent);
                fill.style.height = vertical == true ? Length.Percent(percent) : Length.Percent(100f);
            };
            var refresh = update;
            var dragger = slider.Q(className: "unity-base-slider__dragger");
            void AlignDragger() {
                if (dragger == null || dragger.panel == null) return;
                var vertical = direction() == SliderDirection.Vertical;
                var trackCenter = dragger.parent.WorldToLocal(tracker.worldBound.center);
                // Use an absolute cross-axis position: resolvedStyle.top/left can still
                // contain the previous percentage offset during a geometry callback.
                var position = vertical == true
                    ? trackCenter.x - dragger.layout.width * 0.5f - dragger.resolvedStyle.marginLeft - dragger.resolvedStyle.translate.x
                    : trackCenter.y - dragger.layout.height * 0.5f - dragger.resolvedStyle.marginTop - dragger.resolvedStyle.translate.y;
                if (float.IsNaN(position) == true || float.IsInfinity(position) == true) return;
                if (vertical == true) {
                    if (UnityEngine.Mathf.Abs(dragger.resolvedStyle.left - position) >= 0.1f) dragger.style.left = position;
                } else {
                    if (UnityEngine.Mathf.Abs(dragger.resolvedStyle.top - position) >= 0.1f) dragger.style.top = position;
                }
            }
            slider.RegisterCallback<GeometryChangedEvent>(evt => { refresh(); AlignDragger(); });
            tracker.RegisterCallback<GeometryChangedEvent>(evt => { refresh(); AlignDragger(); });
            dragger?.RegisterCallback<GeometryChangedEvent>(evt => AlignDragger());
            slider.schedule.Execute(AlignDragger);
            refresh();
            return true;
        }

        private static void ApplyThinScrollbar(Scroller scroller) {
            if (scroller.ClassListContains("becs-thin-scrollbar")) return;
            scroller.AddToClassList("becs-thin-scrollbar");
            var vertical = scroller.direction == SliderDirection.Vertical;
            void Size(VisualElement element) {
                if (vertical) {
                    element.style.width = ScrollbarThickness;
                    element.style.minWidth = ScrollbarThickness;
                    element.style.maxWidth = ScrollbarThickness;
                } else {
                    element.style.height = ScrollbarThickness;
                    element.style.minHeight = ScrollbarThickness;
                    element.style.maxHeight = ScrollbarThickness;
                }
            }
            void ClearTrack(VisualElement element) {
                element.style.backgroundImage = StyleKeyword.None;
                // Keep the thumb's USS colors, including hover/active theme states.
                if (!element.ClassListContains("unity-base-slider__dragger")) element.style.backgroundColor = UnityEngine.Color.clear;
                element.style.borderTopWidth = element.style.borderBottomWidth = 0;
                element.style.borderLeftWidth = element.style.borderRightWidth = 0;
                element.style.borderTopLeftRadius = element.style.borderTopRightRadius = 0;
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = 0;
                element.style.marginTop = element.style.marginBottom = 0;
                element.style.marginLeft = element.style.marginRight = 0;
                element.style.paddingTop = element.style.paddingBottom = 0;
                element.style.paddingLeft = element.style.paddingRight = 0;
            }
            ClearTrack(scroller);
            scroller.Query<VisualElement>().Build().ForEach(ClearTrack);
            scroller.lowButton.style.display = DisplayStyle.None;
            scroller.highButton.style.display = DisplayStyle.None;
            Size(scroller);
            Size(scroller.slider);
            var input = scroller.slider.Q(className: "unity-base-slider__input");
            if (input != null) Size(input);
            var thumb = scroller.slider.Q(className: "unity-base-slider__dragger");
            if (thumb != null) {
                Size(thumb);
                if (vertical) thumb.style.left = 0;
                else thumb.style.top = 0;
            }
        }

        public static void ApplyCommonStyles(VisualElement container) {
            TrackThinScrollbars(container);
            TrackFieldSliders(container);
            defaultStyleSheet ??= EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/DefaultStyles.uss");
            if (defaultStyleSheet != null && !container.styleSheets.Contains(defaultStyleSheet)) container.styleSheets.Add(defaultStyleSheet);
        }

        private static StyleSheet defaultStyleSheet;
        private static StyleSheet themeStyleSheet;
        private static StyleSheet prevThemeStyleSheet;
        private static string themeCurrentSelected;
        private static readonly System.Collections.Generic.HashSet<VisualElement> themeRoots = new System.Collections.Generic.HashSet<VisualElement>();
        public static void ApplyDefaultStyles(VisualElement container) {
            if (themeCurrentSelected != Themes.CurrentTheme) {
                prevThemeStyleSheet = themeStyleSheet;
                themeStyleSheet = null;
                themeCurrentSelected = Themes.CurrentTheme;
            }
            themeStyleSheet ??= ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>(Themes.CurrentTheme);
            if (prevThemeStyleSheet != null) container.styleSheets.Remove(prevThemeStyleSheet);
            ApplyCommonStyles(container);
            if (themeStyleSheet != null) {
                container.styleSheets.Remove(themeStyleSheet);
                container.styleSheets.Add(themeStyleSheet);
            }
            themeRoots.Add(container);
        }

        public static void RefreshStyles() {
            foreach (var root in themeRoots) {
                if (root != null) {
                    ApplyDefaultStyles(root);
                    var all = root.Query<GradientAnimated>();
                    all.Build().ForEach(x => x.MarkDirtyRepaint());
                }
            }
        }

        public static GradientAnimated AddLogoLine(VisualElement root) {
            var logo = root.Q<GradientAnimated>(className: "top-line");
            if (logo == null) {
                logo = new GradientAnimated();
                logo.AddToClassList("top-line");
                root.Add(logo);
            }
            return logo;
        }

        public static VisualElement AddWindowContent(VisualElement root, VisualElement currentContent) {
            var newRoot = new GradientAnimated();
            newRoot.AddToClassList("window-content-back");
            var newRootContent = new VisualElement();
            newRoot.Add(newRootContent);
            newRootContent.AddToClassList("window-content");
            newRootContent.Add(currentContent);
            root.Add(newRoot);
            return newRoot;
        }

    }

}