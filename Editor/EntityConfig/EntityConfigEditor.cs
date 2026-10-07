using System.Linq;
using ME.BECS.Editor.Extensions.SubclassSelector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    [CustomEditor(typeof(EntityConfig))]
    [CanEditMultipleObjects]
    public partial class EntityConfigEditor : UnityEditor.Editor {

        private StyleSheet compactStyleSheet;
        private StyleSheet themeStyleSheet;

        private void LoadStyle() {
            this.compactStyleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfigCompact.uss");
        }

        public override bool UseDefaultMargins() => false;

        public override VisualElement CreateInspectorGUI() {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.RegisterCallback<WheelEvent>(evt => this.HideTooltip());
            this.rootVisualElement = rootVisualElement;
            this.Build(rootVisualElement);
            return rootVisualElement;
            
        }

        private void Build(VisualElement rootVisualElement) {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("compact-config-inspector");
            rootVisualElement.EnableInClassList("config-light", !EditorGUIUtility.isProSkin);
            rootVisualElement.styleSheets.Clear();
            this.ApplyTheme();

            var serializedObject = this.serializedObject;
            if (this.targets.Length > 1) this.DrawMultiComponents(rootVisualElement);
            else this.DrawComponents(rootVisualElement, serializedObject);
        }

        private VisualElement rootVisualElement;
        private Item componentsContainer;
        private sealed class SearchInfo {
            public SerializedObject owner;
            public string path;
            public string name;
            public string expansionKey;
        }

        private Label tooltipPopup;
        private string searchText = string.Empty;
        private bool showBaseComponents = true;
        private readonly System.Collections.Generic.Dictionary<string, bool> expandedComponents = new();
        private readonly System.Collections.Generic.Dictionary<EntityConfig, SerializedObject> baseSerializedObjects = new();

        private void OnEnable() {
            Undo.undoRedoPerformed += this.RebuildInspector;
            Themes.Changed += this.ApplyTheme;
        }

        private void OnDisable() {
            Undo.undoRedoPerformed -= this.RebuildInspector;
            Themes.Changed -= this.ApplyTheme;
            this.HideTooltip();
            this.ReleaseMultiObjects();
            this.ReleaseBaseObjects();
        }

        private void ApplyTheme() {
            if (this.rootVisualElement == null) return;
            this.HideTooltip();
            if (this.themeStyleSheet != null) this.rootVisualElement.styleSheets.Remove(this.themeStyleSheet);
            EditorUIUtils.ApplyCommonStyles(this.rootVisualElement);
            this.themeStyleSheet = EditorUtils.LoadResource<StyleSheet>(Themes.CurrentTheme);
            this.rootVisualElement.styleSheets.Add(this.themeStyleSheet);
            // Keep our layout and scoped control rules after the theme's global selectors.
            if (this.compactStyleSheet != null) {
                this.rootVisualElement.styleSheets.Remove(this.compactStyleSheet);
                this.rootVisualElement.styleSheets.Add(this.compactStyleSheet);
            }
        }

        private void ReleaseBaseObjects() {
            foreach (var item in this.baseSerializedObjects.Values) item.Dispose();
            this.baseSerializedObjects.Clear();
        }

        private void RebuildInspector() {
            if (this.rootVisualElement == null || this.target == null) return;
            this.HideTooltip();
            this.serializedObject.Update();
            this.ReleaseMultiObjects();
            this.ReleaseBaseObjects();
            this.Build(this.rootVisualElement);
        }

        private void DrawComponents(VisualElement root, SerializedObject serializedObject) {

            var container = root;

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddManipulator(new ContextualMenuManipulator((menu) => {
                menu.menu.AppendAction("Copy Entity Config CSV", (evt) => {
                    var data = new System.Text.StringBuilder();
                    {
                        var components = serializedObject.FindProperty(nameof(EntityConfig.data)).FindPropertyRelative(nameof(EntityConfig.data.components));
                        for (int i = 0; i < components.arraySize; ++i) {
                            var component = components.GetArrayElementAtIndex(i);
                            var csv = JSON.JsonUtils.ComponentToCSV(component);
                            data.Append(csv);
                        }
                    }
                    {
                        var components = serializedObject.FindProperty(nameof(EntityConfig.sharedData)).FindPropertyRelative(nameof(EntityConfig.sharedData.components));
                        for (int i = 0; i < components.arraySize; ++i) {
                            var component = components.GetArrayElementAtIndex(i);
                            var csv = JSON.JsonUtils.ComponentToCSV(component);
                            data.Append(csv);
                        }
                    }
                    {
                        var components = serializedObject.FindProperty(nameof(EntityConfig.staticData)).FindPropertyRelative(nameof(EntityConfig.staticData.components));
                        for (int i = 0; i < components.arraySize; ++i) {
                            var component = components.GetArrayElementAtIndex(i);
                            var csv = JSON.JsonUtils.ComponentToCSV(component);
                            data.Append(csv);
                        }
                    }
                    {
                        var components = serializedObject.FindProperty(nameof(EntityConfig.aspects)).FindPropertyRelative(nameof(EntityConfig.aspects.components));
                        for (int i = 0; i < components.arraySize; ++i) {
                            var component = components.GetArrayElementAtIndex(i);
                            var csv = JSON.JsonUtils.ComponentToCSV(component);
                            data.Append(csv);
                        }
                    }
                    EditorUtils.Copy(data.ToString());
                });
            }));
            container.Add(scrollView);
            var componentsContainer = new VisualElement();
            componentsContainer.AddToClassList("config-content");
            scrollView.contentContainer.Add(componentsContainer);
            var toolbar = new VisualElement();
            toolbar.AddToClassList("config-toolbar");
            componentsContainer.Add(toolbar);
            var baseConfig = new ObjectField("Base Config") {
                objectType = typeof(EntityConfig), allowSceneObjects = false,
            };
            var baseProperty = serializedObject.FindProperty(nameof(EntityConfig.baseConfig));
            baseConfig.SetValueWithoutNotify(baseProperty.objectReferenceValue);
            baseConfig.AddToClassList("baseconfig-field");
            baseConfig.RegisterValueChangedCallback(evt => {
                if (evt.newValue == evt.previousValue) return;
                var next = evt.newValue as EntityConfig;
                var visited = new System.Collections.Generic.HashSet<EntityConfig> { (EntityConfig)this.target };
                for (var config = next; config != null; config = config.baseConfig) {
                    if (visited.Add(config)) continue;
                    baseConfig.SetValueWithoutNotify(evt.previousValue);
                    EditorUtility.DisplayDialog("Base Config", "This reference would create a Base Config cycle.", "OK");
                    return;
                }
                serializedObject.Update();
                serializedObject.FindProperty(nameof(EntityConfig.baseConfig)).objectReferenceValue = next;
                serializedObject.ApplyModifiedProperties();
                this.needSync = true;
                EditorApplication.delayCall += this.Update;
                EditorApplication.delayCall += this.RebuildInspector;
            });
            toolbar.Add(baseConfig);

            var search = new TextField();
            search.textEdition.placeholder = "Search components or fields…";
            search.SetValueWithoutNotify(this.searchText);
            search.AddToClassList("config-search");
            search.RegisterValueChangedCallback(evt => {
                this.searchText = evt.newValue;
                this.ApplySearch();
            });

            if (baseProperty.objectReferenceValue != null) {
                var showBase = new Toggle("Show Base Components");
                showBase.SetValueWithoutNotify(this.showBaseComponents);
                showBase.RegisterValueChangedCallback(evt => {
                    this.showBaseComponents = evt.newValue;
                    this.RebuildInspector();
                });
                toolbar.Add(showBase);
            }
            var maskable = new Toggle("Maskable Config");
            maskable.SetValueWithoutNotify(serializedObject.FindProperty(nameof(EntityConfig.maskable)).boolValue);
            maskable.AddToClassList("maskable-field");
            maskable.tooltip = "Select which fields of Config Components are applied. Shared and Static Components are applied in full.";
            maskable.RegisterValueChangedCallback(evt => {
                if (evt.target != maskable || evt.newValue == evt.previousValue) return;
                // Commit before rebuilding; Update() in RebuildInspector must not discard
                // a pending value from UI Toolkit's asynchronous property binding.
                serializedObject.Update();
                serializedObject.FindProperty(nameof(EntityConfig.maskable)).boolValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                this.needSync = true;
                EditorApplication.delayCall += this.Update;
                this.rootVisualElement.schedule.Execute(this.RebuildInspector);
            });
            toolbar.Add(maskable);
            componentsContainer.Add(search);

            this.componentsContainer = this.DrawSection(componentsContainer, "Config Components", nameof(EntityConfig.data), typeof(IConfigComponent), true);
            this.DrawSection(componentsContainer, "Static Components", nameof(EntityConfig.staticData), typeof(IConfigComponentStatic), false);
            this.DrawSection(componentsContainer, "Shared Components", nameof(EntityConfig.sharedData), typeof(IConfigComponentShared), false);
            this.DrawSection(componentsContainer, "Aspects", nameof(EntityConfig.aspects), typeof(IAspect), false);
            this.ApplySearch();
        }

        private Item DrawSection(VisualElement parent, string title, string storageName, System.Type contract, bool useMaskable) {
            var section = new VisualElement();
            section.AddToClassList("entity-components");
            parent.Add(section);
            var label = new Label(title);
            label.AddToClassList("entity-components-label");
            label.userData = title;
            section.Add(label);
            var data = this.serializedObject.FindProperty(storageName);
            var components = data.FindPropertyRelative("components");
            var item = this.DrawFields(contract, data, components, this.serializedObject, useMaskable);
            section.Add(item.container);
            return item;
        }

        private void ApplySearch() {
            if (this.rootVisualElement == null) return;
            this.HideTooltip();
            this.rootVisualElement.Query<VisualElement>(className: "config-component-row").ForEach(row => {
                var info = row.userData as SearchInfo;
                var text = info?.name ?? row.userData as string ?? string.Empty;
                var componentMatch = MatchesText(text, this.searchText);
                var property = info?.owner.FindProperty(info.path);
                var visible = componentMatch || MatchesPropertyTree(property, this.searchText);
                row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (row is Foldout foldout) {
                    if (string.IsNullOrEmpty(this.searchText) && info != null) {
                        foldout.value = this.IsExpanded(info.expansionKey);
                    } else if (visible) {
                        foldout.value = true;
                    }
                }
                row.Query<VisualElement>(className: "config-field-row").ForEach(fieldRow => {
                    if (fieldRow.userData is not SearchInfo fieldInfo) return;
                    this.FilterField(fieldRow, fieldInfo, componentMatch);
                });
            });
            this.rootVisualElement.Query<Foldout>(className: "config-difference-group").ForEach(group => {
                var visible = string.IsNullOrEmpty(this.searchText) || group.Query<VisualElement>(className: "config-component-row").ToList()
                    .Any(row => row.style.display.value != DisplayStyle.None);
                group.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible && !string.IsNullOrEmpty(this.searchText)) group.value = true;
            });
            this.rootVisualElement.Query<VisualElement>(className: "drag-root").ForEach(handle => {
                handle.SetEnabled(string.IsNullOrEmpty(this.searchText));
            });
            this.rootVisualElement.Query<VisualElement>(className: "entity-components").ForEach(section => {
                var label = section.Q<Label>(className: "entity-components-label");
                if (label == null) return;
                var count = section.Query<VisualElement>(className: "config-component-row").ToList()
                    .Count(row => row.style.display.value != DisplayStyle.None);
                label.text = label.userData + " · " + count;
            });
        }

        private static bool MatchesText(string text, string query) {
            return string.IsNullOrEmpty(query) || (!string.IsNullOrEmpty(text) && text.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool MatchesPropertyTree(SerializedProperty property, string query) {
            if (property == null) return false;
            if (MatchesText(property.name, query) || MatchesText(property.displayName, query)) return true;
            var iterator = property.Copy();
            var end = property.GetEndProperty();
            while (iterator.NextVisible(true)) {
                if (SerializedProperty.EqualContents(iterator, end) || iterator.depth <= property.depth) break;
                if (MatchesText(iterator.name, query) || MatchesText(iterator.displayName, query)) return true;
            }
            return false;
        }

        private void FilterField(VisualElement row, SearchInfo info, bool componentMatch) {
            var property = info.owner.FindProperty(info.path);
            var showAll = componentMatch || MatchesText(property?.name, this.searchText) || MatchesText(property?.displayName, this.searchText);
            row.style.display = showAll || MatchesPropertyTree(property, this.searchText) ? DisplayStyle.Flex : DisplayStyle.None;
            row.Query<PropertyField>().ForEach(field => {
                if (string.IsNullOrEmpty(field.bindingPath)) return;
                var nested = info.owner.FindProperty(field.bindingPath);
                if (nested == null) return;
                var ancestorMatch = showAll;
                var parentPath = nested.propertyPath;
                while (!ancestorMatch && parentPath.Length > info.path.Length) {
                    var dot = parentPath.LastIndexOf('.');
                    if (dot < 0) break;
                    parentPath = parentPath.Substring(0, dot);
                    var parent = info.owner.FindProperty(parentPath);
                    ancestorMatch = parent != null && (MatchesText(parent.name, this.searchText) || MatchesText(parent.displayName, this.searchText));
                }
                var visible = ancestorMatch || MatchesPropertyTree(nested, this.searchText);
                field.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible && !string.IsNullOrEmpty(this.searchText)) {
                    // Unity creates nested PropertyFields lazily when their foldout opens.
                    field.Query<Foldout>().ForEach(foldout => {
                        if (foldout.parent == field || foldout == field.Q<Foldout>()) foldout.value = true;
                    });
                }
            });
        }

        private VisualElement DrawComponent(SerializedProperty component, System.Type type, string label,
                                            SerializedProperty masks, bool useMaskable, string source = null) {
            var key = this.ExpansionKey(component.serializedObject, component.propertyPath);
            var names = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var search = new SearchInfo {
                owner = component.serializedObject, path = component.propertyPath,
                name = label + " " + type.FullName, expansionKey = key,
            };
            var hasBase = this.serializedObject.FindProperty(nameof(EntityConfig.baseConfig)).objectReferenceValue != null;
            var title = hasBase ? label + (source == null ? "   · Local" : "   · Base: " + source) : label;
            if (!component.hasVisibleChildren) {
                var tag = new Label(title);
                tag.AddToClassList("config-component-row");
                tag.AddToClassList("config-tag-row");
                tag.userData = search;
                EditorUIUtils.ApplyComponentGroupColor(tag, type);
                this.BindTooltip(tag, FieldTooltip.Get(type));
                return tag;
            }
            var foldout = new Foldout { text = title };
            foldout.AddToClassList("config-component-row");
            foldout.AddToClassList("config-component-foldout");
            EditorUIUtils.ApplyComponentGroupColor(foldout, type);
            this.BindTooltip(foldout.Q<Toggle>(), FieldTooltip.Get(type));
            foldout.userData = search;
            foldout.SetValueWithoutNotify(this.IsExpanded(key));
            var built = false;
            void BuildFields() {
                if (built) return;
                built = true;
                var iterator = component.Copy();
                var end = component.GetEndProperty();
                if (!iterator.NextVisible(true)) return;
                do {
                    if (SerializedProperty.EqualContents(iterator, end) || iterator.depth <= component.depth) break;
                    if (iterator.depth != component.depth + 1) continue;
                    var field = iterator.Copy();
                    var propertyField = new PropertyField(field);
                    var row = new VisualElement();
                    row.AddToClassList("config-field-row");
                    var fieldSearch = new SearchInfo { owner = field.serializedObject, path = field.propertyPath };
                    row.userData = fieldSearch;
                    // Match mask bits by reflection field name, never by visible UI order.
                    var index = System.Array.FindIndex(names, name => name.Name == field.name);
                    if (useMaskable && index >= 0 && masks != null) {
                        var toggle = new Toggle();
                        toggle.AddToClassList("config-field-mask");
                        toggle.tooltip = "Apply " + field.displayName;
                        var maskPath = masks.propertyPath;
                        var so = component.serializedObject;
                        var values = so.FindProperty(maskPath);
                        toggle.SetValueWithoutNotify(index < values.arraySize && values.GetArrayElementAtIndex(index).boolValue);
                        toggle.showMixedValue = index < values.arraySize && values.GetArrayElementAtIndex(index).hasMultipleDifferentValues;
                        row.EnableInClassList("config-field-excluded", !toggle.showMixedValue && !toggle.value);
                        toggle.RegisterValueChangedCallback(evt => {
                            so.Update();
                            var bits = so.FindProperty(maskPath);
                            if (bits.arraySize < names.Length) bits.arraySize = names.Length;
                            bits.GetArrayElementAtIndex(index).boolValue = evt.newValue;
                            so.ApplyModifiedProperties();
                            toggle.showMixedValue = false;
                            this.CommitCommonComponent(so, component.propertyPath);
                            row.EnableInClassList("config-field-excluded", !evt.newValue);
                            this.needSync = true;
                            EditorApplication.delayCall += this.Update;
                        });
                        row.Add(toggle);
                        // Custom drawers may put their first label below the top of the property.
                        // Align the mask with that label, not the height of the whole expanded field.
                        void AlignMask() {
                            var firstLabel = propertyField.Query<Label>().ToList()
                                .FirstOrDefault(item => !item.ClassListContains("tooltip") && !item.ClassListContains("tooltip-text") && item.worldBound.height > 0f && item.resolvedStyle.display != DisplayStyle.None);
                            if (firstLabel == null || toggle.resolvedStyle.height <= 0f) return;
                            var offset = UnityEngine.Mathf.Max(0f, firstLabel.worldBound.center.y - row.worldBound.yMin - toggle.resolvedStyle.height * 0.5f);
                            if (UnityEngine.Mathf.Abs(toggle.resolvedStyle.marginTop - offset) > 0.5f) toggle.style.marginTop = offset;
                        }
                        propertyField.RegisterCallback<GeometryChangedEvent>(evt => AlignMask());
                        propertyField.RegisterCallback<AttachToPanelEvent>(evt => propertyField.schedule.Execute(AlignMask));
                    }
                    this.AddFieldTooltip(row, propertyField, field, type);
                    row.Add(propertyField);
                    this.RestorePropertyExpansion(field);
                    propertyField.BindProperty(field);
                    propertyField.RegisterCallback<GeometryChangedEvent>(evt => {
                        this.ConfigurePropertyExpansion(propertyField, field.serializedObject);
                        this.ConfigureTooltips(propertyField);
                        if (!string.IsNullOrEmpty(this.searchText)) this.FilterField(row, fieldSearch, MatchesText(search.name, this.searchText));
                    });
                    propertyField.RegisterCallback<AttachToPanelEvent>(evt => propertyField.schedule.Execute(() => {
                        this.ConfigurePropertyExpansion(propertyField, field.serializedObject);
                        this.ConfigureTooltips(propertyField);
                        this.FilterField(row, fieldSearch, MatchesText(search.name, this.searchText));
                    }));
                    if (source == null) propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt => {
                        this.CommitCommonComponent(field.serializedObject, component.propertyPath);
                        this.needSync = true;
                        EditorApplication.delayCall += this.Update;
                    });
                    foldout.Add(row);
                } while (iterator.NextVisible(false));
                if (source != null) foldout.contentContainer.SetEnabled(false);
            }
            foldout.RegisterValueChangedCallback(evt => {
                if (evt.target != foldout) return;
                if (string.IsNullOrEmpty(this.searchText)) this.SaveExpanded(key, evt.newValue);
                if (evt.newValue) BuildFields();
            });
            if (foldout.value) BuildFields();
            return foldout;
        }

        private string ExpansionKey(SerializedObject owner, string path) {
            var asset = owner.targetObject is EntityConfig ? owner.targetObject : this.target;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(asset);
            return "ME.BECS.EntityConfig.Expanded." + UnityEngine.Application.dataPath + ":" + id + ":" + path;
        }

        private bool IsExpanded(string key) {
            return this.expandedComponents.TryGetValue(key, out var expanded) == true ? expanded : EditorPrefs.GetBool(key, false);
        }

        private void SaveExpanded(string key, bool expanded) {
            if (string.IsNullOrEmpty(this.searchText) == false) return;
            this.expandedComponents[key] = expanded;
            EditorPrefs.SetBool(key, expanded);
        }

        private void RestorePropertyExpansion(SerializedProperty property) {
            var iterator = property.Copy();
            var end = property.GetEndProperty();
            do {
                var key = this.ExpansionKey(iterator.serializedObject, iterator.propertyPath);
                if (EditorPrefs.HasKey(key) == true) iterator.isExpanded = EditorPrefs.GetBool(key);
            } while (iterator.NextVisible(true) == true && SerializedProperty.EqualContents(iterator, end) == false);
        }

        private void ConfigurePropertyExpansion(PropertyField root, SerializedObject owner) {
            root.Query<Foldout>().ForEach(foldout => {
                if (foldout.ClassListContains("config-saved-expansion") == true) return;
                var propertyField = foldout.GetFirstAncestorOfType<PropertyField>();
                if (propertyField == null || string.IsNullOrEmpty(propertyField.bindingPath) == true) return;
                var key = this.ExpansionKey(owner, propertyField.bindingPath);
                foldout.AddToClassList("config-saved-expansion");
                if (string.IsNullOrEmpty(this.searchText) == true && EditorPrefs.HasKey(key) == true) foldout.value = this.IsExpanded(key);
                foldout.RegisterValueChangedCallback(evt => {
                    if (evt.target != foldout) return;
                    // List virtualization may rebind the same PropertyField to another element.
                    this.SaveExpanded(this.ExpansionKey(owner, propertyField.bindingPath), evt.newValue);
                });
            });
        }

        private void AddFieldTooltip(VisualElement row, PropertyField propertyField, SerializedProperty field, System.Type componentType) {
            // BECS TooltipAttribute is independent of Unity's SerializedProperty.tooltip.
            var fieldInfo = componentType.GetField(field.name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var text = FieldTooltip.Get(fieldInfo, field.tooltip);
            if (string.IsNullOrEmpty(text) == true) return;
            var question = new Label("?") { pickingMode = PickingMode.Position };
            question.AddToClassList("config-field-tooltip");
            propertyField.AddToClassList("config-has-field-tooltip");
            row.Add(question);
            this.BindTooltip(row, text);
        }

        private void BindTooltip(VisualElement anchor, string text) {
            if (anchor == null || string.IsNullOrEmpty(text) == true) return;
            anchor.pickingMode = PickingMode.Position;
            anchor.RegisterCallback<PointerOverEvent>(evt => { this.ShowTooltip(anchor, text); evt.StopPropagation(); });
            anchor.RegisterCallback<PointerLeaveEvent>(evt => this.HideTooltip());
            anchor.RegisterCallback<DetachFromPanelEvent>(evt => this.HideTooltip());
            anchor.RegisterCallback<TooltipEvent>(evt => evt.StopImmediatePropagation());
        }

        private void ConfigureTooltips(PropertyField propertyField) {
            propertyField.Query<VisualElement>(className: "has-tooltip").ForEach(decorator => {
                // Unity wraps decorators in a container after binding. Match their owning
                // property instead of relying on an immediate-child USS selector.
                var owner = decorator as PropertyField ?? decorator.GetFirstAncestorOfType<PropertyField>();
                if (owner == propertyField && propertyField.ClassListContains("config-has-field-tooltip") == true) {
                    if (decorator.ClassListContains("tooltip-decorator") == true) {
                        decorator.style.display = DisplayStyle.None;
                    } else {
                        foreach (var child in decorator.Children()) {
                            if (child.ClassListContains("tooltip") == true || child.ClassListContains("tooltip-text") == true) child.style.display = DisplayStyle.None;
                        }
                    }
                    return;
                }
                if (decorator.ClassListContains("config-tooltip-ready") == true) return;
                var text = decorator.Q<Label>(className: "tooltip-text")?.text;
                var anchor = decorator.ClassListContains("tooltip-decorator") == true ? decorator.parent : decorator;
                if (string.IsNullOrEmpty(text) == true || anchor == null) return;
                decorator.AddToClassList("config-tooltip-ready");
                this.BindTooltip(anchor, text);
            });
        }

        private void HideTooltip() {
            this.tooltipPopup?.RemoveFromHierarchy();
            this.tooltipPopup = null;
        }

        private void ShowTooltip(VisualElement anchor, string text) {
            this.HideTooltip();
            if (anchor.panel == null) return;
            var overlay = anchor.panel.visualTree;
            var popup = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
            // The panel root does not inherit the inspector's text font.
            popup.style.unityFont = anchor.resolvedStyle.unityFont;
            popup.style.unityFontDefinition = anchor.resolvedStyle.unityFontDefinition;
            popup.AddToClassList("config-tooltip-popup");
            popup.EnableInClassList("config-tooltip-light", !EditorGUIUtility.isProSkin);
            EditorUIUtils.ApplyCommonStyles(popup);
            popup.styleSheets.Add(this.themeStyleSheet);
            popup.styleSheets.Add(this.compactStyleSheet);
            var width = UnityEngine.Mathf.Min(360f, overlay.worldBound.width - 16f);
            popup.style.width = UnityEngine.Mathf.Max(80f, width);
            var position = overlay.WorldToLocal(anchor.worldBound.position);
            popup.style.left = UnityEngine.Mathf.Clamp(position.x, 8f, UnityEngine.Mathf.Max(8f, overlay.worldBound.width - width - 8f));
            popup.style.bottom = overlay.worldBound.height - position.y + 4f;
            // The panel overlay avoids clipping by section borders and the inspector ScrollView.
            overlay.Add(popup);
            popup.BringToFront();
            this.tooltipPopup = popup;
        }

        private VisualElement DrawAspect(SerializedProperty component, System.Type type, string label) {
            var key = this.ExpansionKey(component.serializedObject, component.propertyPath);
            var foldout = new Foldout { text = label };
            foldout.AddToClassList("config-component-row");
            foldout.AddToClassList("config-component-foldout");
            foldout.userData = label + " " + type.FullName;
            foldout.SetValueWithoutNotify(this.IsExpanded(key));
            foldout.RegisterValueChangedCallback(evt => {
                if (evt.target == foldout) this.SaveExpanded(key, evt.newValue);
            });
            foreach (var field in EditorUtils.GetAspectTypes(type)) {
                var row = new VisualElement();
                row.AddToClassList("config-aspect-row");
                var name = new Label(EditorUtils.GetComponentName(field.fieldType));
                name.AddToClassList("config-aspect-name");
                row.Add(name);
                row.Add(new Label(field.required ? "Required" : "Optional"));
                row.Add(new Label(field.config ? "Config" : "Runtime"));
                foldout.Add(row);
            }
            return foldout;
        }

        private void DrawBaseComponents(VisualElement container, string storageName, bool useMaskable) {
            if (!this.showBaseComponents) return;
            var current = ((EntityConfig)this.target).baseConfig;
            var visited = new System.Collections.Generic.HashSet<EntityConfig> { (EntityConfig)this.target };
            while (current != null) {
                if (!visited.Add(current)) {
                    container.Add(new HelpBox("Base Config contains a cycle.", HelpBoxMessageType.Error));
                    break;
                }
                if (!this.baseSerializedObjects.TryGetValue(current, out var so)) {
                    so = new SerializedObject(current);
                    this.baseSerializedObjects.Add(current, so);
                }
                so.Update();
                var data = so.FindProperty(storageName);
                var components = data.FindPropertyRelative("components");
                var masks = data.FindPropertyRelative("masks");
                for (var i = 0; i < components.arraySize; ++i) {
                    var component = components.GetArrayElementAtIndex(i).Copy();
                    var type = EditorUtils.GetTypeFromPropertyField(component.managedReferenceFullTypename);
                    if (type == null || typeof(IAspect).IsAssignableFrom(type)) continue;
                    var bits = i < masks.arraySize ? masks.GetArrayElementAtIndex(i).FindPropertyRelative("mask") : null;
                    var row = this.DrawComponent(component, type, EditorUtils.GetComponentName(type), bits, useMaskable && current.maskable, current.name);
                    row.AddToClassList("config-inherited-row");
                    container.Add(row);
                }
                current = current.baseConfig;
            }
        }

        public struct Item {

            public VisualElement container;
            public VisualElement drawFieldsContainer;
            public System.Action<System.Collections.Generic.List<VisualElement>, int> updateButtons;
            public System.Action redrawFields;

        }

        private bool needSync = false;
        private bool dragging;

        public void Update() {

            if (this.needSync == true) {
                if (UnityEngine.Application.isPlaying == true) return;
                this.needSync = false;
                foreach (var target in this.serializedObject.targetObjects) {
                    if (target is EntityConfig config) {
                        try {
                            config.Sync();
                        } catch (System.Exception ex) {
                            // ignored
                            UnityEngine.Debug.LogException(ex);
                        }
                    }
                }
            }

        }
        
        private Item DrawFields(System.Type type, SerializedProperty dataContainer, SerializedProperty componentsArr, SerializedObject serializedObject, bool useMaskable = true) {

            Button removeButton = null;
            Button addButton = null;
            int selectedIndex = -1;
            void UpdateButtons(System.Collections.Generic.List<VisualElement> allProps, int selectIndex) {
                if (selectIndex >= -1) {
                    if (selectedIndex >= 0 && selectedIndex < allProps.Count) allProps[selectedIndex].RemoveFromClassList("field-selected");
                    selectedIndex = selectIndex;
                    if (selectedIndex >= 0 && selectedIndex < allProps.Count) allProps[selectedIndex].AddToClassList("field-selected");
                }

                this.needSync = true;
                EditorApplication.delayCall += () => {
                    this.Update();
                };
                removeButton.SetEnabled(selectIndex >= 0);
            }
            
            var container = new UnityEngine.UIElements.VisualElement();
            container.AddToClassList("fields-container-root");

            var drawFieldsContainer = new VisualElement();
            drawFieldsContainer.AddToClassList("fields-container");
            container.Add(drawFieldsContainer);
            
            var buttons = new VisualElement();
            container.Add(buttons);
            buttons.AddToClassList("buttons-container");
            {
                removeButton = new Button(() => {
                    serializedObject.Update();
                    var prop = serializedObject.FindProperty(componentsArr.propertyPath);
                    var masksProp = serializedObject.FindProperty(dataContainer.propertyPath).FindPropertyRelative(nameof(EntityConfig.data.masks));
                    if (selectedIndex >= 0) {
                        prop.DeleteArrayElementAtIndex(selectedIndex);
                        masksProp.DeleteArrayElementAtIndex(selectedIndex);
                    }
                    selectedIndex = -1;
                    serializedObject.ApplyModifiedProperties();
                    this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, serializedObject.FindProperty(dataContainer.propertyPath), serializedObject.FindProperty(componentsArr.propertyPath), serializedObject, useMaskable);
                    this.componentsContainer.redrawFields?.Invoke();
                });
                removeButton.text = "Remove";
                removeButton.AddToClassList("remove-button");
                buttons.Add(removeButton);
            }
            {
                addButton = new Button(() => {
                    var rect = buttons.worldBound;
                    EditorUtils.ShowPopup(rect, (type) => {
                        {
                            AddComponent(serializedObject, dataContainer, componentsArr, type);
                            var key = this.ExpansionKey(serializedObject, componentsArr.propertyPath + ".Array.data[" + (componentsArr.arraySize - 1) + "]");
                            this.SaveExpanded(key, true);
                        }
                        if (typeof(IAspect).IsAssignableFrom(type) == true) {
                            // Add missing types
                            var aspectTypes = EditorUtils.GetAspectTypes(type);
                            var data = serializedObject.FindProperty(nameof(EntityConfig.data));
                            var componentsData = data.FindPropertyRelative(nameof(EntityConfig.data.components));
                            var refreshRequired = false;
                            foreach (var item in aspectTypes) {
                                if (item.config == false) continue;
                                var propItem = serializedObject.FindProperty(componentsData.propertyPath);
                                var found = false;
                                for (int i = 0; i < propItem.arraySize; ++i) {
                                    var elem = propItem.GetArrayElementAtIndex(i);
                                    var elemType = EditorUtils.GetTypeFromPropertyField(elem.managedReferenceFieldTypename);
                                    if (elemType == item.fieldType) {
                                        found = true;
                                        break;
                                    }
                                }
                                if (found == false) {
                                    // Add component
                                    AddComponent(serializedObject, data, componentsData, item.fieldType);
                                    refreshRequired = true;
                                }
                            }

                            if (refreshRequired == true) {
                                this.DrawFields_INTERNAL(this.componentsContainer.updateButtons, this.componentsContainer.drawFieldsContainer, data, serializedObject.FindProperty(componentsData.propertyPath), serializedObject, true);
                            }
                        }
                        this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, serializedObject.FindProperty(dataContainer.propertyPath), serializedObject.FindProperty(componentsArr.propertyPath), serializedObject, useMaskable);
                    }, type, unmanagedTypes: true, runtimeAssembliesOnly: true, showNullElement: false);
                });
                EditorUIUtils.ConfigureAddButton(addButton, "Component");
                addButton.AddToClassList("add-button");
                buttons.Add(addButton);
            }

            this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, dataContainer, componentsArr, serializedObject, useMaskable);

            return new Item() {
                container = container,
                drawFieldsContainer = drawFieldsContainer,
                updateButtons = UpdateButtons,
                redrawFields = () => this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, dataContainer, componentsArr, serializedObject, useMaskable),
            };

        }

        private static void AddComponent(SerializedObject serializedObject, SerializedProperty dataContainer, SerializedProperty componentsArr, System.Type componentType) {
            var prop = serializedObject.FindProperty(componentsArr.propertyPath);
            var masksProp = serializedObject.FindProperty(dataContainer.propertyPath).FindPropertyRelative(nameof(EntityConfig.data.masks));
            masksProp.arraySize = UnityEngine.Mathf.Max(masksProp.arraySize, prop.arraySize + 1);
            ++prop.arraySize;
            var lastProp = prop.GetArrayElementAtIndex(prop.arraySize - 1);
            var mask = masksProp.GetArrayElementAtIndex(prop.arraySize - 1);
            mask.FindPropertyRelative(nameof(ComponentsStorageBitMask.mask)).arraySize = componentType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Length; 
            lastProp.CreateComponent(componentType);
            lastProp.isExpanded = false;
            lastProp.serializedObject.ApplyModifiedProperties();
            lastProp.serializedObject.Update();
        }

        private void DrawFields_INTERNAL(System.Action<System.Collections.Generic.List<VisualElement>, int> updateButtons, VisualElement container, SerializedProperty dataContainer, SerializedProperty componentsArr, SerializedObject serializedObject, bool useMaskable) {
            
            container.Clear();

            var maskable = serializedObject.FindProperty("maskable").boolValue;
            
            var list = new System.Collections.Generic.List<VisualElement>();
            var dataArr = componentsArr;
            var masks = dataContainer.FindPropertyRelative("masks");
            if (masks.arraySize < dataArr.arraySize) {
                masks.arraySize = dataArr.arraySize;
                serializedObject.ApplyModifiedProperties();
            }

            var dragHandler = new VisualElement();
            dragHandler.style.visibility = Visibility.Hidden;
            {
                dragHandler.AddToClassList("drag-handler");
                {
                    var decorator = new VisualElement();
                    decorator.AddToClassList("left");
                    dragHandler.Add(decorator);
                }
                {
                    var decorator = new VisualElement();
                    decorator.AddToClassList("right");
                    dragHandler.Add(decorator);
                }
                container.Add(dragHandler);
            }

            for (int i = 0; i < dataArr.arraySize; ++i) {

                VisualElement rootElement = null;

                var idx = i;
                var it = dataArr.GetArrayElementAtIndex(i);
                var copy = it.Copy();
                var type = EditorUtils.GetTypeFromPropertyField(it.managedReferenceFullTypename);
                if (type == null) {
                    var missing = new HelpBox("Missing component type. Remove or restore its script.", HelpBoxMessageType.Warning);
                    var missingIndex = list.Count;
                    missing.RegisterCallback<ClickEvent>(evt => updateButtons.Invoke(list, missingIndex));
                    list.Add(missing);
                    container.Add(missing);
                    continue;
                }
                var label = EditorUtils.GetComponentName(type);
                if (typeof(IAspect).IsAssignableFrom(type) == true) {
                    
                    rootElement = this.DrawAspect(copy, type, label);
                    rootElement.RegisterCallback<ClickEvent>(evt => updateButtons.Invoke(list, idx));
                    container.Add(rootElement);
                    list.Add(rootElement);
                } else {
                    var bits = masks.GetArrayElementAtIndex(i).FindPropertyRelative("mask");
                    rootElement = this.DrawComponent(copy, type, label, bits, useMaskable && maskable);
                    rootElement.AddToClassList("field");
                    rootElement.RegisterCallback<ClickEvent>(evt => updateButtons.Invoke(list, idx));
                    container.Add(rootElement);
                    list.Add(rootElement);
                }

                if (rootElement != null) {

                    rootElement.AddManipulator(new ContextualMenuManipulator((menu) => {
                        var listIdx = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(2, Unity.Collections.Allocator.Temp);
                        var items = menu.menu.MenuItems();
                        for (int index = 0; index < items.Count; ++index) {
                            var dropdownMenuItem = items[index];
                            if (dropdownMenuItem is DropdownMenuAction d && (d.name.Equals("Delete Array Element") == true || d.name.Equals("Duplicate Array Element") == true)) {
                                listIdx.Add(index);
                            }
                        }
                        for (int index = listIdx.Length - 1; index >= 0; --index) {
                            int j = listIdx[index];
                            menu.menu.RemoveItemAt(j);
                        }
                        menu.menu.AppendAction("Move Up", (evt) => {
                            copy.serializedObject.Update();
                            dataArr.MoveArrayElement(idx, idx - 1);
                            masks.MoveArrayElement(idx, idx - 1);
                            copy.serializedObject.ApplyModifiedProperties();
                            copy.serializedObject.Update();
                            Redraw();
                        }, idx == 0 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                        menu.menu.AppendAction("Move Down", (evt) => {
                            copy.serializedObject.Update();
                            dataArr.MoveArrayElement(idx, idx + 1);
                            masks.MoveArrayElement(idx, idx + 1);
                            copy.serializedObject.ApplyModifiedProperties();
                            copy.serializedObject.Update();
                            Redraw();
                        }, idx == dataArr.arraySize - 1 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                        if (copy.hasVisibleChildren) {
                            menu.menu.AppendSeparator();
                            menu.menu.AppendAction("Copy JSON", (evt) => {
                                var json = JSON.JsonUtils.ComponentToJSON(copy);
                                EditorUtils.Copy(json);
                            });
                            var pasteStatus = DropdownMenuAction.Status.Normal;
                            var buffer = EditorUtils.ReadCopyBuffer();
                            if (string.IsNullOrEmpty(buffer) == true) {
                                pasteStatus = DropdownMenuAction.Status.Disabled;
                            } else if (JSON.JsonUtils.IsValidJson(buffer) == false) {
                                pasteStatus = DropdownMenuAction.Status.Disabled;
                            }
                            menu.menu.AppendAction("Paste JSON", (evt) => {
                                copy.serializedObject.Update();
                                JSON.JsonUtils.JSONToComponent(buffer, copy);
                                copy.serializedObject.ApplyModifiedProperties();
                                copy.serializedObject.Update();
                                Redraw();
                            }, pasteStatus);
                            menu.menu.AppendAction("Copy CSV", (evt) => {
                                var csv = JSON.JsonUtils.ComponentToCSV(copy);
                                EditorUtils.Copy(csv);
                            });
                        }
                    }));

                    var dragRoot = new VisualElement();
                    dragRoot.AddToClassList("drag-root");
                    rootElement.hierarchy.Add(dragRoot);

                    dragRoot.RegisterCallback<PointerDownEvent>((evt) => {
                        // show handler
                        dragHandler.style.visibility = Visibility.Visible;
                        FindClosestSlot(rootElement, evt.position, out _, out var pos);
                        dragHandler.style.top = pos.y - rootElement.parent.worldBound.y;
                        dragRoot.CapturePointer(evt.pointerId);
                        this.dragging = true;
                    });
                    dragRoot.RegisterCallback<PointerMoveEvent>((evt) => {
                        if (this.dragging == true && dragRoot.HasPointerCapture(evt.pointerId) == true) {
                            FindClosestSlot(rootElement, evt.position, out _, out var pos);
                            dragHandler.style.top = pos.y - rootElement.parent.worldBound.y;
                        }
                    });
                    dragRoot.RegisterCallback<PointerUpEvent>((evt) => {
                        if (this.dragging == true && dragRoot.HasPointerCapture(evt.pointerId) == true) {
                            FindClosestSlot(rootElement, evt.position, out var index, out _);
                            copy.serializedObject.Update();
                            dataArr.MoveArrayElement(idx, index);
                            masks.MoveArrayElement(idx, index);
                            copy.serializedObject.ApplyModifiedProperties();
                            copy.serializedObject.Update();
                            dragRoot.ReleasePointer(evt.pointerId);
                            Redraw();
                        }
                    });
                    dragRoot.RegisterCallback<PointerCaptureOutEvent>((evt) => {
                        if (this.dragging == true) {
                            dragHandler.style.visibility = Visibility.Hidden;
                            this.dragging = false;
                        }
                    });
                }

            }

            this.DrawBaseComponents(container, dataContainer.propertyPath, useMaskable);
            this.ApplySearch();
            updateButtons.Invoke(list, -2);

            return;

            VisualElement FindClosestSlot(VisualElement drag, UnityEngine.Vector2 position, out int index, out UnityEngine.Vector2 pos) {
                var idx = -1;
                for (var i = 0; i < list.Count; ++i) {
                    var slot = list[i];
                    if (slot == drag) {
                        idx = i;
                        break;
                    }
                }

                pos = default;
                index = -1;
                float bestDistanceSq = float.MaxValue;
                VisualElement closest = null;
                for (var i = 0; i < list.Count + 1; ++i) {
                    var slot = i >= list.Count ? null : list[i];
                    UnityEngine.Vector2 displacement;
                    UnityEngine.Vector2 offset;
                    if (slot == null) {
                        // bottom
                        offset = new UnityEngine.Vector2(0f, list[list.Count - 1].worldBound.yMax);
                        displacement = position - offset;
                    } else {
                        offset = new UnityEngine.Vector2(0f, slot.worldBound.yMin);
                        displacement = position - offset;
                    }
                    float distanceSq = displacement.sqrMagnitude;
                    if (distanceSq < bestDistanceSq) {
                        pos = offset;
                        index = i;
                        bestDistanceSq = distanceSq;
                        closest = slot;
                    }
                }

                var last = false;
                if (index >= list.Count) {
                    index = list.Count - 1;
                    last = true;
                }
                if (idx < index && last == false) {
                    index = UnityEngine.Mathf.Clamp(index - 1, 0, list.Count - 1);
                }

                return closest;
            }
            
            void Redraw() {
                this.DrawFields_INTERNAL(updateButtons, container, dataContainer, componentsArr, serializedObject, useMaskable);
            }

        }

    }
    
}
