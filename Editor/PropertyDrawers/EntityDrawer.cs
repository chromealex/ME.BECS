using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ME.BECS.Editor.Extensions.SubclassSelector;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(Ent))]
    public unsafe class EntityDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        private static StyleSheet styleSheet;
        
        private void LoadStyle() {
            if (EntityDrawer.styleSheetBase == null) {
                EntityDrawer.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Entity.uss");
            }
            if (EntityDrawer.styleSheet == null) {
                EntityDrawer.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfig.uss");
            }
        }

        ~EntityDrawer() {

            this.propertyPath = null;
            this.property = null;
            this.propertySerializedObject = null;

        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("entity-mini");
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(EntityDrawer.styleSheetBase);
            rootVisualElement.styleSheets.Add(EntityDrawer.styleSheet);
            this.rootVisualElement = rootVisualElement;

            this.entity = (Ent)PropertyEditorUtils.GetTargetObjectOfProperty(property);
            this.propertyPath = property.propertyPath;
            this.propertySerializedObject = property.serializedObject;
            this.property = property;
            this.DrawEntity(rootVisualElement, this.entity.World);

            EditorApplication.update += this.OnUpdate;
            
            return rootVisualElement;

        }

        private bool prevState;
        private SerializedProperty property;
        private string propertyPath;
        private SerializedObject propertySerializedObject;
        private void OnUpdate() {

            if (PropertyEditorUtils.IsValid(this.property) == false) {
                EditorApplication.update -= this.OnUpdate;
                return;
            }

            var obj = PropertyEditorUtils.GetTargetObjectOfProperty(this.property ?? this.propertySerializedObject.FindProperty(this.propertyPath));
            if (obj == null) return;
            
            this.entity = (Ent)obj;
            var world = this.entity.World;
            if (this.entity.IsAlive() == true && this.version != this.entity.Version) {
                this.FetchDataFromEntity(world);
            }
            var newState = this.GetState(world);
            if (this.prevState != newState ||
                (newState == true && this.archId != this.GetArchId())) {
                this.DrawEntity(this.rootVisualElement, world);
            } else if (newState == true) {
                this.UpdateData();
            }
            
        }

        private bool GetState(World world) {

            return world.isCreated == true &&
                   this.entity.IsAlive() == true;
            
        }

        private uint version;
        private Ent entity;
        private uint archId;
        private VisualElement rootVisualElement;
        private TempObject tempObject;
        private SerializedObject serializedObj;

        private Label versionLabel;

        private uint GetArchId() {
            var world = this.entity.World;
            return world.state->archetypes.entToArchetypeIdx[world.state->allocator, this.entity.id];
        }

        private void UpdateData() {

            this.versionLabel.text = this.entity.Version.ToString();

        }
        
        private void DrawEntity(VisualElement root, World world) {
            
            var container = root;
            container.Clear();

            var toggleContainer = new VisualElement();
            var header = new Toggle();
            header.RegisterValueChangedCallback((evt) => {
                toggleContainer.style.display = new StyleEnum<DisplayStyle>(evt.newValue == true ? DisplayStyle.Flex : DisplayStyle.None);
                header.RemoveFromClassList("toggle-checked");
                if (evt.newValue == true) header.AddToClassList("toggle-checked");
            });
            toggleContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            header.AddToClassList("entity-header");
            container.Add(header);
            container.Add(toggleContainer);
            
            {
                var idContainer = new VisualElement();
                idContainer.AddToClassList("entity-id-container");
                header.Add(idContainer);
                var entityIdLabel = new Label("ID");
                entityIdLabel.AddToClassList("entity-id-label");
                entityIdLabel.AddToClassList("label-header");
                idContainer.Add(entityIdLabel);
                var entityId = new Label(this.entity.id.ToString());
                entityId.AddToClassList("entity-id");
                entityId.AddToClassList("label-value");
                idContainer.Add(entityId);
            }

            {
                var genContainer = new VisualElement();
                genContainer.AddToClassList("entity-gen-container");
                header.Add(genContainer);
                var entityGenLabel = new Label("Generation");
                entityGenLabel.AddToClassList("entity-gen-label");
                entityGenLabel.AddToClassList("label-header");
                genContainer.Add(entityGenLabel);
                var entityGen = new Label(this.entity.gen.ToString());
                entityGen.AddToClassList("entity-gen");
                entityGen.AddToClassList("label-value");
                genContainer.Add(entityGen);
            }
            
            if (this.GetState(world) == false) {

                this.prevState = false;
                root.AddToClassList("entity-not-alive");
                
                if (this.entity.IsEmpty() == true) {

                    var notAliveContainer = new Label("Entity is empty");
                    notAliveContainer.AddToClassList("entity-not-alive");
                    header.Add(notAliveContainer);

                } else {

                    var notAliveContainer = new Label("Entity is not alive");
                    notAliveContainer.AddToClassList("entity-not-alive");
                    header.Add(notAliveContainer);
                    
                }

            } else {

                {
                    var versionContainer = new VisualElement();
                    versionContainer.AddToClassList("entity-version-container");
                    header.Add(versionContainer);
                    var entityGenLabel = new Label("Version");
                    entityGenLabel.AddToClassList("entity-version-label");
                    entityGenLabel.AddToClassList("label-header");
                    versionContainer.Add(entityGenLabel);
                    var entityVersion = new Label(this.entity.Version.ToString());
                    this.versionLabel = entityVersion;
                    entityVersion.AddToClassList("entity-version");
                    entityVersion.AddToClassList("label-value");
                    versionContainer.Add(entityVersion);
                }

                this.prevState = true;
                root.RemoveFromClassList("entity-not-alive");
                {
                    var archContainer = new VisualElement();
                    archContainer.AddToClassList("entity-arch-container");
                    header.Add(archContainer);
                    var entityArchLabel = new Label("Archetype");
                    entityArchLabel.AddToClassList("entity-arch-label");
                    entityArchLabel.AddToClassList("label-header");
                    archContainer.Add(entityArchLabel);
                    var archId = this.GetArchId();
                    this.archId = archId;
                    var entityArch = new Label($"#{archId}");
                    entityArch.AddToClassList("entity-arch");
                    entityArch.AddToClassList("label-value");
                    archContainer.Add(entityArch);
                }

                var rootComponents = new VisualElement();
                this.DrawComponents(rootComponents, world);
                toggleContainer.Add(rootComponents);
                
            }

        }
        
        private static readonly System.Reflection.MethodInfo methodSetComponent = typeof(Components).GetMethod(nameof(Components.SetDirect));
        private static readonly System.Reflection.MethodInfo methodSetSharedComponent = typeof(Components).GetMethod(nameof(Components.SetSharedDirect));
        private VisualElement componentContainerComponents;
        private VisualElement componentContainerSharedComponents;
        private VisualElement componentContainerComponentsRoot;
        private VisualElement componentContainerSharedComponentsRoot;
        private void DrawComponents(VisualElement root, World world) {

            this.cachedFieldsComponents.Clear();
            this.cachedFieldsSharedComponents.Clear();
            
            var container = root;

            this.FetchDataFromEntity(world);
            
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            container.Add(scrollView);
            var componentsContainer = new VisualElement();
            scrollView.contentContainer.Add(componentsContainer);
            {
                var components = new VisualElement();
                this.componentContainerComponentsRoot = components;
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
                    componentContainer.AddToClassList("fields-container");
                    componentsList.Add(componentContainer);
                    var componentContainerComponents = componentContainer;
                    this.componentContainerComponents = componentContainerComponents;
                }
            }
            {
                var components = new VisualElement();
                this.componentContainerSharedComponentsRoot = components;
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
                    componentContainer.AddToClassList("fields-container");
                    componentsList.Add(componentContainer);
                    var componentContainerSharedComponents = componentContainer;
                    this.componentContainerSharedComponents = componentContainerSharedComponents;
                }
            }
            
            this.serializedObj = new SerializedObject(this.tempObject);

            this.RedrawComponents(world);
            
        }

        private System.Collections.Generic.List<VisualElement> cachedFieldsComponents = new System.Collections.Generic.List<VisualElement>();
        private System.Collections.Generic.List<VisualElement> cachedFieldsSharedComponents = new System.Collections.Generic.List<VisualElement>();
        private void RedrawComponents(World world) {
            
            this.DrawFields(this.componentContainerComponentsRoot, this.componentContainerComponents, this.cachedFieldsComponents, world, this.tempObject.data, this.serializedObj, nameof(TempObject.data), methodSetComponent);
            this.DrawFields(this.componentContainerSharedComponentsRoot, this.componentContainerSharedComponents, this.cachedFieldsSharedComponents, world, this.tempObject.dataShared, this.serializedObj, nameof(TempObject.dataShared), methodSetSharedComponent);
            
        }
        
        private void FetchDataFromEntity(World world) {

            this.version = this.entity.Version;
            
            if (this.tempObject == null) {
                var c = TempObject.CreateInstance<TempObject>();
                this.tempObject = c;
                this.serializedObj = new SerializedObject(this.tempObject);
            }

            this.FetchComponentsFromEntity(world);
            this.FetchSharedComponentsFromEntity(world);
        }
        
        private void FetchComponentsFromEntity(World world) {
            
            var archId = world.state->archetypes.entToArchetypeIdx[world.state->allocator, this.entity.id];
            var arch = world.state->archetypes.list[world.state->allocator, archId];
                    
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
            
        }

        private void FetchSharedComponentsFromEntity(World world) {
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
        }

        private void DrawFields(VisualElement root, VisualElement rootContainer, System.Collections.Generic.List<VisualElement> fields, World world, object[] arrData, SerializedObject serializedObject, string fieldName, System.Reflection.MethodInfo methodSet) {

            var dataArr = serializedObject.FindProperty(fieldName);
            var delta = dataArr.arraySize - fields.Count;
            if (delta > 0) {
                // add new items
                for (int i = 0; i < delta; ++i) {

                    var it = dataArr.GetArrayElementAtIndex(i);
                    var copy = it.Copy();
                    var label = EditorUtils.GetComponentName(arrData[i].GetType());
                    if (copy.hasVisibleChildren == true) {
                        var propertyField = new PropertyField(copy, label) {
                            name = $"PropertyField:{it.propertyPath}",
                        };
                        propertyField.userData = i;
                        propertyField.AddToClassList("field");
                        propertyField.Bind(serializedObject);
                        System.Action rebuild = () => {
                            var allChilds = propertyField.Query<PropertyField>().ToList();
                            foreach (var child in allChilds) {
                                child.userData = propertyField.userData;
                                child.RegisterValueChangeCallback((evt) => {

                                    if (evt.target == null) return;
                                    var userData = ((PropertyField)evt.target).userData;
                                    if (userData == null) return;

                                    var idx = (int)userData;
                                    arrData[idx] = dataArr.GetArrayElementAtIndex(idx).managedReferenceValue;
                                    var value = arrData[idx];
                                    var gMethod = methodSet.MakeGenericMethod(value.GetType());
                                    gMethod.Invoke(world.state->components, new object[] { this.entity, value });
                                    this.version = this.entity.Version;

                                });
                            }
                        };
                        propertyField.RegisterCallback<UnityEngine.UIElements.GeometryChangedEvent>((evt) => { rebuild.Invoke(); });
                        propertyField.RegisterCallback<AttachToPanelEvent>(new EventCallback<AttachToPanelEvent>((evt) => { rebuild.Invoke(); }));
                        rootContainer.Add(propertyField);
                        fields.Add(propertyField);
                    } else {
                        
                        var labelField = new Foldout();
                        labelField.text = label;
                        labelField.AddToClassList("field");
                        fields.Add(labelField);
                    }

                }
            } else if (delta < 0) {
                // remove items
                delta = -delta;
                for (int i = 0; i < delta; ++i) {
                    rootContainer.RemoveAt(0);
                    fields.RemoveAtSwapBack(0);
                }
            }

            if (dataArr.arraySize == 0) {
                root.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            } else {
                root.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            }
            
            // redraw all items
            for (int i = 0; i < dataArr.arraySize; ++i) {

                var field = fields[i];
                var it = dataArr.GetArrayElementAtIndex(i);
                if (field is PropertyField propertyField) {
                    var copy = it.Copy();
                    propertyField.name = $"PropertyField:{it.propertyPath}";
                    propertyField.bindingPath = it.propertyPath;
                    propertyField.userData = i;
                    propertyField.BindProperty(copy);
                    propertyField.Bind(serializedObject);
                    var allChilds = field.Query<PropertyField>().ToList();
                    foreach (var child in allChilds) {
                        child.userData = propertyField.userData;
                    }
                }

            }

        }

    }

}