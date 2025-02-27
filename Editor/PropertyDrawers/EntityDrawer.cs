using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static ME.BECS.Cuts;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(Ent))]
    public unsafe class EntityDrawer : PropertyDrawer {

        public static Dictionary<ulong, TempObject> tempObjects = new Dictionary<ulong, TempObject>();
        
        private static StyleSheet styleSheetBase;
        private static StyleSheet styleSheet;

        private TempObject tempObject {
            get {
                if (tempObjects.TryGetValue(this.entity.ToULong(), out var temp) == true) {
                    if (temp == null) tempObjects.Remove(this.entity.ToULong());
                    return temp;
                }

                return null;
            }
        }

        private void CreateTempObject() {
            var c = TempObject.CreateInstance<TempObject>();
            tempObjects.Add(this.entity.ToULong(), c);
        }
        
        protected void LoadStyle() {
            if (EntityDrawer.styleSheetBase == null) {
                EntityDrawer.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Entity.uss");
            }
            if (EntityDrawer.styleSheet == null) {
                EntityDrawer.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfig.uss");
            }
        }

        ~EntityDrawer() {

            this.Dispose();
            this.propertyPath = null;
            this.property = null;
            this.propertySerializedObject = null;

            if (tempObjects.TryGetValue(this.entity.ToULong(), out var temp) == true) {
                if (temp != null) EditorApplication.delayCall += () => Object.DestroyImmediate(temp);
                tempObjects.Remove(this.entity.ToULong());
            }

        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            var ent = (Ent)PropertyEditorUtils.GetTargetObjectOfProperty(property);
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("entity-mini");
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(EntityDrawer.styleSheetBase);
            rootVisualElement.styleSheets.Add(EntityDrawer.styleSheet);
            this.rootVisualElement = rootVisualElement;

            this.entity = ent;
            this.propertyPath = property.propertyPath;
            this.propertySerializedObject = property.serializedObject;
            this.property = property;
            this.DrawEntity(rootVisualElement, this.entity.World, property.displayName);

            EditorApplication.update -= this.OnUpdate;
            EditorApplication.update += this.OnUpdate;

            return rootVisualElement;

        }

        public void Dispose() {
            
            EditorApplication.update -= this.OnUpdate;
            
        }

        private bool prevState;
        private SerializedProperty property;
        private string propertyPath;
        private SerializedObject propertySerializedObject;
        public void OnUpdate() {

            if (PropertyEditorUtils.IsValid(this.property) == false) {
                EditorApplication.update -= this.OnUpdate;
                return;
            }

            var obj = PropertyEditorUtils.GetTargetObjectOfProperty(this.property ?? this.propertySerializedObject.FindProperty(this.propertyPath));
            if (obj == null || obj is not Ent ent) return;
            
            this.entity = ent;
            var world = this.entity.World;
            if (this.entity.IsAlive() == true && this.version != this.entity.Version) {
                this.FetchDataFromEntity(world);
            }
            var newState = this.GetState(world);
            if (this.prevState != newState ||
                (newState == true && this.archId != this.GetArchId())) {
                this.DrawEntity(this.rootVisualElement, world, this.property.displayName);
            } else if (newState == true) {
                this.UpdateData();
            }
            
        }

        private bool GetState(World world) {

            return world.isCreated == true &&
                   this.entity.IsAlive() == true;
            
        }

        private uint version;
        protected Ent entity;
        private uint archId;
        private VisualElement rootVisualElement;
        private SerializedObject serializedObj;

        private Label versionLabel;

        private uint GetArchId() {
            var world = this.entity.World;
            return world.state.ptr->archetypes.entToArchetypeIdx[world.state.ptr->allocator, this.entity.id];
        }

        private void UpdateData() {

            this.versionLabel.text = this.entity.Version.ToString();
            if (this.journalRoot != null) {
                this.journalRoot.Clear();
                JournalEditorWindow.UpdateEntityJournal(this.journalRoot, ref this.journalItems, this.entity);
            }

        }

        private void DrawEntity(VisualElement root, World world, string displayName = null) {
            
            var container = root;
            container.userData = this.entity;
            container.Clear();

            var idString = "-";
            var genString = "-";
            var worldString = "-";
            var editorNameString = string.Empty;
            var versionString = string.Empty;
            var drawComponents = true;

            if (this.GetState(world) == true) {
                
                idString = this.entity.id.ToString();
                genString = this.entity.gen.ToString();
                worldString = this.entity.worldId.ToString();
                versionString = this.entity.Version.ToString();
                editorNameString = this.entity.EditorName.ToString();

            } else {

                drawComponents = false;

            }

            if (this.property.serializedObject.targetObjects.Length > 1) {

                idString = "-";
                genString = "-";
                worldString = "-";
                versionString = "-";
                editorNameString = string.Empty;
                drawComponents = false;

            }

            var rootComponents = new VisualElement();
            var toggleContainer = new VisualElement();
            var header = new Toggle();
            header.RegisterValueChangedCallback((evt) => {
                toggleContainer.style.display = new StyleEnum<DisplayStyle>(evt.newValue == true ? DisplayStyle.Flex : DisplayStyle.None);
                header.RemoveFromClassList("toggle-checked");
                if (evt.newValue == true) {
                    header.AddToClassList("toggle-checked");
                    if (drawComponents == true && header.value == true) this.DrawComponents(rootComponents, world);
                } else {
                    rootComponents.Clear();
                }
                this.SetFoldoutState(evt.newValue);
            });
            header.value = EditorPrefs.GetBool($"ME.BECS.Foldouts.Entity.{this.propertyPath}", false);
            toggleContainer.style.display = new StyleEnum<DisplayStyle>(header.value == true ? DisplayStyle.Flex : DisplayStyle.None);
            header.RemoveFromClassList("toggle-checked");
            if (header.value == true) header.AddToClassList("toggle-checked");
            header.AddToClassList("entity-header");
            container.Add(header);
            container.Add(toggleContainer);
            
            if (drawComponents == true && header.value == true) this.DrawComponents(rootComponents, world);
            toggleContainer.Add(rootComponents);

            if (displayName != null) {
                var idContainer = new VisualElement();
                idContainer.AddToClassList("entity-name-container");
                header.Add(idContainer);
                var entityIdLabel = new Label(displayName);
                entityIdLabel.AddToClassList("entity-name-label");
                entityIdLabel.AddToClassList("label-header");
                idContainer.Add(entityIdLabel);
            }

            {
                var nameContainer = new VisualElement();
                nameContainer.style.display = new StyleEnum<DisplayStyle>(string.IsNullOrEmpty(editorNameString) == true ? DisplayStyle.None : DisplayStyle.Flex);
                nameContainer.AddToClassList("entity-name-container");
                header.Add(nameContainer);
                var entityNameLabel = new Label("Name");
                entityNameLabel.AddToClassList("entity-name-label");
                entityNameLabel.AddToClassList("label-header");
                nameContainer.Add(entityNameLabel);
                var entityName = new Label(editorNameString);
                entityName.AddToClassList("entity-name");
                entityName.AddToClassList("label-value");
                nameContainer.Add(entityName);
            }

            {
                var idContainer = new VisualElement();
                idContainer.AddToClassList("entity-id-container");
                header.Add(idContainer);
                var entityIdLabel = new Label("ID");
                entityIdLabel.AddToClassList("entity-id-label");
                entityIdLabel.AddToClassList("label-header");
                idContainer.Add(entityIdLabel);
                var entityId = new Label(idString);
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
                var entityGen = new Label(genString);
                entityGen.AddToClassList("entity-gen");
                entityGen.AddToClassList("label-value");
                genContainer.Add(entityGen);
            }

            {
                var genContainer = new VisualElement();
                genContainer.AddToClassList("entity-world-container");
                header.Add(genContainer);
                var entityGenLabel = new Label("World");
                entityGenLabel.AddToClassList("entity-world-label");
                entityGenLabel.AddToClassList("label-header");
                genContainer.Add(entityGenLabel);
                var entityGen = new Label(worldString);
                entityGen.AddToClassList("entity-world");
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
                    var entityVersion = new Label(versionString);
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
                    var entityArch = new Label(drawComponents == true ? $"#{archId}" : "-");
                    entityArch.AddToClassList("entity-arch");
                    entityArch.AddToClassList("label-value");
                    archContainer.Add(entityArch);
                }

            }

        }
        
        private static readonly System.Reflection.MethodInfo methodSetComponent = typeof(Components).GetMethod(nameof(Components.SetDirect));
        private static readonly System.Reflection.MethodInfo methodSetSharedComponent = typeof(Components).GetMethod(nameof(Components.SetSharedDirect));
        private static readonly System.Reflection.MethodInfo methodReadComponent = typeof(Components).GetMethod(nameof(Components.ReadDirect));
        private static readonly System.Reflection.MethodInfo methodReadSharedComponent = typeof(Components).GetMethod(nameof(Components.ReadSharedDirect));
        private VisualElement componentContainerComponents;
        private VisualElement componentContainerSharedComponents;
        private VisualElement componentContainerComponentsRoot;
        private VisualElement componentContainerSharedComponentsRoot;
        private void DrawComponents(VisualElement root, World world) {

            root.Clear();
            
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
                    this.componentContainerComponents = componentContainer;
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
                    this.componentContainerSharedComponents = componentContainer;
                }
            }
            {
                var components = new Foldout();
                components.text = "Journal";
                components.value = false;
                components.AddToClassList("entity-journal");
                componentsContainer.Add(components);

                components.styleSheets.Add(EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Journal.uss"));

                var componentsList = new VisualElement();
                componentsList.AddToClassList("entity-components-list");
                components.Add(componentsList);
                this.journalRoot = JournalEditorWindow.DrawEntityJournal(componentsList, ref this.journalItems, this.entity);
            }
            
            this.serializedObj = new SerializedObject(this.tempObject);

            this.RedrawComponents(world);
            
        }

        private VisualElement journalRoot;
        private JournalEditorWindow.VisualElementData[] journalItems;

        private readonly System.Collections.Generic.List<VisualElement> cachedFieldsComponents = new System.Collections.Generic.List<VisualElement>();
        private readonly System.Collections.Generic.List<VisualElement> cachedFieldsSharedComponents = new System.Collections.Generic.List<VisualElement>();
        private void RedrawComponents(World world) {
            
            DrawFields(this.entity, this.componentContainerComponentsRoot, this.componentContainerComponents, this.cachedFieldsComponents, world, this.tempObject.data, this.tempObject.dataHas, this.serializedObj, nameof(TempObject.data), methodSetComponent, methodReadComponent);
            DrawFields(this.entity, this.componentContainerSharedComponentsRoot, this.componentContainerSharedComponents, this.cachedFieldsSharedComponents, world, this.tempObject.dataShared, this.tempObject.dataSharedHas, this.serializedObj, nameof(TempObject.dataShared), methodSetSharedComponent, methodReadSharedComponent);
            
        }
        
        private void FetchDataFromEntity(World world) {

            this.version = this.entity.Version;
            
            if (this.tempObject == null) {
                this.CreateTempObject();
                this.serializedObj = new SerializedObject(this.tempObject);
            }

            this.FetchComponentsFromEntity(world);
            this.FetchSharedComponentsFromEntity(world);
            
        }
        
        private void FetchComponentsFromEntity(World world) {
            
            var archId = world.state.ptr->archetypes.entToArchetypeIdx[world.state.ptr->allocator, this.entity.id];
            var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, archId];
                    
            var methodRead = typeof(Components).GetMethod(nameof(Components.ReadDirect));
            var methodHas = typeof(Components).GetMethod(nameof(Components.HasDirectEnabled));
            var cnt = 0;
            {
                var e = arch.components.GetEnumerator(world);
                while (e.MoveNext() == true) {
                    var cId = e.Current;
                    if (StaticTypesLoadedManaged.loadedTypes.ContainsKey(cId) == true) ++cnt;
                }
            }

            if (this.tempObject.data != null &&
                this.tempObject.data.Length == cnt) {
                
                {
                    var i = 0;
                    var e = arch.components.GetEnumerator(world);
                    while (e.MoveNext() == true) {
                        var cId = e.Current;
                        if (StaticTypesLoadedManaged.loadedTypes.TryGetValue(cId, out var type) == true) {
                            {
                                var gMethod = methodRead.MakeGenericMethod(type);
                                var gMethodHas = methodHas.MakeGenericMethod(type);
                                var val = gMethod.Invoke(null, new object[] { this.entity });
                                this.tempObject.data[i] = val;
                                this.tempObject.dataHas[i] = (bool)gMethodHas.Invoke(null, new object[] { this.entity });
                            }
                            ++i;
                        }
                    }
                }
            } else {
                
                this.tempObject.data = new object[cnt];
                this.tempObject.dataHas = new bool[cnt];
                {
                    var i = 0;
                    var e = arch.components.GetEnumerator(world);
                    while (e.MoveNext() == true) {
                        var cId = e.Current;
                        if (StaticTypesLoadedManaged.loadedTypes.TryGetValue(cId, out var type) == true) {
                            {
                                var gMethod = methodRead.MakeGenericMethod(type);
                                var gMethodHas = methodHas.MakeGenericMethod(type);
                                var val = gMethod.Invoke(null, new object[] { this.entity });
                                this.tempObject.data[i] = val;
                                this.tempObject.dataHas[i] = (bool)gMethodHas.Invoke(null, new object[] { this.entity });
                            }
                            ++i;
                        }
                    }
                }
            }

        }

        private void FetchSharedComponentsFromEntity(World world) {
            var methodRead = typeof(Components).GetMethod(nameof(Components.ReadSharedDirect));
            var methodHas = typeof(Components).GetMethod(nameof(Components.HasSharedDirect));
            var count = 0;
            foreach (var kv in StaticTypesLoadedManaged.loadedSharedTypes) {
                var type = kv.Value;
                {
                    var gHas = methodHas.MakeGenericMethod(type);
                    var has = (bool)gHas.Invoke(world.state.ptr->components, new object[] { this.entity });
                    if (has == true) {
                        ++count;
                    }

                }
            }

            if (this.tempObject.dataShared != null &&
                this.tempObject.dataShared.Length == count) {
                var i = 0;
                foreach (var kv in StaticTypesLoadedManaged.loadedSharedTypes) {
                    var type = kv.Value;
                    {
                        var gHas = methodHas.MakeGenericMethod(type);
                        var has = (bool)gHas.Invoke(world.state.ptr->components, new object[] { this.entity });
                        if (has == true) {
                            var gMethod = methodRead.MakeGenericMethod(type);
                            var val = gMethod.Invoke(world.state.ptr->components, new object[] { this.entity });
                            this.tempObject.dataShared[i++] = val;
                        }
                    }
                }
            } else {
                var list = new System.Collections.Generic.List<object>(count);
                foreach (var kv in StaticTypesLoadedManaged.loadedSharedTypes) {
                    var type = kv.Value;
                    {
                        var gHas = methodHas.MakeGenericMethod(type);
                        var has = (bool)gHas.Invoke(world.state.ptr->components, new object[] { this.entity });
                        if (has == true) {
                            var gMethod = methodRead.MakeGenericMethod(type);
                            var val = gMethod.Invoke(world.state.ptr->components, new object[] { this.entity });
                            list.Add(val);
                        }
                    }
                }
                this.tempObject.dataShared = list.ToArray();
                this.tempObject.dataSharedHas = new bool[list.Count];
                for (int i = 0; i < this.tempObject.dataSharedHas.Length; ++i) {
                    this.tempObject.dataSharedHas[i] = true;
                }
            }

        }

        public static void DrawFields(Ent entity, VisualElement root, VisualElement rootContainer, System.Collections.Generic.List<VisualElement> fields, World world, object[] arrData, bool[] arrDataHas, SerializedObject serializedObject, string fieldName, System.Reflection.MethodInfo methodSet, System.Reflection.MethodInfo methodRead) {

            var dataArr = serializedObject.FindProperty(fieldName);
            var delta = dataArr.arraySize - fields.Count;
            var isDirty = false;
            if (delta > 0) {
                isDirty = true;
                // add new items
                for (int i = 0; i < delta; ++i) {

                    var it = dataArr.GetArrayElementAtIndex(i);
                    var copy = it.Copy();
                    var type = arrData[i].GetType();
                    var label = EditorUtils.GetComponentName(type);
                    if (copy.hasVisibleChildren == true) {
                        var propertyField = new PropertyField(copy, label) {
                            name = $"PropertyField:{it.propertyPath}",
                        };
                        propertyField.userData = i;
                        propertyField.AddToClassList("field");
                        propertyField.BindProperty(copy);
                        propertyField.Bind(serializedObject);
                        if (EditorUtils.TryGetComponentGroupColor(type, out var color) == true) {
                            color.a = 0.1f;
                            propertyField.style.backgroundColor = new StyleColor(color);
                        }
                        System.Action rebuild = () => {
                            var allChilds = propertyField.Query<PropertyField>().ToList();
                            foreach (var child in allChilds) {
                                child.userData = propertyField.userData;
                                child.RegisterValueChangeCallback((evt) => {

                                    /*if (evt.target == null) return;
                                    var userData = ((PropertyField)evt.target).userData;
                                    if (userData == null) return;
                                    var idx = (int)userData;
                                    if (idx >= dataArr.arraySize) return;

                                    var newValue = dataArr.GetArrayElementAtIndex(idx)?.managedReferenceValue;
                                    {
                                        arrData[idx] = newValue;
                                        var value = arrData[idx];
                                        if (value == null) {
                                            Logger.Editor.Error($"Value is null at index {idx} in entity {entity}");
                                            return;
                                        }

                                        object prevData;
                                        {
                                            var gMethod = methodRead.MakeGenericMethod(value.GetType());
                                            prevData = gMethod.Invoke(world.state.ptr->components, new object[] { entity });
                                        }
                                        var hasChanged = StructsAreEqual(prevData, newValue) == false;
                                        if (hasChanged == true) {
                                            var gMethod = methodSet.MakeGenericMethod(value.GetType());
                                            gMethod.Invoke(world.state.ptr->components, new object[] { entity, value });
                                        }
                                    }*/

                                });
                            }
                        };
                        if (entity.IsAlive() == true) {
                            propertyField.RegisterCallback<UnityEngine.UIElements.GeometryChangedEvent>((evt) => { rebuild.Invoke(); });
                            propertyField.RegisterCallback<AttachToPanelEvent>(new EventCallback<AttachToPanelEvent>((evt) => { rebuild.Invoke(); }));
                        }
                        rootContainer.Add(propertyField);
                        fields.Add(propertyField);
                    } else {
                        
                        var labelField = new Label();
                        if (EditorUtils.TryGetComponentGroupColor(type, out var color) == true) {
                            color.a = 0.1f;
                            labelField.style.backgroundColor = new StyleColor(color);
                        }
                        labelField.text = label;
                        labelField.AddToClassList("field");
                        labelField.AddToClassList("no-children");
                        rootContainer.Add(labelField);
                        fields.Add(labelField);
                    }

                }
            } else if (delta < 0) {
                isDirty = true;
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

            if (isDirty == true) {
                // redraw all items
                for (int i = 0; i < dataArr.arraySize; ++i) {

                    var field = fields[i];
                    var it = dataArr.GetArrayElementAtIndex(i);
                    if (arrDataHas[i] == true) {
                        field.RemoveFromClassList("disabled");
                        field.AddToClassList("enabled");
                    } else {
                        field.RemoveFromClassList("enabled");
                        field.AddToClassList("disabled");
                    }
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

        public static bool StructCopy<T>(T data, T data2) where T : unmanaged {
            var size = TSize<T>.size;
            var addr1 = _address(ref data);
            var addr2 = _address(ref data2);
            return _memcmp(addr1, addr2, size) == 0;
        }

        public static bool StructsAreEqual(object s1, object s2) {
            var method = typeof(EntityDrawer).GetMethod("StructCopy");
            var gMethod = method.MakeGenericMethod(s1.GetType());
            var res = (bool)gMethod.Invoke(null, new object[] { s1, s2 });
            return res;
        }

        public void SetFoldoutState(bool value) {
            EditorPrefs.SetBool($"ME.BECS.Foldouts.Entity.{this.propertyPath}", value);
        }

    }

}