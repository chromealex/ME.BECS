using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;

    public unsafe class WorldEntityEditorWindow : UnityEditor.EditorWindow {

        public StyleSheet styleSheet;
        public Ent entity;

        public static void Show(Ent ent) {
            var win = WorldEntityEditorWindow.CreateInstance<WorldEntityEditorWindow>();
            win.entity = ent;
            win.titleContent = new GUIContent(ent, EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-entityview.png"));
            win.LoadStyle();
            win.Show();
        }

        /*[UnityEditor.MenuItem("ME.BECS/Entity View")]
        private static WorldEntityEditorWindow ShowWindow() {
            var window = GetWindow<WorldEntityEditorWindow>();
            window.titleContent = new UnityEngine.GUIContent("ME.BECS: Entity View");
            window.LoadStyle();
            window.ShowUtility();
            return window;
        }*/

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Entity.uss");
            }
        }

        public void CreateGUI() {

            this.LoadStyle();
            this.rootVisualElement.Clear();
            this.rootVisualElement.styleSheets.Add(this.styleSheet);

            var world = this.entity.World;
            if (world.isCreated == true) {

                if (this.entity.IsAlive() == false) {
                    return;
                }

                var container = this.rootVisualElement;
                container.Clear();

                this.DrawEntity(world);
                this.DrawComponents(world);

            }

        }

        /*public void Update() {
            
            var world = this.entity.world;
            if (world.isCreated == true) {

                if (this.entity.IsAlive() == false) {
                    return;
                }

                this.DrawComponents(world);

            }
            
        }*/

        private TempObject tempObject;
        private SerializedObject serializedObj;

        private void DrawEntity(World world) {
            
            var container = this.rootVisualElement;

            var header = new VisualElement();
            header.AddToClassList("entity-header");
            container.Add(header);
            
            var idContainer = new VisualElement();
            idContainer.AddToClassList("entity-id-container");
            header.Add(idContainer);
            var entityIdLabel = new Label("ID");
            entityIdLabel.AddToClassList("entity-id-label");
            idContainer.Add(entityIdLabel);
            var entityId = new Label(this.entity.id.ToString());
            entityId.AddToClassList("entity-id");
            idContainer.Add(entityId);

            {
                var genContainer = new VisualElement();
                genContainer.AddToClassList("entity-gen-container");
                header.Add(genContainer);
                var entityGenLabel = new Label("Generation");
                entityGenLabel.AddToClassList("entity-gen-label");
                genContainer.Add(entityGenLabel);
                var entityGen = new Label(this.entity.gen.ToString());
                entityGen.AddToClassList("entity-gen");
                genContainer.Add(entityGen);
            }
            
            {
                var archContainer = new VisualElement();
                archContainer.AddToClassList("entity-arch-container");
                header.Add(archContainer);
                var entityGenLabel = new Label("Archetype");
                entityGenLabel.AddToClassList("entity-arch-label");
                archContainer.Add(entityGenLabel);
                var entityGen = new Label($"#{world.state->archetypes.entToArchetypeIdx[this.entity.World.state->allocator, this.entity.id]}");
                entityGen.AddToClassList("entity-arch");
                archContainer.Add(entityGen);
            }
            
        }
        
        private void DrawComponents(World world) {

            var container = this.rootVisualElement;

            if (this.tempObject == null) {
                var c = TempObject.CreateInstance<TempObject>();
                this.tempObject = c;
                this.serializedObj = new SerializedObject(this.tempObject);
            }

            var methodSetComponent = typeof(Components).GetMethod(nameof(Components.SetDirect));
            var methodSetSharedComponent = typeof(Components).GetMethod(nameof(Components.SetSharedDirect));
            VisualElement componentContainerComponents;
            VisualElement componentContainerSharedComponents;
            
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            container.Add(scrollView);
            var componentsContainer = new VisualElement();
            scrollView.contentContainer.Add(componentsContainer);
            {
                var components = new VisualElement();
                components.AddToClassList("entity-components");
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
                    var archId = world.state->archetypes.entToArchetypeIdx[this.entity.World.state->allocator, this.entity.id];
                    var arch = world.state->archetypes.list[this.entity.World.state->allocator, archId];
                    
                    var methodRead = typeof(Components).GetMethod(nameof(Components.ReadDirect));
                    
                    this.tempObject.data = new object[arch.components.Count];
                    var i = 0;
                    var e = arch.components.GetEnumerator(world);
                    while (e.MoveNext() == true) {
                        var cId = e.Current;
                        var type = StaticTypesLoadedManaged.loadedTypes[cId];
                        {
                            var gMethod = methodRead.MakeGenericMethod(type);
                            var val = gMethod.Invoke(world.state->components, new object[] { this.entity });
                            this.tempObject.data[i] = val;
                        }
                        ++i;
                    }

                    componentContainerComponents = componentContainer;

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
                    var methodRead = typeof(Components).GetMethod(nameof(Components.ReadSharedDirect));
                    var methodHas = typeof(Components).GetMethod(nameof(Components.HasSharedDirect));
                    
                    var list = new System.Collections.Generic.List<object>();
                    foreach (var kv in StaticTypesLoadedManaged.loadedSharedTypes) {
                        var type = kv.Value;
                        {
                            var gHas = methodHas.MakeGenericMethod(type);
                            var has = (bool)gHas.Invoke(world.state->components, new object[] { this.entity });
                            if (has == true) {
                                var gMethod = methodRead.MakeGenericMethod(type);
                                var val = gMethod.Invoke(world.state->components, new object[] { this.entity });
                                list.Add(val);
                            }
                        }
                    }

                    this.tempObject.dataShared = list.ToArray();
                    
                    componentContainerSharedComponents = componentContainer;
                    
                }
            }
            
            this.serializedObj = new SerializedObject(this.tempObject);
                    
            componentContainerComponents.Add(this.DrawFields(world, this.tempObject.data, this.serializedObj, nameof(TempObject.data), methodSetComponent));
            componentContainerSharedComponents.Add(this.DrawFields(world, this.tempObject.dataShared, this.serializedObj, nameof(TempObject.dataShared), methodSetSharedComponent));
            
        }

        private VisualElement DrawFields(World world, object[] arrData, SerializedObject serializedObject, string field, System.Reflection.MethodInfo methodSet) {

            SerializedObject soEditor = serializedObject;
            var container = new UnityEngine.UIElements.VisualElement();
            container.AddToClassList("fields-container");

            var dataArr = soEditor.FindProperty(field);
            for (int i = 0; i < dataArr.arraySize; ++i) {

                var idx = i;
                var it = dataArr.GetArrayElementAtIndex(i);
                var copy = it.Copy();
                var label = EditorUtils.GetComponentName(arrData[i].GetType());
                if (copy.hasVisibleChildren == true) {
                    var propertyField = new UnityEditor.UIElements.PropertyField(copy, label) {
                        name = $"PropertyField:{it.propertyPath}",
                    };
                    propertyField.AddToClassList("field");
                    propertyField.Bind(soEditor);
                    System.Action rebuild = () => {
                        var allChilds = propertyField.Query<PropertyField>().ToList();
                        foreach (var child in allChilds) {
                            child.RegisterValueChangeCallback((evt) => {
                        
                                arrData[idx] = dataArr.GetArrayElementAtIndex(idx).managedReferenceValue;
                                var value = arrData[idx];
                                var gMethod = methodSet.MakeGenericMethod(value.GetType());
                                gMethod.Invoke(world.state->components, new object[] { this.entity, value });

                            });
                        }
                    };
                    propertyField.RegisterCallback<UnityEngine.UIElements.GeometryChangedEvent>((evt) => {
                        rebuild.Invoke();
                    });
                    propertyField.RegisterCallback<AttachToPanelEvent>(new EventCallback<AttachToPanelEvent>((evt) => {
                        rebuild.Invoke();
                    }));
                    container.Add(propertyField);
                } else {

                    var labelField = new Foldout();
                    labelField.text = label;
                    labelField.AddToClassList("field");
                    container.Add(labelField);

                }
                
            }

            return container;

        }

    }

}