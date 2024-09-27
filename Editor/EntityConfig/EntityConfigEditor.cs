using System.Reflection;
using ME.BECS.Editor.Extensions.SubclassSelector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    [CustomEditor(typeof(EntityConfig))]
    public class EntityConfigEditor : UnityEditor.Editor {

        public StyleSheet styleSheetBase;
        public StyleSheet styleSheet;
        
        private void LoadStyle() {
            if (this.styleSheetBase == null) {
                this.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Entity.uss");
            }
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfig.uss");
            }
        }

        public override VisualElement CreateInspectorGUI() {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(this.styleSheetBase);
            rootVisualElement.styleSheets.Add(this.styleSheet);

            var serializedObject = this.serializedObject;
            this.DrawComponents(rootVisualElement, serializedObject);

            return rootVisualElement;
            
        }

        private Item componentsContainer;
        
        private void DrawComponents(VisualElement root, SerializedObject serializedObject) {

            var world = Context.world;
            var container = root;

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
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

                var componentsLabel = new Label("Components");
                componentsLabel.AddToClassList("entity-components-label");
                components.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                {
                    var componentContainer = new VisualElement();
                    componentsList.Add(componentContainer);

                    var data = serializedObject.FindProperty(nameof(EntityConfig.data));
                    var componentsData = data.FindPropertyRelative(nameof(EntityConfig.data.components));
                    this.componentsContainer = this.DrawFields(typeof(IConfigComponent), componentsData, serializedObject);
                    componentContainer.Add(this.componentsContainer.container);
                }
            }
            {
                
                var components = new VisualElement();
                components.AddToClassList("entity-components");
                components.AddToClassList("entity-shared-components");
                componentsContainer.Add(components);

                var componentsLabel = new Label("Shared Components");
                componentsLabel.AddToClassList("entity-components-label");
                components.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                {
                    var componentContainer = new VisualElement();
                    componentsList.Add(componentContainer);

                    var data = serializedObject.FindProperty(nameof(EntityConfig.sharedData));
                    var componentsData = data.FindPropertyRelative(nameof(EntityConfig.sharedData.components));
                    componentContainer.Add(this.DrawFields(typeof(IConfigComponentShared), componentsData, serializedObject).container);
                }
            }
            {
                
                var components = new VisualElement();
                components.AddToClassList("entity-components");
                components.AddToClassList("entity-static-components");
                componentsContainer.Add(components);

                var componentsLabel = new Label("Static Components");
                componentsLabel.AddToClassList("entity-components-label");
                components.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                {
                    var componentContainer = new VisualElement();
                    componentsList.Add(componentContainer);
                    
                    var data = serializedObject.FindProperty(nameof(EntityConfig.staticData));
                    var componentsData = data.FindPropertyRelative(nameof(EntityConfig.staticData.components));
                    componentContainer.Add(this.DrawFields(typeof(IComponentStatic), componentsData, serializedObject).container);
                }
            }
            {
                
                var components = new VisualElement();
                components.AddToClassList("entity-components");
                components.AddToClassList("entity-aspects");
                componentsContainer.Add(components);

                var componentsLabel = new Label("Aspects");
                componentsLabel.AddToClassList("entity-components-label");
                components.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                {
                    var componentContainer = new VisualElement();
                    componentsList.Add(componentContainer);
                    
                    var data = serializedObject.FindProperty(nameof(EntityConfig.aspects));
                    var componentsData = data.FindPropertyRelative(nameof(EntityConfig.aspects.components));
                    componentContainer.Add(this.DrawFields(typeof(IAspect), componentsData, serializedObject).container);
                }
            }
            
        }

        public struct Item {

            public VisualElement container;
            public VisualElement drawFieldsContainer;
            public System.Action<System.Collections.Generic.List<VisualElement>, int> updateButtons;

        }
        
        private Item DrawFields(System.Type type, SerializedProperty componentsArr, SerializedObject serializedObject) {

            Button removeButton = null;
            Button addButton = null;
            int selectedIndex = -1;
            void UpdateButtons(System.Collections.Generic.List<VisualElement> allProps, int selectIndex) {
                if (selectIndex >= -1) {
                    if (selectedIndex >= 0) allProps[selectedIndex].RemoveFromClassList("field-selected");
                    selectedIndex = selectIndex;
                    if (selectedIndex >= 0) allProps[selectedIndex].AddToClassList("field-selected");
                }

                foreach (var target in serializedObject.targetObjects) {
                    if (target is EntityConfig config) {
                        config.ResetCache();
                        try {
                            config.Sync();
                        } catch (System.Exception) {
                            // ignored
                        }
                    }
                }
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
                    this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, serializedObject.FindProperty(componentsArr.propertyPath), serializedObject);
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
                                this.DrawFields_INTERNAL(this.componentsContainer.updateButtons, this.componentsContainer.drawFieldsContainer, serializedObject.FindProperty(componentsData.propertyPath), serializedObject);
                            }
                        }
                        this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, serializedObject.FindProperty(componentsArr.propertyPath), serializedObject);
                    }, type, unmanagedTypes: true, runtimeAssembliesOnly: true, showNullElement: false);
                });
                addButton.text = "+";
                addButton.AddToClassList("add-button");
                buttons.Add(addButton);
            }

            this.DrawFields_INTERNAL(UpdateButtons, drawFieldsContainer, componentsArr, serializedObject);

            return new Item() {
                container = container,
                drawFieldsContainer = drawFieldsContainer,
                updateButtons = UpdateButtons,
            };

        }

        private static void AddComponent(SerializedObject serializedObject, SerializedProperty componentsArr, System.Type componentType) {
            var prop = serializedObject.FindProperty(componentsArr.propertyPath);
            ++prop.arraySize;
            var lastProp = prop.GetArrayElementAtIndex(prop.arraySize - 1);
            var obj = lastProp.SetManagedReference(componentType);
            lastProp.isExpanded = obj != null;
            lastProp.serializedObject.ApplyModifiedProperties();
            lastProp.serializedObject.Update();
        }

        private void DrawFields_INTERNAL(System.Action<System.Collections.Generic.List<VisualElement>, int> updateButtons, VisualElement container, SerializedProperty componentsArr, SerializedObject serializedObject) {
            
            container.Clear();

            var list = new System.Collections.Generic.List<VisualElement>();
            var dataArr = componentsArr;
            for (int i = 0; i < dataArr.arraySize; ++i) {

                var idx = i;
                var it = dataArr.GetArrayElementAtIndex(i);
                var copy = it.Copy();
                var type = EditorUtils.GetTypeFromPropertyField(it.managedReferenceFullTypename);
                var label = EditorUtils.GetComponentName(type);
                if (typeof(IAspect).IsAssignableFrom(type) == true) {

                    var fields = EditorUtils.GetAspectTypes(type);
                    
                    var labelField = new Foldout();
                    labelField.RegisterCallback<ClickEvent>((evt) => { updateButtons.Invoke(list, idx); });
                    labelField.text = label;
                    labelField.AddToClassList("field");

                    foreach (var field in fields) {

                        var labelFieldItem = new Label(EditorUtils.GetComponentName(field.fieldType));
                        if (field.required == true) {
                            labelFieldItem.AddToClassList("required");
                        } else {
                            labelFieldItem.AddToClassList("not-required");
                        }
                        labelField.Add(labelFieldItem);
                        
                    }
                    
                    container.Add(labelField);
                    list.Add(labelField);
                    
                } else if (copy.hasVisibleChildren == true) {
                    
                    var propertyField = new UnityEditor.UIElements.PropertyField(copy, label) {
                        name = $"PropertyField:{it.propertyPath}",
                    };
                    propertyField.RegisterCallback<ClickEvent>((evt) => { updateButtons.Invoke(list, idx); });
                    propertyField.AddToClassList("field");
                    propertyField.Bind(serializedObject);

                    System.Action rebuild = () => {
                        var lbl = propertyField.Q<Label>();
                        if (lbl != null) lbl.text = label;
                    };
                    propertyField.RegisterCallback<UnityEngine.UIElements.GeometryChangedEvent>((evt) => { rebuild.Invoke(); });
                    propertyField.RegisterCallback<AttachToPanelEvent>(new EventCallback<AttachToPanelEvent>((evt) => { rebuild.Invoke(); }));
                    propertyField.RegisterCallback<ChangeEvent<string>, PropertyField>((evt, p) => rebuild(), propertyField);
                    propertyField.RegisterCallback<ChangeEvent<StyleFont>, PropertyField>((evt, p) => rebuild(), propertyField);
                    propertyField.RegisterCallback<ChangeEvent<StyleFontDefinition>, PropertyField>((evt, p) => rebuild(), propertyField);
                    propertyField.RegisterCallback<ChangeEvent<StyleLength>, PropertyField>((evt, p) => rebuild(), propertyField);
                    
                    container.Add(propertyField);
                    list.Add(propertyField);

                } else {

                    var labelField = new Foldout();
                    labelField.RegisterCallback<ClickEvent>((evt) => { updateButtons.Invoke(list, idx); });
                    labelField.text = label;
                    labelField.AddToClassList("field");
                    container.Add(labelField);
                    list.Add(labelField);

                }

            }

            updateButtons.Invoke(list, -2);

        }

    }
    
}