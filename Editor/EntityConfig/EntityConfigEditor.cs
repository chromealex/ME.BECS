using System.Linq;
using ME.BECS.Editor.Extensions.SubclassSelector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    [CustomEditor(typeof(EntityConfig))]
    public class EntityConfigEditor : UnityEditor.Editor {

        public StyleSheet styleSheetBase;
        public StyleSheet styleSheetTooltip;
        public StyleSheet styleSheet;
        
        private void LoadStyle() {
            if (this.styleSheetBase == null) {
                this.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Entity.uss");
            }
            if (this.styleSheetTooltip == null) {
                this.styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfig.uss");
            }
        }

        public override VisualElement CreateInspectorGUI() {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            this.Build(rootVisualElement);
            this.rootVisualElement = rootVisualElement;
            return rootVisualElement;
            
        }

        private void Build(VisualElement rootVisualElement) {
            //rootVisualElement.Clear();
            EditorUIUtils.ApplyDefaultStyles(rootVisualElement);
            rootVisualElement.styleSheets.Add(this.styleSheetBase);
            rootVisualElement.styleSheets.Add(this.styleSheetTooltip);
            rootVisualElement.styleSheets.Add(this.styleSheet);

            EditorUIUtils.AddLogoLine(rootVisualElement);
            
            var serializedObject = this.serializedObject;
            this.DrawComponents(rootVisualElement, serializedObject);
        }

        private VisualElement rootVisualElement;
        private Item componentsContainer;
        private VisualElement componentsContainerRoot;
        private Item componentsSharedContainer;
        private VisualElement componentsSharedContainerRoot;
        private Item aspects;

        private void RedrawComponents() {
            
            var data = this.serializedObject.FindProperty(nameof(EntityConfig.data));
            var componentsData = data.FindPropertyRelative(nameof(EntityConfig.data.components));
            if (this.componentsContainer.container != null) {
                this.componentsContainer.container.RemoveFromHierarchy();
            }
            this.componentsContainer = this.DrawFields(typeof(IConfigComponent), data, componentsData, this.serializedObject);
            this.componentsContainerRoot.Add(this.componentsContainer.container);
            
        }

        private void RedrawSharedComponents() {
            
            var data = this.serializedObject.FindProperty(nameof(EntityConfig.sharedData));
            var componentsData = data.FindPropertyRelative(nameof(EntityConfig.sharedData.components));
            if (this.componentsSharedContainer.container != null) {
                this.componentsSharedContainer.container.RemoveFromHierarchy();
            }
            this.componentsSharedContainer = this.DrawFields(typeof(IConfigComponentShared), data, componentsData, this.serializedObject, false);
            this.componentsSharedContainerRoot.Add(this.componentsSharedContainer.container);
            
        }

        private void DrawComponents(VisualElement root, SerializedObject serializedObject) {

            var container = root;

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddManipulator(new ContextualMenuManipulator((menu) => {
                menu.menu.AppendAction("Copy CSV", (evt) => {
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
            scrollView.contentContainer.Add(componentsContainer);
            {
                var baseConfig = new PropertyField(serializedObject.FindProperty(nameof(EntityConfig.baseConfig)));
                baseConfig.AddToClassList("baseconfig-field");
                componentsContainer.Add(baseConfig);
            }
            {
                var components = new VisualElement();
                components.AddToClassList("entity-components");
                components.AddToClassList("entity-state-components");
                componentsContainer.Add(components);

                var tooltip = new VisualElement();
                components.Add(tooltip);
                EditorUIUtils.DrawTooltip(tooltip, "<b>IConfigComponent</b>\nWill be applied at runtime and stores on entity in world state.");
                var componentsLabel = new Label("Components");
                componentsLabel.AddToClassList("entity-components-label");
                tooltip.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                {
                    var componentContainer = new VisualElement();
                    this.componentsContainerRoot = componentContainer;
                    componentsList.Add(componentContainer);

                    this.RedrawComponents();
                }
            }
            {
                
                var components = new VisualElement();
                components.AddToClassList("entity-components");
                components.AddToClassList("entity-shared-components");
                componentsContainer.Add(components);

                var tooltip = new VisualElement();
                components.Add(tooltip);
                EditorUIUtils.DrawTooltip(tooltip, "<b>ISharedComponent</b>\nWill be applied at runtime and stores in special container in world state.");
                var componentsLabel = new Label("Shared Components");
                componentsLabel.AddToClassList("entity-components-label");
                tooltip.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                {
                    var componentContainer = new VisualElement();
                    this.componentsSharedContainerRoot = componentContainer;
                    componentsList.Add(componentContainer);

                    this.RedrawSharedComponents();
                }
            }
            {
                
                var components = new VisualElement();
                components.AddToClassList("entity-components");
                components.AddToClassList("entity-static-components");
                componentsContainer.Add(components);

                var tooltip = new VisualElement();
                components.Add(tooltip);
                EditorUIUtils.DrawTooltip(tooltip, "<b>IConfigStaticComponent</b>\nWill <b>not</b> be applied at runtime, it will stored in this config.");
                var componentsLabel = new Label("Static Components");
                componentsLabel.AddToClassList("entity-components-label");
                tooltip.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                {
                    var componentContainer = new VisualElement();
                    componentsList.Add(componentContainer);
                    
                    var data = serializedObject.FindProperty(nameof(EntityConfig.staticData));
                    var componentsData = data.FindPropertyRelative(nameof(EntityConfig.staticData.components));
                    componentContainer.Add(this.DrawFields(typeof(IConfigComponentStatic), data, componentsData, serializedObject, false).container);
                }
            }
            {
                
                var components = new VisualElement();
                components.AddToClassList("entity-components");
                components.AddToClassList("entity-aspects");
                componentsContainer.Add(components);
                
                var tooltip = new VisualElement();
                components.Add(tooltip);
                EditorUIUtils.DrawTooltip(tooltip, "<b>IAspect</b>\nWill be applied at runtime on entity with QueryWith attribute.");
                var componentsLabel = new Label("Aspects");
                componentsLabel.AddToClassList("entity-components-label");
                tooltip.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                {
                    var componentContainer = new VisualElement();
                    componentsList.Add(componentContainer);
                    
                    var data = serializedObject.FindProperty(nameof(EntityConfig.aspects));
                    var componentsData = data.FindPropertyRelative(nameof(EntityConfig.aspects.components));
                    this.aspects = this.DrawFields(typeof(IAspect), data, componentsData, serializedObject);
                    componentContainer.Add(this.aspects.container);
                }
            }
            {
                // maskable
                var prop = serializedObject.FindProperty(nameof(EntityConfig.maskable));
                var maskable = new Toggle("Maskable Config");
                EditorUIUtils.DrawTooltip(maskable, "<b>Maskable Config</b>\nChoose which fields in components you want to apply.");
                maskable.value = prop.boolValue;
                maskable.RegisterValueChangedCallback((evt) => {
                    if (this.rootVisualElement == null) return;
                    prop.boolValue = evt.newValue;
                    prop.serializedObject.ApplyModifiedProperties();
                    this.RedrawComponents();
                    this.RedrawSharedComponents();
                });
                maskable.AddToClassList("maskable-field");
                componentsContainer.Add(maskable);
            }
            
        }

        public struct Item {

            public VisualElement container;
            public VisualElement drawFieldsContainer;
            public System.Action<System.Collections.Generic.List<VisualElement>, int> updateButtons;
            public System.Action redrawFields;

        }

        private bool needSync = false;
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
                    if (selectedIndex >= 0) allProps[selectedIndex].RemoveFromClassList("field-selected");
                    selectedIndex = selectIndex;
                    if (selectedIndex >= 0) allProps[selectedIndex].AddToClassList("field-selected");
                }

                this.needSync = true;
                EditorApplication.delayCall = () => {
                    EditorApplication.delayCall = null;
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
                    if (selectedIndex >= 0) prop.DeleteArrayElementAtIndex(selectedIndex);
                    selectedIndex = -1;
                    serializedObject.ApplyModifiedProperties();
                    this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, serializedObject.FindProperty(dataContainer.propertyPath), serializedObject.FindProperty(componentsArr.propertyPath), serializedObject, useMaskable);
                    this.componentsContainer.redrawFields?.Invoke();
                });
                removeButton.text = "-";
                removeButton.AddToClassList("remove-button");
                buttons.Add(removeButton);
            }
            {
                addButton = new Button(() => {
                    var rect = buttons.worldBound;
                    EditorUtils.ShowPopup(rect, (type) => {
                        {
                            AddComponent(serializedObject, componentsArr, type);
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
                                    AddComponent(serializedObject, componentsData, item.fieldType);
                                    refreshRequired = true;
                                }
                            }

                            if (refreshRequired == true) {
                                this.DrawFields_INTERNAL(this.componentsContainer.updateButtons, this.componentsContainer.drawFieldsContainer, serializedObject.FindProperty(dataContainer.propertyPath), serializedObject.FindProperty(componentsData.propertyPath), serializedObject, useMaskable);
                            }
                        }
                        this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, serializedObject.FindProperty(dataContainer.propertyPath), serializedObject.FindProperty(componentsArr.propertyPath), serializedObject, useMaskable);
                    }, type, unmanagedTypes: true, runtimeAssembliesOnly: true, showNullElement: false);
                });
                addButton.text = "+";
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

        private static void AddComponent(SerializedObject serializedObject, SerializedProperty componentsArr, System.Type componentType) {
            var prop = serializedObject.FindProperty(componentsArr.propertyPath);
            ++prop.arraySize;
            var lastProp = prop.GetArrayElementAtIndex(prop.arraySize - 1);
            var obj = lastProp.CreateComponent(componentType);
            lastProp.isExpanded = obj != null;
            lastProp.serializedObject.ApplyModifiedProperties();
            lastProp.serializedObject.Update();
        }

        private void DrawFields_INTERNAL(System.Action<System.Collections.Generic.List<VisualElement>, int> updateButtons, VisualElement container, SerializedProperty dataContainer, SerializedProperty componentsArr, SerializedObject serializedObject, bool useMaskable) {
            
            container.Clear();

            var maskable = serializedObject.FindProperty("maskable").boolValue;
            var dataPropertyPath = dataContainer.propertyPath;
            
            var list = new System.Collections.Generic.List<VisualElement>();
            var dataArr = componentsArr;
            var masks = dataContainer.FindPropertyRelative("masks");
            if (masks.arraySize < dataArr.arraySize) {
                masks.arraySize = dataArr.arraySize;
            }
            for (int i = 0; i < dataArr.arraySize; ++i) {

                var idx = i;
                var it = dataArr.GetArrayElementAtIndex(i);
                var copy = it.Copy();
                var type = EditorUtils.GetTypeFromPropertyField(it.managedReferenceFullTypename);
                var label = EditorUtils.GetComponentName(type);
                if (typeof(IAspect).IsAssignableFrom(type) == true) {
                    
                    var fieldContainer = EditorUIUtils.DrawAspects(container, new System.Collections.Generic.List<EditorUtils.AspectItem>() { EditorUtils.GetAspect(type) });
                    list.AddRange(fieldContainer);
                    foreach (var fc in fieldContainer) {
                        var lbl = fc.Q(className: "aspect-component-container-field");
                        lbl.RegisterCallback<ClickEvent>((evt) => { updateButtons.Invoke(list, idx); });
                    }
                    
                } else if (copy.hasVisibleChildren == true) {

                    var propContainer = new VisualElement();
                    propContainer.AddToClassList("property-field-container");
                    
                    var propertyField = new UnityEditor.UIElements.PropertyField(copy, label) {
                        name = $"PropertyField:{it.propertyPath}",
                    };
                    propertyField.RegisterCallback<ClickEvent>((evt) => { updateButtons.Invoke(list, idx); });
                    propertyField.AddToClassList("field");
                    propertyField.Bind(serializedObject);

                    System.Action rebuild = null;
                    rebuild = () => {
                        var lbl = propertyField.Q<Label>();
                        if (lbl != null) lbl.text = label;

                        if (useMaskable == true && maskable == true) {
                            var foldout = propertyField.Query(className: "unity-foldout").First();
                            if (foldout != null) {
                                var container = foldout.Query(className: "unity-foldout__content").First();
                                var children = container.Children();
                                if (children != null && children.Count() > 1) {
                                    var list = children.ToList();
                                    var addContainers = new System.Collections.Generic.List<VisualElement>();
                                    var so = new SerializedObject(this.targets);
                                    var maskValues = so.FindProperty(dataPropertyPath).FindPropertyRelative("masks").GetArrayElementAtIndex(idx).FindPropertyRelative(nameof(ComponentsStorageBitMask.mask));
                                    if (maskValues.arraySize < list.Count) {
                                        maskValues.arraySize = list.Count;
                                    }
                                    for (int i = 0; i < list.Count; ++i) {
                                        var savedIndex = i;
                                        var item = list[i];
                                        if (item is not PropertyField) continue;
                                        item.RegisterCallbackOnce<UnityEngine.UIElements.GeometryChangedEvent>((evt) => { rebuild.Invoke(); });
                                        item.RegisterCallbackOnce<UnityEngine.UIElements.AttachToPanelEvent>((evt) => { rebuild.Invoke(); });
                                        item.RegisterCallbackOnce<UnityEngine.UIElements.DetachFromPanelEvent>((evt) => { rebuild.Invoke(); });
                                        if (item.userData is Toggle tlg) {
                                            tlg.RemoveFromHierarchy();
                                            addContainers.Add(tlg);
                                            continue;
                                        }

                                        static void ApplyState(VisualElement item, bool state) {
                                            item.RemoveFromClassList("checked");
                                            item.RemoveFromClassList("unchecked");
                                            if (state == true) {
                                                item.AddToClassList("checked");
                                            } else {
                                                item.AddToClassList("unchecked");
                                            }
                                        }

                                        item.AddToClassList("maskable-property-field");
                                        var toggle = new Toggle();
                                        toggle.RegisterValueChangedCallback(evt => {
                                            maskValues.GetArrayElementAtIndex(savedIndex).boolValue = evt.newValue;
                                            serializedObject.ApplyModifiedProperties();
                                            ApplyState(item, evt.newValue);
                                        });
                                        toggle.value = maskValues.GetArrayElementAtIndex(i).boolValue;
                                        ApplyState(item, toggle.value);
                                        toggle.AddToClassList("toggle-mask-field");
                                        addContainers.Add(toggle);
                                        item.userData = toggle;
                                        toggle.userData = item;
                                    }

                                    for (int i = addContainers.Count - 1; i >= 0; --i) {
                                        var addContainer = addContainers[i];
                                        if (addContainer.userData is VisualElement root) {
                                            root.Add(addContainer);
                                        }
                                    }
                                }
                            }
                        }

                    };
                    propertyField.RegisterCallback<UnityEngine.UIElements.GeometryChangedEvent>((evt) => { rebuild.Invoke(); });
                    propertyField.RegisterCallback<AttachToPanelEvent>(new EventCallback<AttachToPanelEvent>((evt) => { rebuild.Invoke(); }));
                    propertyField.RegisterCallback<UnityEngine.UIElements.FocusEvent>((evt) => { rebuild.Invoke(); });
                    propertyField.RegisterCallback<ChangeEvent<object>, PropertyField>((evt, p) => rebuild(), propertyField);
                    propertyField.RegisterCallback<ChangeEvent<string>, PropertyField>((evt, p) => rebuild(), propertyField);
                    propertyField.RegisterCallback<ChangeEvent<StyleFont>, PropertyField>((evt, p) => rebuild(), propertyField);
                    propertyField.RegisterCallback<ChangeEvent<StyleFontDefinition>, PropertyField>((evt, p) => rebuild(), propertyField);
                    propertyField.RegisterCallback<ChangeEvent<StyleLength>, PropertyField>((evt, p) => rebuild(), propertyField);
                    propertyField.AddManipulator(new ContextualMenuManipulator((menu) => {
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
                            propertyField.Bind(serializedObject);
                            propertyField.BindProperty(copy);
                        }, pasteStatus);
                        menu.menu.AppendAction("Copy CSV", (evt) => {
                            var csv = JSON.JsonUtils.ComponentToCSV(copy);
                            EditorUtils.Copy(csv);
                        });
                    }));

                    propContainer.Add(propertyField);

                    if (EditorUtils.TryGetComponentGroupColor(type, out var color) == true) {
                        color.a = 0.1f;
                        propertyField.style.backgroundColor = new StyleColor(color);
                    }
                    
                    container.Add(propContainer);
                    list.Add(propertyField);

                    EditorUIUtils.DrawTooltip(propContainer, EditorUtils.GetComponent(type)?.GetEditorComment());
                    rebuild.Invoke();
                    
                    this.DrawAspects(propContainer, type);

                } else {

                    var labelField = new Foldout();
                    var foldoutLabel = labelField.Q<Toggle>();
                    EditorUIUtils.DrawTooltip(foldoutLabel, EditorUtils.GetComponent(type)?.GetEditorComment());
                    labelField.RegisterCallback<ClickEvent>((evt) => { updateButtons.Invoke(list, idx); });
                    labelField.text = label;
                    labelField.AddToClassList("tag-component");
                    labelField.AddToClassList("field");
                    if (EditorUtils.TryGetComponentGroupColor(type, out var color) == true) {
                        color.a = 0.1f;
                        labelField.style.backgroundColor = new StyleColor(color);
                    }
                    container.Add(labelField);
                    list.Add(labelField);

                }

            }

            updateButtons.Invoke(list, -2);

        }

        private void DrawAspects(VisualElement propContainer, System.Type type) {
            
            var aspects = new VisualElement();
            aspects.AddToClassList("component-aspects");
            {
                var data = this.serializedObject.FindProperty(nameof(EntityConfig.aspects));
                var componentsData = data.FindPropertyRelative(nameof(EntityConfig.aspects.components));
                for (int j = 0; j < componentsData.arraySize; ++j) {

                    var itAspect = componentsData.GetArrayElementAtIndex(j);
                    var typeAspect = EditorUtils.GetTypeFromPropertyField(itAspect.managedReferenceFullTypename);
                    if (typeof(IAspect).IsAssignableFrom(typeAspect) == true) {

                        var label = EditorUtils.GetComponentName(typeAspect);
                        var fields = EditorUtils.GetAspectTypes(typeAspect);
                        foreach (var field in fields) {
                            if (field.fieldType == type) {
                                var aspect = new Label(label);
                                aspect.AddToClassList("component-aspect");
                                aspects.Add(aspect);
                                break;
                            }
                        }
                                    
                    }
                }
            }
            propContainer.Add(aspects);
            
        }

    }
    
}