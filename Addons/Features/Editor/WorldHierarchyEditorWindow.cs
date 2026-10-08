using System.Linq;
using ME.BECS.Transforms;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using scg = System.Collections.Generic;

namespace ME.BECS.Editor {

    /// <summary>
    /// Provides the Unity Editor window for world hierarchy filter editor.
    /// </summary>
    public class WorldHierarchyFilterEditorWindow : EditorWindow {

        private StyleSheet styleSheet;
        private StyleSheet styleSheetTooltip;
        private WorldHierarchyEditorWindow src;
        private GradientAnimated logoLine;

        /// <summary>
        /// Opens or focuses the associated editor window.
        /// </summary>
        public static void ShowWindow(WorldHierarchyEditorWindow src, Rect rect, Vector2 size) {
            var win = WorldHierarchyFilterEditorWindow.CreateInstance<WorldHierarchyFilterEditorWindow>();
            EditorUIUtils.ApplyWindowIcon(win, "ECS Hierarchy", "ME.BECS.Resources/Icons/icon-hierarchy.png");
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
            EditorUIUtils.ApplyDefaultStyles(root);
            root.styleSheets.Add(this.styleSheet);
            root.styleSheets.Add(this.styleSheetTooltip);
            
            this.logoLine = EditorUIUtils.AddLogoLine(root);
            
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
    
    /// <summary>
    /// Provides the Unity Editor window for world hierarchy editor.
    /// </summary>
    public unsafe class WorldHierarchyEditorWindow : EditorWindow {


        private StyleSheet styleSheet;
        private StyleSheet styleSheetTooltip;
        private World selectedWorld;
        private readonly System.Collections.Generic.List<World> aliveWorlds = new System.Collections.Generic.List<World>();
        private string search;
        private readonly System.Collections.Generic.HashSet<System.Type> searchTypes = new System.Collections.Generic.HashSet<System.Type>();
        private readonly System.Collections.Generic.HashSet<string> searchNames = new System.Collections.Generic.HashSet<string>();
        private bool alignSceneViewToObject = false;
        private bool synchronizingSelection;
        [SerializeField] private bool suppressRuntimeWorlds;
        private double nextHierarchyUpdate, nextWorldUpdate;
        private ViewsModule viewsModule;
        private StyleSheet currentTheme;
        private StyleSheet compactHierarchyStyle;
        private StyleSheet dashboardStyle;
        private readonly System.Collections.Generic.HashSet<Ent> expandedEntities = new System.Collections.Generic.HashSet<Ent>();
        private readonly scg::Dictionary<Ent, Ent> parents = new scg::Dictionary<Ent, Ent>();
        private readonly scg::Dictionary<Ent, Ent> observedParents = new scg::Dictionary<Ent, Ent>();
        private readonly scg::Dictionary<Ent, Ent> snapshotParents = new scg::Dictionary<Ent, Ent>();
        private readonly scg::Dictionary<Ent, byte> traversal = new scg::Dictionary<Ent, byte>();
        private readonly scg::List<Ent> pathBuffer = new scg::List<Ent>();
        private readonly System.Collections.Generic.HashSet<int> parentIds = new System.Collections.Generic.HashSet<int>();
        private readonly scg::List<int> staleParentIds = new scg::List<int>();
        private readonly scg::List<int> selectionIds = new scg::List<int>();
        private readonly scg::List<Renderer> selectedViewRenderers = new scg::List<Renderer>();
        private readonly scg::List<Renderer> viewRendererBuffer = new scg::List<Renderer>();
        private readonly scg::List<Ent> selectionBuffer = new scg::List<Ent>();

        private readonly System.Collections.Generic.HashSet<Ent> selected = new System.Collections.Generic.HashSet<Ent>();
        internal readonly System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem> uniqueGroups = new System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem>();
        internal readonly System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem> ignoredGroups = new System.Collections.Generic.HashSet<EditorUtils.ComponentGroupItem>();
        internal bool settingsChanged;

        private System.Collections.Generic.List<Ent> cache = new System.Collections.Generic.List<Ent>();

        private readonly System.Collections.Generic.HashSet<Ent> current = new System.Collections.Generic.HashSet<Ent>();
        private readonly System.Collections.Generic.HashSet<Ent> allEntities = new System.Collections.Generic.HashSet<Ent>();
        //private bool rawHierarchy;

        private scg::List<TreeViewItemData<Ent>> roots = new scg::List<TreeViewItemData<Ent>>();
        private scg::Dictionary<int, scg::List<TreeViewItemData<Ent>>> dics = new scg::Dictionary<int, scg::List<TreeViewItemData<Ent>>>();
        private Label currentEntitiesCount;
        private Label selectedEntitiesCount;

        /// <summary>
        /// Opens or focuses the associated editor window.
        /// </summary>
        [MenuItem("ME.BECS/\u2637 Hierarchy...", priority = 10000)]
        public static void ShowWindow() {
            var win = WorldHierarchyEditorWindow.CreateInstance<WorldHierarchyEditorWindow>();
            EditorUIUtils.ApplyWindowIcon(win, "ECS Hierarchy", "ME.BECS.Resources/Icons/icon-hierarchy.png");
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
            
            SceneView.duringSceneGui -= this.OnSceneGUI;
            SceneView.duringSceneGui += this.OnSceneGUI;
            
            EditorApplication.playModeStateChanged -= this.ClearWindowData;
            EditorApplication.playModeStateChanged += this.ClearWindowData;

            this.search = EditorPrefs.GetString("ME.BECS.WorldHierarchyEditorWindow.search", string.Empty);

            EditorUtility.DisplayProgressBar("Hierarchy", "Initialization", 0f);
            try {
                var groups = EditorUtils.GetComponentGroups(false);
                var count = EditorPrefs.GetInt("ME.BECS.WorldHierarchyEditorWindow.ignoreGroups.Count", 0);
                for (int i = 0; i < count; ++i) {
                    EditorUtility.DisplayProgressBar("Hierarchy", "Initialization", i / (float)count);
                    var typeStr = EditorPrefs.GetString($"ME.BECS.WorldHierarchyEditorWindow.ignoreGroups[{i}]", string.Empty);
                    var type = System.Type.GetType(typeStr);
                    if (type != null) {
                        var group = groups.FirstOrDefault(x => x.type == type);
                        if (group.type != null) this.ignoredGroups.Add(group);
                    }
                }
            } catch (System.Exception ex) {
                Debug.LogException(ex);
            } finally {
                EditorUtility.ClearProgressBar();
            }

        }
        
        private void ClearWindowData(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode) { this.suppressRuntimeWorlds = false; this.nextWorldUpdate = 0; return; }
            if (state is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.EnteredEditMode) {
                this.suppressRuntimeWorlds = true;
                this.viewsModule = null; this.alignSceneViewToObject = false;
                this.allEntities.Clear(); this.cache.Clear(); this.selectionBuffer.Clear(); this.selectionIds.Clear();
                this.traversal.Clear(); this.pathBuffer.Clear(); this.parentIds.Clear(); this.staleParentIds.Clear();
                if (this.currentInspector != null) Object.DestroyImmediate(this.currentInspector);
                this.currentInspector = null;
                this.expandedEntities.Clear();
                this.aliveWorlds.Clear(); this.openedWorlds.Clear(); this.tabsSignature = null;
                this.selectedWorld = default;
                this.parents.Clear(); this.observedParents.Clear(); this.snapshotParents.Clear(); this.roots.Clear(); this.dics.Clear();
                this.selected.Clear();
                this.current.Clear();
                this.CreateGUI();
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

        private void OnSceneGUI(SceneView scene) {
            var frame = this.alignSceneViewToObject;
            this.alignSceneViewToObject = false;
            this.selectedViewRenderers.Clear();
            foreach (var ent in this.selected) {
                if (!ent.IsAlive() || this.viewsModule?.GetViewByEntity(ent) is not Component view || view == null) continue;
                view.GetComponentsInChildren<Renderer>(false, this.viewRendererBuffer);
                foreach (var renderer in this.viewRendererBuffer) {
                    if (renderer != null && renderer.enabled) this.selectedViewRenderers.Add(renderer);
                }
            }
            if (UnityEngine.Event.current.type == EventType.Repaint && this.selectedViewRenderers.Count > 0) {
                Handles.DrawOutline(this.selectedViewRenderers.ToArray(), Handles.selectedColor);
            }
            var framed = false;
            if (frame && this.selectedViewRenderers.Count > 0) {
                var bounds = this.selectedViewRenderers[0].bounds;
                for (var i = 1; i < this.selectedViewRenderers.Count; ++i) bounds.Encapsulate(this.selectedViewRenderers[i].bounds);
                scene.Frame(bounds);
                framed = true;
            }
            foreach (var ent in this.selected) {
                if (!ent.IsAlive() || !ent.Has<WorldMatrixComponent>()) continue;
                var matrix = (Matrix4x4)ent.Read<WorldMatrixComponent>().value;
                var position = (Vector3)matrix.GetColumn(3);
                var size = HandleUtility.GetHandleSize(position) * 0.5f;
                using (new Handles.DrawingScope()) {
                    Handles.color = Handles.xAxisColor;
                    Handles.DrawLine(position, position + matrix.MultiplyVector(Vector3.right).normalized * size);
                    Handles.color = Handles.yAxisColor;
                    Handles.DrawLine(position, position + matrix.MultiplyVector(Vector3.up).normalized * size);
                    Handles.color = Handles.zAxisColor;
                    Handles.DrawLine(position, position + matrix.MultiplyVector(Vector3.forward).normalized * size);
                    Handles.color = Color.white;
                    Handles.SphereHandleCap(0, position, Quaternion.identity, size * 0.1f, EventType.Repaint);
                    Handles.Label(position, "#" + ent.id);
                }
                if (frame && !framed) { scene.Frame(new Bounds(position, Vector3.one * 2f)); framed = true; }
            }
        }

        private void UpdateWorlds() {
            
            this.aliveWorlds.Clear();
            if (this.suppressRuntimeWorlds) return;
            
            var worlds = Worlds.GetWorlds();
            for (int i = 0; i < worlds.Length; ++i) {
                
                var world = worlds.Get(i).world;
                if (world.isCreated == false) continue;
                
                this.aliveWorlds.Add(world);
                
            }
            
        }

        private void Update() {
            if (this.suppressRuntimeWorlds) return;
            var now = EditorApplication.timeSinceStartup;
            if (now >= this.nextWorldUpdate) {
                this.nextWorldUpdate = now + 0.5;
                this.UpdateWorlds(); this.DrawToolbar();
                if (this.selectedWorld.isCreated && !this.aliveWorlds.Any(world => world.Equals(this.selectedWorld))) this.SelectWorld(default);
                this.viewsModule = this.selectedWorld.isCreated ? WorldInitializers.GetByWorldName(this.selectedWorld.Name)?.GetModule<ViewsModule>() : null;
            }
            if (now < this.nextHierarchyUpdate) return;
            this.nextHierarchyUpdate = now + 0.1;
            if (this.selectedWorld.isCreated) this.DrawEntities();
            this.UpdateFooter();
            if (this.selected.Count > 0) SceneView.RepaintAll();
        }

        /// <summary>
        /// Draws toolbar.
        /// </summary>
        public void DrawToolbar() {
            var removed = this.openedWorlds.RemoveAll(world => !this.aliveWorlds.Any(alive => alive.Equals(world)));
            if (removed > 0) this.tabsSignature = null;
            this.DrawWorldTabs();

        }

        private TreeView treeView;
        private VisualElement worldTabs;
        private readonly scg::List<World> openedWorlds = new scg::List<World>();
        private string tabsSignature;
        private void OnEnable() {
            EditorApplication.delayCall -= this.RebuildAfterReload;
            EditorApplication.delayCall += this.RebuildAfterReload;
        }
        private void RebuildAfterReload() {
            if (this != null) this.CreateGUI();
        }

        private void CreateGUI() {

            Selection.selectionChanged -= this.OnSelectionChanged;
            Selection.selectionChanged += this.OnSelectionChanged;

            this.LoadSettings();
            this.LoadStyle();
            this.treeView = null;
            this.tabsSignature = null;
            this.rootVisualElement.Clear();
            EditorUIUtils.ApplyDefaultStyles(this.rootVisualElement);
            this.rootVisualElement.AddToClassList("compact-ecs-hierarchy");
            this.rootVisualElement.AddToClassList("world-dashboard");
            this.rootVisualElement.EnableInClassList("dark", EditorGUIUtility.isProSkin);
            this.rootVisualElement.EnableInClassList("light", !EditorGUIUtility.isProSkin);
            this.rootVisualElement.style.flexDirection = FlexDirection.Column;
            this.ApplyTheme();
            Themes.Changed -= this.ApplyTheme; Themes.Changed += this.ApplyTheme;
            

            var root = this.rootVisualElement;
            EditorUIUtils.AddLogoLine(root);
            this.worldTabs = new VisualElement();
            this.worldTabs.AddToClassList("dashboard-tabs");
            this.worldTabs.style.flexDirection = FlexDirection.Row;
            this.worldTabs.style.flexShrink = 0;
            this.worldTabs.style.minHeight = 30;
            root.Add(this.worldTabs);
            this.DrawWorldTabs();
            {
                var toolbarContainer = new VisualElement();
                root.Add(toolbarContainer);
                toolbarContainer.AddToClassList("toolbar-container");
                toolbarContainer.style.flexShrink = 0;
                this.MakeToolbar(toolbarContainer);
                toolbarContainer.style.display = this.selectedWorld.isCreated ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            if (this.selectedWorld.isCreated == true) {
                var treeView = new TreeView {
                    makeItem = () => this.MakeElement(),
                    bindItem = (element, i) => {
                        var item = this.treeView.GetItemDataForIndex<Ent>(i);
                        var txt = element.Q<Label>(className: "caption");
                        element.userData = item;
                        element.EnableInClassList("h-selected", this.selected.Contains(item));
                        var alive = item.IsAlive();
                        var inactive = alive == true && item.IsActive() == false;
                        element.EnableInClassList("h-inactive", inactive);
                        txt.text = alive == true ? item.ToString(withWorld: false, withVersion: false).ToString() : "Destroyed entity";
                        element.tooltip = inactive == true ? "Entity is disabled" : string.Empty;
                        var ver = element.Q<Label>(className: "version");
                        ver.text = item.IsAlive() ? item.Version.ToString() : "—";
                    },
                };
                treeView.fixedItemHeight = 26;
                treeView.RegisterCallback<KeyDownEvent>(evt => {
                    if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace) return;
                    this.DeleteSelected(); evt.StopPropagation();
                });
                treeView.itemExpandedChanged += args => {
                    if (this.synchronizingSelection) return;
                    var ent = this.treeView.GetItemDataForId<Ent>(args.id);
                    if (!ent.IsAlive()) return;
                    if (args.isExpanded) this.expandedEntities.Add(ent);
                    else this.expandedEntities.Remove(ent);
                };
                treeView.selectionType = SelectionType.Multiple;
                treeView.selectionChanged += values => {
                    if (this.synchronizingSelection) return;
                    this.selected.Clear();
                    foreach (var value in values) if (value is Ent ent && ent.IsAlive()) this.selected.Add(ent);
                    this.RefreshSelectionHighlight();
                    this.PublishSelection();
                };
                treeView.AddToClassList("h-root");
                this.treeView = treeView;
                this.treeView.SetRootItems(this.roots);
                
                treeView.style.flexGrow = 1;
                treeView.style.flexBasis = 0;
                treeView.style.minHeight = 0;
                root.Add(treeView);
                
                {
                    var toolbarContainer = new VisualElement();
                    root.Add(toolbarContainer);
                    toolbarContainer.AddToClassList("footer-container");
                    this.MakeFooter(toolbarContainer);
                }
                
            } else {
                var lbl = new Label("Open a world with +");
                lbl.AddToClassList("empty-label");
                lbl.AddToClassList("becs-empty-state");
                lbl.style.flexGrow = 1;
                root.Add(lbl);
            }
            
        }

        private void OnSelectionChanged() {
            if (this.synchronizingSelection || this.suppressRuntimeWorlds) return;
            this.selectionBuffer.Clear();
            foreach (var obj in Selection.objects) {
                if (obj is Entity entityObject) {
                    if (entityObject.values != null) foreach (var ent in entityObject.values) if (ent.IsAlive()) this.selectionBuffer.Add(ent);
                    continue;
                }
                if (obj is WorldEntityEditorWindow.TempObject entityReference) {
                    if (entityReference.entity.IsAlive()) this.selectionBuffer.Add(entityReference.entity);
                    continue;
                }
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                var view = go != null ? go.GetComponentInParent<ME.BECS.Views.EntityView>(true) : null;
                if (view == null) continue;
                var entity = view.viewData.logicEnt.GetEntity();
                if (!entity.IsAlive()) continue;
                var module = WorldInitializers.GetByWorldName(entity.World.Name)?.GetModule<ViewsModule>();
                // Pooled views can retain old handles until their next enable.
                if (module?.GetViewByEntity(entity) is Component activeView && activeView == view) this.selectionBuffer.Add(entity);
            }
            if (this.selected.SetEquals(this.selectionBuffer)) return;
            this.synchronizingSelection = true;
            try {
                if (this.selectionBuffer.Count > 0 && !this.selectedWorld.Equals(this.selectionBuffer[0].World)) this.SelectWorld(this.selectionBuffer[0].World);
                this.selected.Clear();
                foreach (var ent in this.selectionBuffer) if (ent.World.Equals(this.selectedWorld)) this.selected.Add(ent);
                if (this.selectedWorld.isCreated) this.DrawEntities();
                this.RestoreSelection(true);
            } finally { this.synchronizingSelection = false; }
        }

        private void RestoreSelection(bool reveal) {
            if (this.treeView == null) return;
            this.selectionIds.Clear();
            foreach (var ent in this.selected) {
                if (!ent.IsAlive() || !this.current.Contains(ent)) continue;
                if (reveal) {
                    var parent = ent;
                    var remaining = this.parents.Count;
                    while (remaining-- > 0 && this.parents.TryGetValue(parent, out parent) && !parent.IsEmpty()) { this.expandedEntities.Add(parent); this.treeView.ExpandItem((int)parent.id, false, true); }
                }
                this.selectionIds.Add((int)ent.id);
            }
            this.treeView.SetSelectionByIdWithoutNotify(this.selectionIds);
            this.RefreshSelectionHighlight();
            if (reveal && this.selectionIds.Count > 0) this.treeView.ScrollToItemById(this.selectionIds[0]);
        }

        private void PublishSelection() {
            this.synchronizingSelection = true;
            try {
                // Keep the Inspector on ECS data; view outlines are drawn independently.
                if (this.selected.Count > 0) this.DrawInspector();
                else Selection.objects = System.Array.Empty<Object>();
            } finally { this.synchronizingSelection = false; }
            SceneView.RepaintAll();
        }

        private void ApplyTheme() {
            EditorUIUtils.ApplyWindowIcon(this, "ECS Hierarchy", "ME.BECS.Resources/Icons/icon-hierarchy.png");
            this.rootVisualElement.AddToClassList("becs-editor-window");
            this.rootVisualElement.EnableInClassList("dark", EditorGUIUtility.isProSkin);
            this.rootVisualElement.EnableInClassList("light", !EditorGUIUtility.isProSkin);
            // EditorWindow's root owns Unity's default text/control styles too.
            // Remove only our sheets, otherwise text loses its inherited font.
            if (this.currentTheme != null) this.rootVisualElement.styleSheets.Remove(this.currentTheme);
            if (this.compactHierarchyStyle != null) this.rootVisualElement.styleSheets.Remove(this.compactHierarchyStyle);
            if (this.dashboardStyle != null) this.rootVisualElement.styleSheets.Remove(this.dashboardStyle);
            this.currentTheme = EditorUtils.LoadResource<StyleSheet>(Themes.CurrentTheme);
            this.compactHierarchyStyle = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/HierarchyCompact.uss");
            this.rootVisualElement.styleSheets.Add(this.currentTheme);
            this.dashboardStyle = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldDashboard.uss");
            this.rootVisualElement.styleSheets.Add(this.dashboardStyle);
            this.rootVisualElement.styleSheets.Add(this.compactHierarchyStyle);
            // Inherit UI Toolkit's editor font. EditorStyles may not be initialized during CreateGUI.
            this.rootVisualElement.style.unityFont = StyleKeyword.Null;
            this.rootVisualElement.style.fontSize = 12;
            this.rootVisualElement.style.color = StyleKeyword.Null;
        }

        private void OnDisable() {
            EditorApplication.delayCall -= this.RebuildAfterReload;
            Selection.selectionChanged -= this.OnSelectionChanged;
            SceneView.duringSceneGui -= this.OnSceneGUI;
            EditorApplication.playModeStateChanged -= this.ClearWindowData;
            Themes.Changed -= this.ApplyTheme;
            if (this.currentInspector != null) Object.DestroyImmediate(this.currentInspector);
            this.currentInspector = null;
        }

        private void SelectWorld(World world) {
            
            if (world.isCreated && !this.openedWorlds.Any(opened => opened.Equals(world))) this.openedWorlds.Add(world);
            this.selectedWorld = world;
            this.selected.Clear(); this.parents.Clear(); this.observedParents.Clear(); this.snapshotParents.Clear(); this.dics.Clear();
            this.viewsModule = world.isCreated ? WorldInitializers.GetByWorldName(world.Name)?.GetModule<ViewsModule>() : null;
            this.current.Clear();
            this.allEntities.Clear();
            this.roots.Clear();
            foreach (var kv in this.dics) {
                kv.Value.Clear();
            }
            this.CreateGUI();
            
        }

        private void MakeFooter(VisualElement container) {
            
            container.Clear();

            {
                var label = new Label();
                container.Add(label);
                this.currentEntitiesCount = label;
                label.AddToClassList("footer-entities-count");
            }
            {
                var label = new Label();
                container.Add(label);
                this.selectedEntitiesCount = label;
                label.AddToClassList("footer-selected-entities-count");
            }
            
        }

        private void UpdateFooter() {

            if (this.selectedEntitiesCount == null) return;
            
            this.selectedEntitiesCount.text = $"Selected: {this.selected.Count}";
            this.currentEntitiesCount.text = $"Entities: {this.allEntities.Count}";
            
        }

        private void DrawWorldTabs() {
            if (this.worldTabs == null) return;
            var signature = this.selectedWorld.id + ":" + string.Join("|", this.openedWorlds.Select(world => world.id + ":" + world.Name.ToString()));
            if (signature == this.tabsSignature) return;
            this.tabsSignature = signature;
            this.worldTabs.Clear();
            foreach (var opened in this.openedWorlds) {
                var world = opened;
                var select = new Button(() => {
                    this.UpdateWorlds();
                    if (this.aliveWorlds.Any(alive => alive.Equals(world))) this.SelectWorld(world);
                }) { text = world.Name + " · #" + world.id };
                select.AddToClassList("dashboard-tab");
                select.EnableInClassList("selected", world.Equals(this.selectedWorld));
                select.AddManipulator(new ContextualMenuManipulator(menu => {
                    menu.menu.AppendAction("Close Tab", action => {
                        this.openedWorlds.RemoveAll(item => item.Equals(world));
                        this.tabsSignature = null;
                        if (this.selectedWorld.Equals(world)) this.SelectWorld(this.openedWorlds.Count > 0 ? this.openedWorlds[this.openedWorlds.Count - 1] : default);
                        else this.DrawWorldTabs();
                    });
                }));
                this.worldTabs.Add(select);
            }
            var add = EditorUIUtils.CreateAddWorldButton(() => {
                this.UpdateWorlds();
                var menu = new GenericMenu();
                var count = 0;
                foreach (var alive in this.aliveWorlds) {
                    var world = alive;
                    if (this.openedWorlds.Any(item => item.Equals(world))) continue;
                    ++count;
                    menu.AddItem(new GUIContent((world.Name + " · #" + world.id).Replace('/', '∕')), false, () => {
                        this.UpdateWorlds();
                        if (this.aliveWorlds.Any(item => item.Equals(world))) this.SelectWorld(world);
                    });
                }
                if (count == 0) menu.AddDisabledItem(new GUIContent(this.aliveWorlds.Count == 0 ? "No running worlds" : "All running worlds are already open"));
                menu.ShowAsContext();
            }, "Open an existing world in a tab");
            add.AddToClassList("add-world-tab");
            add.style.flexShrink = 0;
            this.worldTabs.Add(add);
        }

        private void MakeToolbar(VisualElement container) {
            
            container.Clear();

            var toolbar = new UnityEditor.UIElements.Toolbar();
            container.Add(toolbar);
            toolbar.AddToClassList("toolbar");
            {
                var toolbarContainer = new VisualElement();
                toolbar.Add(toolbarContainer);
                toolbarContainer.AddToClassList("search-container");
                toolbarContainer.Add(new Label("Search"));
                var field = new TextField { tooltip = "Entity name, ID or component" };
                field.AddToClassList("hierarchy-search");
                var clear = new Button(() => field.value = string.Empty) { text = "×", tooltip = "Clear search" };
                field.RegisterValueChangedCallback((evt) => {
                    this.search = evt.newValue;
                    clear.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.None : DisplayStyle.Flex;
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
                clear.AddToClassList("hierarchy-search-clear");
                clear.style.display = string.IsNullOrEmpty(field.value) ? DisplayStyle.None : DisplayStyle.Flex;
                toolbarContainer.Add(clear);
            }
        }

        /// <summary>
        /// Provides Unity Editor controls for entity editor.
        /// </summary>
        [CustomEditor(typeof(Entity))]
        public class EntityEditor : UnityEditor.Editor {

            /// <summary>
            /// Provides the <c>OnHeaderGUI</c> callback; this implementation performs no work.
            /// </summary>
            protected override void OnHeaderGUI() {
                
                
                
            }

            /// <summary>
            /// Builds the UI Toolkit inspector for the inspected object.
            /// </summary>
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
        
        /// <summary>
        /// Defines entity state and operations for <c>WorldHierarchyEditorWindow</c>.
        /// </summary>
        public class Entity : ScriptableObject {

            /// <summary>
            /// Values used by <c>WorldHierarchyEditorWindow.Entity</c>.
            /// </summary>
            public Ent[] values;

        }

        private Entity currentInspector;
        
        private void DrawInspector() {
            {
                if (this.currentInspector == null) {
                    this.currentInspector = ScriptableObject.CreateInstance<Entity>();
                }
                // Keep navigation enabled; EntityDrawer locks only value fields.
                this.currentInspector.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
                this.currentInspector.values = this.selected.ToArray();
                EditorUtility.SetDirty(this.currentInspector);
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                Selection.activeObject = this.currentInspector;
            }
        }

        private void RefreshSelectionHighlight() {
            this.treeView?.Query<VisualElement>(className: "h-element").Build().ForEach(element => {
                element.EnableInClassList("h-selected", element.userData is Ent ent && this.selected.Contains(ent));
            });
        }

        private VisualElement MakeElement() {
            var container = new VisualElement();
            container.AddToClassList("h-element");
            var textLabel = new Label();
            textLabel.AddToClassList("caption");
            textLabel.pickingMode = PickingMode.Ignore;
            container.Add(textLabel);
            var versionLabel = new Label();
            versionLabel.AddToClassList("version");
            versionLabel.pickingMode = PickingMode.Ignore;
            container.Add(versionLabel);
            container.AddManipulator(new ContextualMenuManipulator(menu => {
                if (container.userData is not Ent ent || !ent.IsAlive()) return;
                if (!this.selected.Contains(ent)) {
                    this.selected.Clear(); this.selected.Add(ent); this.RestoreSelection(false);
                }
                menu.menu.AppendAction("Delete Entity", action => this.DeleteSelected(),
                    action => this.selected.Count > 0 && this.selected.All(EntityDrawer.CanEditComponents) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                menu.menu.AppendAction("Highlight View", action => this.PublishSelection(),
                    action => this.selected.Any(item => item.IsAlive() && this.viewsModule?.GetViewByEntity(item) is Component) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }));
            container.RegisterCallback<ClickEvent>(evt => {
                if (evt.clickCount == 2) {
                    this.alignSceneViewToObject = true;
                }
            });
            return container;
        }

        private void DeleteSelected() {
            // Recheck at execution time: replay mode may have changed after opening the menu.
            if (this.selected.Count == 0 || !this.selected.All(EntityDrawer.CanEditComponents)) return;
            var deleting = this.selected.ToArray();
            foreach (var ent in deleting) if (EntityDrawer.CanEditComponents(ent)) ent.DestroyHierarchy();
            this.selected.Clear(); this.DrawEntities(); this.PublishSelection();
        }

        private bool MatchesSearch(Ent ent) {
            if (string.IsNullOrWhiteSpace(this.search) || this.selected.Contains(ent)) return true;
            foreach (var type in this.searchTypes) {
                if (StaticTypesLoadedManaged.typeToId.TryGetValue(type, out var id) && Components.HasUnknownType(ent.World.state, id, ent.id, ent.gen, true)) return true;
            }
            var name = ent.EditorName.ToString();
            var idName = "#" + ent.id;
            foreach (var term in this.searchNames) if (name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0 || idName.Contains(term)) return true;
            return false;
        }

        private void DrawEntities() {
            if (this.treeView?.viewController == null || !this.selectedWorld.isCreated) return;
            this.allEntities.Clear(); this.snapshotParents.Clear();
            var state = this.selectedWorld.state;
            var bits = new TempBitArray(state.ptr->entities.aliveBits.Length, allocator: Constants.ALLOCATOR_TEMP);
            bits.Union(in state.ptr->allocator, in state.ptr->entities.aliveBits);
            var alive = bits.GetTrueBitsTemp();
            for (var i = 0; i < alive.Length; ++i) {
                var ent = new Ent(alive[i], this.selectedWorld);
                if (!ent.IsAlive() || !this.MatchesSearch(ent)) continue;
                this.allEntities.Add(ent);
                this.snapshotParents[ent] = ent.Has<ParentComponent>() ? ent.Read<ParentComponent>().value : default;
            }
            alive.Dispose();
            bits.Dispose();
            var changed = this.current.Count != this.allEntities.Count;
            foreach (var pair in this.snapshotParents) {
                if (!this.observedParents.TryGetValue(pair.Key, out var previous) || !previous.Equals(pair.Value)) { changed = true; break; }
            }
            var removedSelection = this.selected.RemoveWhere(ent => !ent.IsAlive());
            if (changed) {
                    var wasSynchronizing = this.synchronizingSelection;
                this.synchronizingSelection = true;
                try {
                    this.cache.Clear(); this.cache.AddRange(this.allEntities); this.cache.Sort((a, b) => a.id.CompareTo(b.id));
                    this.observedParents.Clear();
                    foreach (var pair in this.snapshotParents) this.observedParents.Add(pair.Key, pair.Value);
                    this.parents.Clear();
                    foreach (var pair in this.snapshotParents) this.parents[pair.Key] = this.allEntities.Contains(pair.Value) ? pair.Value : default;
                    // Break malformed parent cycles iteratively; never recurse over live Children lists.
                    this.traversal.Clear();
                    foreach (var ent in this.cache) {
                        if (this.traversal.ContainsKey(ent)) continue;
                        this.pathBuffer.Clear(); var cursor = ent;
                        while (!cursor.IsEmpty() && !this.traversal.ContainsKey(cursor)) {
                            this.traversal[cursor] = 1; this.pathBuffer.Add(cursor); cursor = this.parents[cursor];
                        }
                        if (!cursor.IsEmpty() && this.traversal[cursor] == 1 && this.pathBuffer.Count > 0) this.parents[this.pathBuffer[this.pathBuffer.Count - 1]] = default;
                        foreach (var visited in this.pathBuffer) this.traversal[visited] = 2;
                    }
                    this.roots.Clear(); this.parentIds.Clear(); this.staleParentIds.Clear();
                    foreach (var parent in this.parents.Values) if (!parent.IsEmpty()) this.parentIds.Add((int)parent.id);
                    foreach (var pair in this.dics) {
                        if (!this.parentIds.Contains(pair.Key)) this.staleParentIds.Add(pair.Key);
                        else pair.Value.Clear();
                    }
                    foreach (var id in this.staleParentIds) this.dics.Remove(id);
                    foreach (var id in this.parentIds) if (!this.dics.ContainsKey(id)) this.dics[id] = new scg::List<TreeViewItemData<Ent>>();
                    foreach (var ent in this.cache) {
                        this.dics.TryGetValue((int)ent.id, out var children);
                        var item = new TreeViewItemData<Ent>((int)ent.id, ent, children);
                        var parent = this.parents[ent];
                        if (parent.IsEmpty()) this.roots.Add(item); else this.dics[(int)parent.id].Add(item);
                    }
                    foreach (var old in this.current) if (!old.IsAlive()) this.treeView.CollapseItem((int)old.id, false, false);
                    this.current.Clear(); this.current.UnionWith(this.allEntities);
                    this.treeView.SetRootItems(this.roots);
                    this.expandedEntities.RemoveWhere(ent => !ent.IsAlive());
                    foreach (var ent in this.expandedEntities) if (this.current.Contains(ent)) this.treeView.ExpandItem((int)ent.id, false, true);
                    this.treeView.RefreshItems();
                    this.RestoreSelection(false);
                } finally { this.synchronizingSelection = wasSynchronizing; }
            } else this.treeView.RefreshItems(); // Only virtualized visible rows are rebound.
            if (removedSelection > 0 && !this.synchronizingSelection) this.OnSelectionChanged();
            if (this.settingsChanged) this.SaveSettings();
            this.settingsChanged = false;
        }
    }
}
