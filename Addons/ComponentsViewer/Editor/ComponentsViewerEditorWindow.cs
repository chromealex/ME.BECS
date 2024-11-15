using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace ME.BECS.Editor.ComponentsViewer {

    public class ComponentsViewerEditorWindow : EditorWindow {

        [MenuItem("ME.BECS/Components Viewer...")]
        public static void ShowWindow() {

            var win = ComponentsViewerEditorWindow.GetWindow<ComponentsViewerEditorWindow>();
            win.titleContent = new GUIContent("Components Viewer", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-componentsviewer.png"));
            win.Show();

        }

        private static StyleSheet styleSheetBase;
        private static StyleSheet styleSheetTooltip;
        private static StyleSheet styleSheet;

        private static void LoadStyle() {
            if (styleSheetBase == null) {
                styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfig.uss");
            }

            if (styleSheetTooltip == null) {
                styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }

            if (styleSheet == null) {
                styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/ComponentsViewerEditorWindow.uss");
            }
        }

        public class ComponentColumnInfo {

            public System.Func<System.Type, bool> componentType;
            public string caption;
            public string tooltipOn;
            public string tooltipOff;
            public System.Action<EditorUtils.ComponentGroupItem.ComponentMetaInfo> onAdd;
            public System.Action<EditorUtils.ComponentGroupItem.ComponentMetaInfo> onRemove;
            public Toggle toggle;

        }

        private void CreateGUI() {

            LoadStyle();

            var componentData = new ComponentColumnInfo[] {
                new ComponentColumnInfo() {
                    componentType = (componentType) => typeof(IConfigComponent).IsAssignableFrom(componentType) || typeof(IConfigComponentStatic).IsAssignableFrom(componentType),
                    caption = "Config",
                    tooltipOn = "You can use <b>{componentLabel}</b> component is EntityConfig.",
                    tooltipOff = "<b>{componentLabel}</b> is a runtime-only component.",
                    onAdd = (component) => {
                        // Remove others
                        var isShared = typeof(IComponentShared).IsAssignableFrom(component.type);
                        if (isShared == true) {
                            // Replace shared with config shared
                            EditorUtils.UpdateComponentScript(component, typeof(IConfigComponent), true);
                            EditorUtils.UpdateComponentScript(component, typeof(IComponentShared), false);
                            EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentShared), true);
                        } else {
                            EditorUtils.UpdateComponentScript(component, typeof(IConfigComponent), true);
                        }

                        EditorUtils.UpdateComponentScript(component, typeof(IComponent), false);
                    },
                    onRemove = (component) => {
                        EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentStatic), false);
                        EditorUtils.UpdateComponentScript(component, typeof(IConfigComponent), false);
                        var isShared = typeof(IComponentShared).IsAssignableFrom(component.type);
                        if (isShared == true) {
                            EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentShared), false);
                            EditorUtils.UpdateComponentScript(component, typeof(IComponentShared), true);
                        }
                    },
                },
                new ComponentColumnInfo() {
                    componentType = (componentType) => typeof(IConfigComponentStatic).IsAssignableFrom(componentType),
                    caption = "Static",
                    tooltipOn = "<b>{componentLabel}</b> stored in EntityConfig only as static.",
                    tooltipOff = "<b>{componentLabel}</b> stored in state runtime memory.",
                    onAdd = (component) => {
                        // Remove others
                        EditorUtils.UpdateComponentScript(component, typeof(IComponentDestroy), false);
                        EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentShared), false);
                        EditorUtils.UpdateComponentScript(component, typeof(IComponentShared), false);
                        EditorUtils.UpdateComponentScript(component, typeof(IConfigComponent), false);
                        EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentStatic), true);
                        EditorUtils.UpdateComponentScript(component, typeof(IComponent), false);
                    },
                    onRemove = (component) => { EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentStatic), false); },
                },
                new ComponentColumnInfo() {
                    componentType = (componentType) => typeof(IComponentShared).IsAssignableFrom(componentType),
                    caption = "Shared",
                    tooltipOn = "<b>{componentLabel}</b> is <b>shared</b> component.",
                    tooltipOff = "<b>{componentLabel}</b> is <b>not shared</b> component.",
                    onAdd = (component) => {
                        // Remove others
                        EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentStatic), false);
                        var isConfig = typeof(IConfigComponent).IsAssignableFrom(component.type);
                        if (isConfig == true) {
                            // Replace shared with config shared
                            EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentShared), true);
                        } else {
                            EditorUtils.UpdateComponentScript(component, typeof(IComponentShared), true);
                        }

                        EditorUtils.UpdateComponentScript(component, typeof(IComponent), false);
                    },
                    onRemove = (component) => {
                        EditorUtils.UpdateComponentScript(component, typeof(IComponentShared), false);
                        EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentShared), false);
                    },
                },
                new ComponentColumnInfo() {
                    componentType = (componentType) => typeof(IComponentDestroy).IsAssignableFrom(componentType),
                    caption = "Destroy",
                    tooltipOn = "<b>{componentLabel}</b> is <b>auto-destroy</b> component.",
                    tooltipOff = "<b>{componentLabel}</b> is <b>not auto-destroy</b> component.",
                    onAdd = (component) => {
                        // Remove others
                        EditorUtils.UpdateComponentScript(component, typeof(IConfigComponentStatic), false);
                        EditorUtils.UpdateComponentScript(component, typeof(IComponentDestroy), true);
                        EditorUtils.UpdateComponentScript(component, typeof(IComponent), false);
                    },
                    onRemove = (component) => { EditorUtils.UpdateComponentScript(component, typeof(IComponentDestroy), false); },
                },
            };

            var root = this.rootVisualElement;
            var container = new VisualElement();
            container.styleSheets.Add(styleSheetBase);
            container.styleSheets.Add(styleSheet);
            container.styleSheets.Add(styleSheetTooltip);
            root.Add(container);

            var scrollView = new ScrollView();
            container.Add(scrollView);

            {
                var componentsContainer = new Foldout();
                componentsContainer.AddToClassList("main-container");
                componentsContainer.text = "Components";
                scrollView.Add(componentsContainer);

                var components = new VisualElement();
                components.AddToClassList("container");
                components.AddToClassList("fields-container");
                componentsContainer.Add(components);

                var componentGroups = EditorUtils.GetComponentGroups();
                foreach (var group in componentGroups) {

                    var groupFoldout = new Foldout();
                    groupFoldout.text = group.value;
                    groupFoldout.AddToClassList("container-group");
                    groupFoldout.AddToClassList("aspect-component-container-field");
                    components.Add(groupFoldout);

                    if (EditorUtils.TryGetGroupColor(group.type, out var color) == true) {
                        color.a = 0.1f;
                        groupFoldout.style.backgroundColor = new StyleColor(color);
                    }

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
                            foreach (var field in componentData) {
                                var column = new VisualElement();
                                column.AddToClassList("column");
                                var headerLabel = new Label(field.caption);
                                column.Add(headerLabel);
                                header.Add(column);
                            }
                        }
                        {
                            var column = new VisualElement();
                            column.AddToClassList("column");
                            var headerLabel = new Label("Size");
                            column.Add(headerLabel);
                            header.Add(column);
                        }
                        {
                            var column = new VisualElement();
                            column.AddToClassList("column");
                            column.AddToClassList("actions");
                            var headerLabel = new Label("Actions");
                            column.Add(headerLabel);
                            header.Add(column);
                        }
                        groupFoldout.Add(header);
                    }

                    for (var index = 0; index < group.components.Count; ++index) {

                        var component = group.components[index];
                        var componentContainer = new VisualElement();
                        componentContainer.AddToClassList("component-data");
                        componentContainer.AddToClassList("aspect-component-container");
                        groupFoldout.Add(componentContainer);

                        var componentLabel = EditorUtils.GetComponentName(component.type);
                        if (index == group.components.Count - 1) componentContainer.AddToClassList("last");
                        {
                            var column = new VisualElement();
                            column.AddToClassList("first-column");
                            var mainLabel = new Label(componentLabel);
                            mainLabel.AddToClassList("main-label");
                            column.Add(mainLabel);
                            componentContainer.Add(column);
                            var tooltip = (Label)EditorUIUtils.DrawTooltip(column, component.GetEditorComment());
                            column.RegisterCallback<ClickEvent>(x => {
                                if (x.clickCount == 2) {
                                    ComponentMetadataEditorWindow.ShowWindow(column, component, () => {
                                        if (tooltip == null) {
                                            tooltip = (Label)EditorUIUtils.DrawTooltip(column, component.GetEditorComment());
                                        } else if (string.IsNullOrEmpty(component.GetEditorComment()) == true) {
                                            EditorUIUtils.RemoveTooltip(tooltip.parent);
                                        }

                                        tooltip.text = component.GetEditorComment();
                                        EditorUtils.SaveComponentGroups();
                                    });
                                }
                            });
                        }

                        foreach (var field in componentData) {

                            var isEnabled = field.componentType.Invoke(component.type);
                            var column = new VisualElement();
                            column.AddToClassList("column");
                            var text = field.tooltipOff;
                            if (isEnabled == true) {
                                text = field.tooltipOn;
                            }

                            EditorUIUtils.DrawTooltip(column, text.Replace("{componentLabel}", componentLabel), new StyleLength(new Length(200f, LengthUnit.Pixel)));
                            var toggle = new Toggle();
                            toggle.SetEnabled(false);
                            toggle.value = isEnabled;
                            field.toggle = toggle;
                            column.Add(toggle);
                            componentContainer.Add(column);

                        }

                        {
                            var column = new VisualElement();
                            column.AddToClassList("column");
                            var size = System.Runtime.InteropServices.Marshal.SizeOf(component.type);
                            var sizeOf = GetSizeOf(component.type);
                            var lbl = new Label();
                            lbl.text = EditorUtils.BytesToString(size);
                            if (size != sizeOf) {
                                lbl.AddToClassList("field-size-warning");
                            }

                            var fields = component.GetFields();
                            if (fields.Length > 0) {
                                EditorUIUtils.DrawTooltip(column, () => {
                                    var table = new VisualElement();
                                    table.AddToClassList("tooltip-table");
                                    for (var i = 0; i < fields.Length; ++i) {
                                        var field = fields[i];
                                        var tr = new VisualElement();
                                        tr.AddToClassList("tooltip-table-tr");
                                        if (i == fields.Length - 1) tr.AddToClassList("tooltip-table-tr-last");
                                        table.Add(tr);
                                        var fieldSize = 0;
                                        var sizeOf = 0;
                                        if (field.FieldType.IsEnum == true) {
                                            fieldSize = 4;
                                            sizeOf = 4;
                                        } else {
                                            fieldSize = System.Runtime.InteropServices.Marshal.SizeOf(field.FieldType);
                                            sizeOf = GetSizeOf(field.FieldType);
                                        }

                                        var lblField = new Label(field.Name);
                                        lblField.AddToClassList("tooltip-table-field");
                                        if (fieldSize != sizeOf) {
                                            lblField.AddToClassList("field-size-warning");
                                        }

                                        tr.Add(lblField);
                                        var sizeField = new Label(EditorUtils.BytesToString(fieldSize));
                                        sizeField.AddToClassList("tooltip-table-size");
                                        tr.Add(sizeField);
                                    }

                                    return table;
                                }, new StyleLength(new Length(200f, LengthUnit.Pixel)));
                            }

                            column.Add(lbl);
                            componentContainer.Add(column);
                        }

                        {
                            var column = new VisualElement();
                            column.AddToClassList("column");
                            column.AddToClassList("actions");
                            if (component.isBuiltIn == true) {
                                var label = new Label("Built-in");
                                column.Add(label);
                            } else {
                                var open = new Button(() => {
                                    if (component.fileIsReady == true && component.file != null) {
                                        AssetDatabase.OpenAsset(component.file, component.lineNumber, component.columnNumber);
                                    }
                                });
                                open.text = "Open Script";
                                open.SetEnabled(false);
                                if (component.isBuiltIn == false && component.fileIsReady == false) {
                                    component.onFileReady += (state) => { UpdateOnScriptLoad(); };
                                } else {
                                    UpdateOnScriptLoad();
                                }

                                column.Add(open);

                                void UpdateOnScriptLoad() {
                                    var hasFile = component.fileIsReady == true && component.file != null;
                                    open.SetEnabled(hasFile);
                                    column.tooltip = component.GetTooltip();

                                    foreach (var field in componentData) {

                                        var elem = field;
                                        elem.toggle.SetEnabled(hasFile);
                                        elem.toggle.RegisterCallback<ChangeEvent<bool>>((evt) => {
                                            if (evt.newValue == true) {
                                                elem.onAdd?.Invoke(component);
                                            } else {
                                                elem.onRemove?.Invoke(component);
                                            }
                                        });

                                    }
                                }
                            }

                            componentContainer.Add(column);

                        }
                    }

                }
            }

            {
                var componentsContainer = new Foldout();
                componentsContainer.AddToClassList("main-container");
                componentsContainer.text = "Aspects";
                scrollView.Add(componentsContainer);

                var components = new VisualElement();
                components.AddToClassList("container");
                components.AddToClassList("fields-container");
                componentsContainer.Add(components);

                var aspects = EditorUtils.GetAspects();
                EditorUIUtils.DrawAspects(components, aspects, (visualElement, tooltip, item) => {
                    ComponentMetadataEditorWindow.ShowWindow(visualElement, item.info, () => {
                        if (tooltip == null) {
                            tooltip = (Label)EditorUIUtils.DrawTooltip(visualElement, item.info.GetEditorComment());
                        } else if (string.IsNullOrEmpty(item.info.GetEditorComment()) == true) {
                            EditorUIUtils.RemoveTooltip(tooltip.parent);
                        }

                        tooltip.text = item.info.GetEditorComment();
                        EditorUtils.SaveComponentGroups();
                    });
                });

            }

        }

        private static readonly System.Reflection.MethodInfo getSizeOfMethodInfo = typeof(ComponentsViewerEditorWindow).GetMethod(nameof(GetSizeOfMethod));

        private static int GetSizeOf(System.Type compType) {
            var gMethod = getSizeOfMethodInfo.MakeGenericMethod(compType);
            return (int)gMethod.Invoke(null, null);
        }

        public static unsafe int GetSizeOfMethod<T>() where T : unmanaged {
            return sizeof(T);
        }

    }

}