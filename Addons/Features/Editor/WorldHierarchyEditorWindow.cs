using System.Linq;
using ME.BECS.Transforms;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    public class WorldHierarchyFilterEditorWindow : EditorWindow {

        private StyleSheet styleSheet;
        private StyleSheet styleSheetTooltip;
        private WorldHierarchyEditorWindow src;

        public static void ShowWindow(WorldHierarchyEditorWindow src, Rect rect, Vector2 size) {
            var win = WorldHierarchyFilterEditorWindow.CreateInstance<WorldHierarchyFilterEditorWindow>();
            win.titleContent = new GUIContent("ECS Hierarchy", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-hierarchy.png"));
            win.LoadStyle();
            win.src = src;
            win.ShowAsDropDown(rect, size);
        }

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Hierarchy.uss");
            }
            if (this.styleSheetTooltip == null) {
                this.styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }
        }

        private void CreateGUI() {

            var root = new ScrollView(ScrollViewMode.Vertical);
            root.styleSheets.Add(this.styleSheet);
            root.styleSheets.Add(this.styleSheetTooltip);
            root.AddToClassList("filter-root");
            {
                var header = new Label("Groups");
                header.AddToClassList("filter-header");
                root.Add(header);
                foreach (var group in this.src.uniqueGroups) {
                    var groupValue = group;
                    var toggle = new Toggle(groupValue.value);
                    toggle.AddToClassList("filter-item");
                    var checkmark = new Label("\u2713");
                    checkmark.AddToClassList("filter-checkmark");
                    var c = toggle.Q(className: Toggle.labelUssClassName).parent;
                    c.Add(checkmark);
                    var colorLabel = new Label();
                    if (EditorUtils.TryGetGroupColor(group.type, out var color) == true) {
                        colorLabel.style.backgroundColor = new StyleColor(color);
                        if (EditorUIUtils.IsDarkColor(color) == true) {
                            colorLabel.AddToClassList("dark-color");
                        } else {
                            colorLabel.AddToClassList("light-color");
                        }
                    }
                    colorLabel.AddToClassList("tag-label");
                    c.Add(colorLabel);
                    colorLabel.SendToBack();
                    checkmark.SendToBack();
                    toggle.value = this.src.ignoredGroups.Contains(groupValue) == false;
                    if (toggle.value == true) toggle.AddToClassList("checked");
                    toggle.RegisterValueChangedCallback((evt) => {
                        if (evt.newValue == true) {
                            this.src.ignoredGroups.Remove(groupValue);
                        } else {
                            this.src.ignoredGroups.Add(groupValue);
                        }

                        if (evt.newValue == true) {
                            toggle.AddToClassList("checked");
                        } else {
                            toggle.RemoveFromClassList("checked");
                        }
                        this.src.settingsChanged = true;
                    });
                    root.Add(toggle);
                }
            }
            this.rootVisualElement.Add(root);

        }

    }
    
    public unsafe class WorldHierarchyEditorWindow : EditorWindow {

        private const float TAG_WIDTH = 8f;

        private StyleSheet styleSheet;
        private StyleSheet styleSheetTooltip;
        private World selectedWorld;
        private readonly System.Collections.Generic.List<World> aliveWorlds = new System.Collections.Generic.List<World>();
        private DropdownField toolbarItemsContainer;
        private VisualElement hierarchyRoot;
        private string search;
        private readonly System.Collections.Generic.HashSet<System.Type> searchTypes = new System.Collections.Generic.HashSet<System.Type>();
        private readonly System.Collections.Generic.HashSet<string> searchNames = new System.Collections.Generic.HashSet<string>();
        private VisualElement tagsBackground;

        [UnityEditor.MenuItem("ME.BECS/Hierarchy...")]
        public static void ShowWindow() {
            var win = WorldHierarchyEditorWindow.CreateInstance<WorldHierarchyEditorWindow>();
            win.titleContent = new GUIContent("ECS Hierarchy", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-hierarchy.png"));
            win.LoadStyle();
            win.LoadSettings();
            win.wantsMouseMove = true;
            win.Show();
        }

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Hierarchy.uss");
            }
            if (this.styleSheetTooltip == null) {
                this.styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }
        }

        private void LoadSettings() {
            
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            
            this.search = EditorPrefs.GetString("ME.BECS.WorldHierarchyEditorWindow.search", string.Empty);

            var groups = EditorUtils.GetComponentGroups(false);
            var count = EditorPrefs.GetInt("ME.BECS.WorldHierarchyEditorWindow.ignoreGroups.Count", 0);
            for (int i = 0; i < count; ++i) {
                var typeStr = EditorPrefs.GetString($"ME.BECS.WorldHierarchyEditorWindow.ignoreGroups[{i}]", string.Empty);
                var type = System.Type.GetType(typeStr);
                if (type != null) {
                    var group = groups.FirstOrDefault(x => x.type == type);
                    if (group.type != null) this.ignoredGroups.Add(group);
                }
            }

        }

        internal void SaveSettings() {
            
            EditorPrefs.SetString("ME.BECS.WorldHierarchyEditorWindow.search", this.search);
            EditorPrefs.SetInt("ME.BECS.WorldHierarchyEditorWindow.ignoreGroups.Count", this.ignoredGroups.Count);
            var i = 0;
            foreach (var item in this.ignoredGroups) {
                EditorPrefs.SetString($"ME.BECS.WorldHierarchyEditorWindow.ignoreGroups[{i}]", item.type.AssemblyQualifiedName);
                ++i;
            }
            
        }

        private readonly System.Collections.Generic.List<Renderer> currentObjects = new System.Collections.Generic.List<Renderer>();
        private bool alignSceneViewToObject = false;
        private void OnSceneGUI(SceneView obj) {
            
            if (this.selected.Count > 0) {
                var initializer = Object.FindObjectsByType<BaseWorldInitializer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(x => x.world.id == this.selectedWorld.id);
                var vm = initializer?.GetModule<ViewsModule>();
                this.currentObjects.Clear();
                Transform tr = null;
                foreach (var selection in this.selected) {
                    if (selection.IsAlive() == false) continue;
                    var view = vm?.GetViewByEntity(selection);
                    if (view is Component comp) {
                        //this.currentObjects.Add(comp.gameObject);
                        this.currentObjects.AddRange(comp.gameObject.GetComponentsInChildren<Renderer>());
                        tr = comp.transform;
                    }
                    if (selection.Has<WorldMatrixComponent>() == true) {
                        var t = selection.GetAspect<TransformAspect>();
                        Handles.PositionHandle((Vector3)t.position, (Quaternion)t.rotation);
                    }
                }
                Handles.DrawOutline(this.currentObjects.ToArray(), Color.green);
                if (tr != null && this.alignSceneViewToObject == true) {
                    obj.AlignViewToObject(tr);
                }
                this.alignSceneViewToObject = false;
            }

        }

        private void UpdateWorlds() {
            
            this.aliveWorlds.Clear();
            
            var worlds = Worlds.GetWorlds();
            for (int i = 0; i < worlds.Length; ++i) {
                
                var world = worlds.Get(i).world;
                if (world.isCreated == false) continue;
                
                this.aliveWorlds.Add(world);
                
            }
            
        }

        private bool pause;
        private void Update() {

            if (this.pause == true) return;
            
            this.UpdateWorlds();
            this.DrawToolbar();

            if (this.selectedWorld.isCreated == true) {
                this.DrawEntities(this.hierarchyRoot);
            }
            
        }

        private bool Move(int delta, out Element newElement, bool addToSelection = false) {
            newElement = null;
            var first = (delta > 0 ? this.selected.OrderBy(x => x.id).LastOrDefault() : this.selected.OrderBy(x => x.id).FirstOrDefault());
            if (first.IsAlive() == true) {
                var elem = this.elements.FindIndex(x => x.value == first);
                if (elem >= 0 && elem + delta < this.elements.Count && elem + delta >= 0) {
                    if (addToSelection == false) this.selected.Clear();
                    newElement = this.elements[elem + delta];
                    this.selected.Add(newElement.value);
                    return true;
                }
            }
            return false;
        }

        private bool UnfoldSelection(out System.Collections.Generic.List<Element> newElement) {
            newElement = null;
            var count = 0;
            for (int i = this.elements.Count - 1; i >= 0; --i) {
                var elem = this.elements[i];
                if (this.selected.Contains(elem.value) == true) { 
                    if (newElement == null) newElement = new System.Collections.Generic.List<Element>();
                    if (elem.IsFoldout == false) {
                        elem.IsFoldout = true;
                        newElement.Add(elem);
                        ++count;
                    } else if (i < this.elements.Count - 1) {
                        // Move to child if it has one
                        var curLevel = elem.level;
                        var next = this.elements[i + 1];
                        if (next.level == curLevel + 1) {
                            this.selected.Remove(elem.value);
                            this.selected.Add(next.value);
                            newElement.Add(next);
                            ++count;
                        }
                    }
                }
            }

            return count > 0;
        }

        private bool FoldSelection(out System.Collections.Generic.List<Element> newElement) {
            newElement = null;
            var count = 0;
            for (int i = 0; i < this.elements.Count; ++i) {
                var elem = this.elements[i];
                if (this.selected.Contains(elem.value) == true) { 
                    if (newElement == null) newElement = new System.Collections.Generic.List<Element>();
                    if (elem.IsFoldout == true) {
                        elem.IsFoldout = false;
                        newElement.Add(elem);
                        ++count;
                    } else if (i > 0) {
                        // Move to parent if it has one
                        var curLevel = elem.level;
                        var next = this.elements[i - 1];
                        if (next.level == curLevel - 1) {
                            this.selected.Remove(elem.value);
                            this.selected.Add(next.value);
                            newElement.Add(next);
                            ++count;
                        }
                    }
                }
            }

            return count > 0;
        }

        public void DrawToolbar() {

            if (this.toolbarItemsContainer != null) {

                var selectedId = this.selectedWorld.id;
                var list = new System.Collections.Generic.List<string>();
                var index = -1;
                var k = 0;
                foreach (var world in this.aliveWorlds) {
                    list.Add(world.Name);
                    if (world.id == selectedId) index = k;
                    ++k;
                }

                this.toolbarItemsContainer.choices = list;
                //this.toolbarItemsContainer.index = index;

            }

        }
        
        private void CreateGUI() {

            Selection.selectionChanged += this.OnSelectionChanged;

            this.LoadSettings();
            this.LoadStyle();
            this.rootVisualElement.Clear();
            this.rootVisualElement.styleSheets.Add(this.styleSheet);
            this.rootVisualElement.styleSheets.Add(this.styleSheetTooltip);
            
            var root = new VisualElement();
            {
                var toolbarContainer = new VisualElement();
                root.Add(toolbarContainer);
                toolbarContainer.AddToClassList("toolbar-container");
                this.MakeToolbar(toolbarContainer);
            }
            
            if (this.selectedWorld.isCreated == true) {
                var scrollView = new ScrollView(ScrollViewMode.Vertical);
                scrollView.RegisterCallback<KeyDownEvent>((evt) => {
                    if (evt.keyCode == KeyCode.DownArrow) {
                        if (this.Move(1, out var newSelection, evt.shiftKey) == true) {
                            evt.StopImmediatePropagation();
                            scrollView.ScrollTo(newSelection.container);
                            this.DrawInspector();
                        }
                    } else if (evt.keyCode == KeyCode.UpArrow) {
                        if (this.Move(-1, out var newSelection, evt.shiftKey) == true) {
                            evt.StopImmediatePropagation();
                            scrollView.ScrollTo(newSelection.container);
                            this.DrawInspector();
                        }
                    } else if (evt.keyCode == KeyCode.RightArrow) {
                        if (this.UnfoldSelection(out var list) == true) {
                            evt.StopImmediatePropagation();
                            scrollView.ScrollTo(list.First().container);
                            scrollView.ScrollTo(list.Last().container);
                            this.DrawInspector();
                        }
                    } else if (evt.keyCode == KeyCode.LeftArrow) {
                        if (this.FoldSelection(out var list) == true) {
                            evt.StopImmediatePropagation();
                            scrollView.ScrollTo(list.First().container);
                            scrollView.ScrollTo(list.Last().container);
                            this.DrawInspector();
                        }
                    }
                }, TrickleDown.TrickleDown);
                {
                    var decorationsRoot = new VisualElement();
                    decorationsRoot.AddToClassList("h-root-decorations");
                    scrollView.Add(decorationsRoot);

                    {
                        var col = new VisualElement();
                        col.AddToClassList("content-column");
                        decorationsRoot.Add(col);
                    }
                    {
                        var col = new VisualElement();
                        col.AddToClassList("tags-column");
                        this.tagsBackground = col;
                        decorationsRoot.Add(col);
                    }
                    {
                        var col = new VisualElement();
                        col.AddToClassList("version-column");
                        decorationsRoot.Add(col);
                    }
                }
                this.hierarchyRoot = new VisualElement();
                this.hierarchyRoot.AddToClassList("h-root");
                scrollView.Add(this.hierarchyRoot);
                root.Add(scrollView);
            }
            this.rootVisualElement.Add(root);
            
        }

        private void OnSelectionChanged() {
            if (Selection.activeObject != this.currentInspector) {
                this.selected.Clear();
            }
        }

        private void SelectWorld(World world) {
            
            this.selectedWorld = world;
            this.CreateGUI();
            
        }

        private void MakeToolbar(VisualElement container) {
            
            container.Clear();

            var toolbar = new UnityEditor.UIElements.Toolbar();
            container.Add(toolbar);
            toolbar.AddToClassList("toolbar");
            /*{
                var p = new Toggle("Pause");
                p.value = this.pause;
                p.RegisterValueChangedCallback(evt => this.pause = evt.newValue);
                toolbar.Add(p);
            }*/
            {
                var selectedId = this.selectedWorld.id;
                var list = new System.Collections.Generic.List<string>();
                var index = -1;
                var k = 0;
                foreach (var world in this.aliveWorlds) {
                    list.Add(world.Name);
                    if (world.id == selectedId) index = k;
                    ++k;
                }

                var selection = new DropdownField(list, index, formatListItemCallback: (val) => {
                    if (val == null) return null;
                    return val.Replace("#", string.Empty);
                }, formatSelectedValueCallback: (val) => {
                    if (val == null) return "<b>W</b>";
                    if (list.Count == 0) return null;
                    var idx = list.IndexOf(val);
                    if (idx >= this.aliveWorlds.Count) return null;
                    return $"#{this.aliveWorlds[idx].id}";
                });
                selection.RegisterValueChangedCallback((evt) => {
                    var idx = selection.choices.IndexOf(evt.newValue);
                    if (idx >= 0) {
                        this.SelectWorld(this.aliveWorlds[idx]);
                    }
                });
                this.toolbarItemsContainer = selection;
                toolbar.Add(selection);
            }
            {
                var toolbarContainer = new VisualElement();
                toolbar.Add(toolbarContainer);
                toolbarContainer.AddToClassList("search-container");
                var field = new ToolbarSearchField();
                field.RegisterValueChangedCallback((evt) => {
                    this.search = evt.newValue;
                    this.searchTypes.Clear();
                    this.searchNames.Clear();
                    if (string.IsNullOrEmpty(this.search) == false) {
                        var val = this.search.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                        var groups = EditorUtils.GetComponentGroups();
                        for (int i = 0; i < val.Length; ++i) {
                            var v = val[i];
                            foreach (var kv in StaticTypesLoadedManaged.loadedTypes) {
                                var type = kv.Value;
                                if (type != null && type.Name.Contains(v, System.StringComparison.InvariantCultureIgnoreCase) == true) {
                                    // Add all components
                                    this.searchTypes.Add(type);
                                }
                            }
                            foreach (var group in groups) {
                                var addAll = false;
                                if (group.type != null && group.type.Name.Contains(v, System.StringComparison.InvariantCultureIgnoreCase) == true) {
                                    // Add all components
                                    addAll = true;
                                    this.searchTypes.Add(group.type);
                                }

                                foreach (var comp in group.components) {
                                    if (addAll == true || (comp.type != null && comp.type.Name.Contains(v, System.StringComparison.InvariantCultureIgnoreCase) == true)) {
                                        this.searchTypes.Add(comp.type);
                                    }
                                }
                            }
                            this.searchNames.Add(v);
                        }
                    }
                    this.settingsChanged = true;
                });
                field.value = this.search;
                toolbarContainer.Add(field);
            }
            {
                Button filtersButton = null;
                filtersButton = new Button(() => {
                    WorldHierarchyFilterEditorWindow.ShowWindow(this, GUIUtility.GUIToScreenRect(filtersButton.worldBound), new Vector2(200f, 300f));
                });
                var img = new Image();
                img.image = EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-settings.png");
                filtersButton.Add(img);
                filtersButton.AddToClassList("filter-button");
                toolbar.Add(filtersButton);
            }
            
        }

        public class Element {

            public Ent value;
            public VisualElement container;
            public Label versionLabel;
            public Foldout foldout;
            public VisualElement tagsContainer;
            private WorldHierarchyEditorWindow window;
            public uint childrenCount;

            public bool IsFoldout {
                set {
                    if (this.window.foldout.TryAdd(this.value, value) == false) {
                        this.window.foldout[this.value] = value;
                    }
                    this.foldout.value = value;
                }
                get {
                    if (this.window.foldout.TryGetValue(this.value, out var val) == true) {
                        return val;
                    }
                    return false;
                }
            }
            public int level;

            public Element(WorldHierarchyEditorWindow window) {
                this.window = window;
            }

            public void Redraw(bool force) {
                var versionChanged = force;
                if (this.window.entToVersions.TryGetValue(this.value, out var version) == false) {
                    this.window.entToVersions.Add(this.value, this.value.Version);
                    versionChanged = true;
                } else if (version != this.value.Version) {
                    this.window.entToVersions[this.value] = this.value.Version;
                    versionChanged = true;
                }

                if (versionChanged == true) {
                    this.foldout.text = this.value.ToString(withWorld: false, withVersion: false, withGen: false).ToString();
                    this.versionLabel.text = this.value.Version.ToString();
                    this.foldout.style.paddingLeft = new StyleLength(0f + 20f * this.level);
                    this.RedrawTags(force);
                }

                if (this.window.selected.Contains(this.value) == true) {
                    this.container.AddToClassList("selected");
                } else {
                    this.container.RemoveFromClassList("selected");
                }
            }

            private void RedrawTags(bool force) {
                var changed = true;
                var state = this.value.World.state;
                if (force == false) {
                    var componentsCount = state.ptr->archetypes.list[state, state.ptr->archetypes.entToArchetypeIdx[state, this.value.id]].componentsCount;
                    if (this.window.entToComponentsCount.TryGetValue(this.value, out var count) == true) {
                        if (count == componentsCount) {
                            changed = false;
                        }
                    } else {
                        this.window.entToComponentsCount.Add(this.value, componentsCount);
                    }
                }

                if (changed == true) {
                    this.window.entToTags.Remove(this.value);
                }
                if (this.window.entToTags.TryGetValue(this.value, out var tags) == false) {
                    var groupsTags = new System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem>();
                    var groups = EditorUtils.GetComponentGroups();
                    foreach (var group in groups) {
                        if (group.type == null) continue;
                        if (groupsTags.Contains(group) == true) continue;
                        foreach (var comp in group.components) {
                            if (StaticTypesLoadedManaged.typeToId.TryGetValue(comp.type, out var typeId) == true) {
                                if (Components.HasUnknownType(state, typeId, this.value.id, this.value.gen, true) == true) {
                                    if (this.window.ignoredGroups.Contains(group) == false) groupsTags.Add(group);
                                    this.window.uniqueGroups.Add(group);
                                }
                            }
                        }
                    }
                    tags = groupsTags.ToList();
                    this.window.entToTags.Add(this.value, tags);
                    this.window.tagsCount = Mathf.Max(this.window.tagsCount, tags.Count);
                    if (this.tagsContainer.childCount == tags.Count) {
                        for (int i = 0; i < tags.Count; ++i) {
                            var tag = tags[i];
                            var tagLabel = this.tagsContainer.ElementAt(i);
                            tagLabel.tooltip = tag.value;
                            if (EditorUtils.TryGetGroupColor(tag.type, out var color) == true) {
                                tagLabel.style.backgroundColor = new StyleColor(color);
                                if (EditorUIUtils.IsDarkColor(color) == true) {
                                    tagLabel.AddToClassList("dark-color");
                                } else {
                                    tagLabel.AddToClassList("light-color");
                                }
                            }

                            tagLabel.AddToClassList("tag-label");
                        }
                    } else {
                        this.tagsContainer.Clear();
                        foreach (var tag in tags) {
                            var tagLabel = new Label();
                            tagLabel.tooltip = tag.value;
                            if (EditorUtils.TryGetGroupColor(tag.type, out var color) == true) {
                                tagLabel.style.backgroundColor = new StyleColor(color);
                                if (EditorUIUtils.IsDarkColor(color) == true) {
                                    tagLabel.AddToClassList("dark-color");
                                } else {
                                    tagLabel.AddToClassList("light-color");
                                }
                            }

                            tagLabel.AddToClassList("tag-label");
                            this.tagsContainer.Add(tagLabel);
                        }
                    }
                }
                this.tagsContainer.style.width = new StyleLength(this.window.GetTagsWidth());
                this.tagsContainer.style.maxWidth = new StyleLength(this.window.GetTagsWidth());
            }

            public void Hide() {
                this.IsFoldout = false;
                if (this.container.parent != null) this.container.parent.Remove(this.container);
            }

            public void Reset() {
                
            }

        }

        private float GetTagsWidth() {
            return TAG_WIDTH * this.tagsCount + 4f + 2f + 8f + 6f;
        }

        private readonly System.Collections.Generic.List<Element> elements = new System.Collections.Generic.List<Element>();
        private readonly System.Collections.Generic.HashSet<Ent> selected = new System.Collections.Generic.HashSet<Ent>();
        internal readonly System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem> uniqueGroups = new System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem>();
        internal readonly System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem> ignoredGroups = new System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem>();
        private readonly System.Collections.Generic.Dictionary<Ent, bool> foldout = new System.Collections.Generic.Dictionary<Ent, bool>();
        private readonly System.Collections.Generic.Dictionary<Ent, Element> entToElement = new System.Collections.Generic.Dictionary<Ent, Element>();
        private readonly System.Collections.Generic.Dictionary<Ent, System.Collections.Generic.List<EditorUtils.ComponentGroupItem>> entToTags = new System.Collections.Generic.Dictionary<Ent, System.Collections.Generic.List<EditorUtils.ComponentGroupItem>>();
        private readonly System.Collections.Generic.Dictionary<Ent, uint> entToComponentsCount = new System.Collections.Generic.Dictionary<Ent, uint>();
        private readonly System.Collections.Generic.Dictionary<Ent, uint> entToVersions = new System.Collections.Generic.Dictionary<Ent, uint>();
        internal bool settingsChanged;

        [CustomEditor(typeof(Entity))]
        public class EntityEditor : UnityEditor.Editor {

            protected override void OnHeaderGUI() {
                
                
                
            }

            public override VisualElement CreateInspectorGUI() {
                
                var root = new VisualElement();
                var prop = this.serializedObject.FindProperty(nameof(Entity.values));
                for (int i = 0; i < prop.arraySize; ++i) {
                    var p = prop.GetArrayElementAtIndex(i);
                    var entProp = new UnityEditor.UIElements.PropertyField(p);
                    entProp.BindProperty(p);
                    root.Add(entProp);
                }

                return root;

            }

        }
        
        public class Entity : ScriptableObject {

            public Ent[] values;

        }

        private Entity currentInspector;
        
        private void DrawInspector() {
            {
                if (this.currentInspector != null) Object.DestroyImmediate(this.currentInspector);
                {
                    var obj = ScriptableObject.CreateInstance<Entity>();
                    this.currentInspector = obj;
                }
                this.currentInspector.values = this.selected.ToArray();
                Selection.activeObject = this.currentInspector;
            }
        }
        
        private Element MakeElement(Ent ent) {
            var element = new Element(this) {
                value = ent,
            };
            var container = new VisualElement();
            var foldout = new Foldout();
            var foldoutText = foldout.Q<Toggle>().Q(className: "unity-base-field__input");
            var tagsContainer = new VisualElement();
            tagsContainer.AddToClassList("tags");
            tagsContainer.pickingMode = PickingMode.Ignore;
            element.tagsContainer = tagsContainer;
            foldout.text = "-";
            foldout.value = false;
            container.Add(foldout);
            foldoutText.Add(tagsContainer);
            container.focusable = true;
            container.pickingMode = PickingMode.Position;
            container.AddToClassList("h-element");
            var context = new ContextualMenuManipulator((menu) => {
                this.selected.Add(element.value);
                menu.menu.AppendAction("Delete", (evt) => {
                    foreach (var selection in this.selected) {
                        if (selection.IsAlive() == true) selection.DestroyHierarchy();
                    }
                    this.selected.Clear();
                });
                {
                    var status = DropdownMenuAction.Status.Disabled;
                    var initializer = Object.FindObjectsByType<BaseWorldInitializer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(x => x.world.id == this.selectedWorld.id);
                    var viewsModule = initializer?.GetModule<ViewsModule>();
                    if (viewsModule != null) {
                        foreach (var selection in this.selected) {
                            var view = viewsModule.GetViewByEntity(selection);
                            if (view is Component) {
                                status = DropdownMenuAction.Status.Normal;
                                break;
                            }
                        }
                    }

                    menu.menu.AppendAction("Select View", (evt) => {
                        var vm = initializer?.GetModule<ViewsModule>();
                        if (vm != null) {
                            var selections = new System.Collections.Generic.List<GameObject>();
                            foreach (var selection in this.selected) {
                                var view = vm.GetViewByEntity(selection);
                                if (view is Component comp) {
                                    selections.Add(comp.gameObject);
                                }
                            }

                            Selection.objects = selections.ToArray();
                        }
                    }, status);
                }
            });
            context.target = foldout;
            var inp = foldout.Q(className: Foldout.inputUssClassName);
            inp.pickingMode = PickingMode.Ignore;
            foldout.Q(className: Foldout.toggleUssClassName).pickingMode = PickingMode.Ignore;
            foldout.Q(className: Foldout.textUssClassName).pickingMode = PickingMode.Ignore;
            foldout.Q(className: Foldout.checkmarkUssClassName).pickingMode = PickingMode.Position;
            foldout.RegisterCallback<MouseOverEvent>(evt => {
                container.AddToClassList("hover");
            });
            foldout.RegisterCallback<MouseOutEvent>(evt => {
                container.RemoveFromClassList("hover");
            });
            foldout.RegisterValueChangedCallback((evt) => {
                element.IsFoldout = evt.newValue;
            });
            container.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.clickCount == 2) {
                    this.alignSceneViewToObject = true;
                }
                if (evt.ctrlKey == true || evt.commandKey == true) {
                    if (this.selected.Add(element.value) == false) {
                        this.selected.Remove(element.value);
                    }
                    this.DrawInspector();
                    return;
                } else if (evt.shiftKey == true) {
                    if (this.selected.Count > 0) {
                        var first = this.selected.First();
                        var elem = this.entToElement[first];
                        var rect = new Bounds(elem.container.worldBound.center, elem.container.worldBound.size * 0.5f);
                        rect.Encapsulate(((VisualElement)evt.target).worldBound.center);
                        var found = false;
                        for (int i = 0; i < this.elements.Count; ++i) {
                            var e = this.elements[i];
                            var wb = e.container.worldBound;
                            var b = new Bounds(wb.center, wb.size);
                            if (rect.Intersects(b) == true) {
                                this.selected.Add(e.value);
                                found = true;
                            } else if (found == true) {
                                break;
                            }
                        }
                        this.DrawInspector();
                        return;
                    }
                }

                this.selected.Clear();
                this.selected.Add(element.value);

                this.DrawInspector();
            });
            var versionLabel = new Label();
            versionLabel.AddToClassList("version");
            versionLabel.pickingMode = PickingMode.Ignore;
            foldoutText.Add(versionLabel);
            element.foldout = foldout;
            element.container = container;
            element.versionLabel = versionLabel;
            return element;
        }
        
        private System.Collections.Generic.List<Ent> cache = new System.Collections.Generic.List<Ent>();
        private int tagsCount;

        private void DrawEntities(VisualElement root) {
            
            this.cache.Clear();
            for (uint i = 0u; i < this.selectedWorld.state.ptr->archetypes.list.Count; ++i) {
                var arch = this.selectedWorld.state.ptr->archetypes.list[this.selectedWorld.state, i];
                for (uint j = 0u; j < arch.entitiesList.Count; ++j) {
                    var entId = arch.entitiesList[this.selectedWorld.state.ptr->allocator, j];
                    var ent = new Ent(entId, this.selectedWorld);
                    if (ent.Read<ParentComponent>().value == default) {
                        this.cache.Add(ent);
                    }
                }
            }

            var k = 0;
            this.DrawEntities(ref k, 0, root, this.cache);
            
            for (int i = k; i < this.elements.Count; ++i) {
                this.elements[i].Hide();
            }

            if (this.settingsChanged == true) {
                this.SaveSettings();
            }
            this.settingsChanged = false;

        }
        
        private void DrawEntities(ref int k, int level, VisualElement root, System.Collections.Generic.List<Ent> list) {

            if (this.searchTypes.Count == 0 && this.searchNames.Count == 0 && string.IsNullOrEmpty(this.search) == false) return;

            var rawHierarchy = false;
            foreach (var ent in list) {
                if (this.entToTags.TryGetValue(ent, out var tags) == true) {
                    var ignore = false;
                    foreach (var tag in tags) {
                        if (this.ignoredGroups.Contains(tag) == false) continue;
                        ignore = true;
                        break;
                    }
                    if (ignore == true) continue;
                }

                var found = true;
                if (this.searchTypes.Count > 0 || this.searchNames.Count > 0) {
                    var containsType = false;
                    var state = ent.World.state;
                    foreach (var type in this.searchTypes) {
                        if (StaticTypesGroups.groups.TryGetValue(type, out var groupId) == true) {
                            if (ent.GetVersion(groupId) > 0u) {
                                containsType = true;
                                break;
                            }
                        } else if (StaticTypesLoadedManaged.typeToId.TryGetValue(type, out var id) == true) {
                            if (Components.HasUnknownType(state, id, ent.id, ent.gen, true) == true) {
                                containsType = true;
                                break;
                            }
                        }
                    }

                    var containsName = false;
                    var name = ent.EditorName;
                    {
                        var idName = $"#{ent.id}";
                        var n = name.IsEmpty == false ? name.ToString() : null;
                        foreach (var searchName in this.searchNames) {
                            if ((n == null || n.Contains(searchName, System.StringComparison.InvariantCultureIgnoreCase) == false) && idName.Contains(searchName) == false) continue;
                            containsName = true;
                            break;
                        }
                    }
                    
                    found = (containsType == true || containsName == true);
                    rawHierarchy = true;
                }

                if (found == true) {

                    Element element;
                    if (k >= this.elements.Count) {
                        this.elements.Add(this.MakeElement(ent));
                    }

                    {
                        element = this.elements[k];
                        if (element.value != ent) {
                            element.Reset();
                            this.entToElement.Remove(element.value);
                            element.value = ent;
                        }

                        element.Redraw(true);
                        this.elements[k] = element;
                    }
                    if (element.container.parent == null || element.container.parent != root) root.Add(element.container);
                    if (this.entToElement.TryAdd(element.value, element) == false) {
                        this.entToElement[element.value] = element;
                    }

                    element.level = level;
                    ++k;
                    {
                        var children = ent.Read<ChildrenComponent>().list;
                        if (children.Count > 0u) {
                            if (rawHierarchy == false) {
                                if (element.container.ClassListContains("raw-hierarchy") == true) element.container.RemoveFromClassList("raw-hierarchy");
                            } else {
                                if (element.container.ClassListContains("raw-hierarchy") == false) element.container.AddToClassList("raw-hierarchy");
                            }
                            if (children.Count != element.childrenCount) {
                                element.container.AddToClassList("has-children");
                            }
                            if (element.IsFoldout == true || rawHierarchy == true) {
                                var childList = new System.Collections.Generic.List<Ent>();
                                foreach (var child in children) childList.Add(child);
                                this.DrawEntities(ref k, level + 1, root, childList);
                            }
                        } else {
                            if (children.Count != element.childrenCount) element.container.RemoveFromClassList("has-children");
                        }

                        element.childrenCount = children.Count;
                    }

                } else if (rawHierarchy == true) {
                    var children = ent.Read<ChildrenComponent>().list;
                    if (children.Count > 0u) {
                        var childList = new System.Collections.Generic.List<Ent>();
                        foreach (var child in children) childList.Add(child);
                        this.DrawEntities(ref k, level + 1, root, childList);
                    }
                }

            }
            
            this.tagsBackground.style.width = new StyleLength(this.GetTagsWidth());
            
        }

    }

}