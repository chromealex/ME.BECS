namespace ME.BECS.Editor.FeaturesGraph {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using ME.BECS.Extensions.GraphProcessor;
    using ME.BECS.FeaturesGraph;
    using ME.BECS.FeaturesGraph.Nodes;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using GraphNode = ME.BECS.FeaturesGraph.Nodes.GraphNode;

    /// <summary>An opt-in editor. The legacy window and asset double-click handler are unchanged.</summary>
    public sealed class FeaturesGraphStudioWindow : EditorWindow {

        [SerializeField] private SystemsGraph currentGraph;
        [SerializeField] private List<SystemsGraph> path = new List<SystemsGraph>();
        [SerializeField] private int phaseMask = FeaturesGraphStudioMetadata.AllPhases;
        [SerializeField] private bool showCompilerPlan;
        private FeaturesGraphStudioView canvas;
        private VisualElement canvasHost;
        private readonly Dictionary<string, (float zoom, Vector2 offset)> cameraStates = new Dictionary<string, (float zoom, Vector2 offset)>();
        private string canvasPath;
        private VisualElement inspector;
        internal static ScrollView CreateStudioScroll(ScrollViewMode mode = ScrollViewMode.Vertical) {
            return new ScrollView(mode);
        }

        private VisualElement breadcrumbs;
        private VisualElement graphPicker;
        private VisualElement usedGraphPicker;
        private VisualElement usedGraphRow;
        private List<SystemsGraph> graphAssets;
        private Label status;
        private TextField compilerPlan;
        private Button phaseField;
        private Button saveButton;
        private Button compileButton;
        private Button clearSearch;
        private static Font monoFont;
        private BaseNode selectedNode;
        private SerializedObject serializedGraph;
        private IVisualElementScheduledItem refresh;
        private bool rebuilding;
        private string inspectorSnapshot;
        [SerializeField] private string systemQuery = "";
        private TextField systemSearch;
        private ScrollView searchResults;
        private readonly List<SystemHit> systemHits = new List<SystemHit>();
        private sealed class SystemHit {
            internal List<SystemsGraph> path;
            internal SystemNode node;
        }

        [MenuItem("ME.BECS/Features Graph Studio")]
        public static void OpenWindow() {
            var window = GetWindow<FeaturesGraphStudioWindow>();
            EditorUIUtils.ApplyWindowIcon(window, "Graph Studio", "ME.BECS.Resources/Icons/icon-graphstudio.png");
            window.minSize = new Vector2(700, 420);
            if (Selection.activeObject is SystemsGraph graph) window.OpenRoot(graph);
            window.Show();
        }

        [MenuItem("Assets/ME.BECS/Open in Graph Studio", false, 2000)]
        private static void OpenSelected() => OpenWindow();

        [MenuItem("Assets/ME.BECS/Open in Graph Studio", true)]
        private static bool CanOpenSelected() => Selection.activeObject is SystemsGraph;

        private void OnEnable() {
            EditorUIUtils.ApplyWindowIcon(this, "Graph Studio", "ME.BECS.Resources/Icons/icon-graphstudio.png");
            this.minSize = new Vector2(700, 420);
            Undo.undoRedoPerformed += this.OnUndo;
            EditorApplication.projectChanged += this.OnProjectChanged;
            EditorApplication.playModeStateChanged += this.OnPlayModeChanged;
            Themes.Changed += this.ApplyStudioTheme;
        }

        private void OnDisable() {
            this.SaveCamera();
            Undo.undoRedoPerformed -= this.OnUndo;
            EditorApplication.projectChanged -= this.OnProjectChanged;
            EditorApplication.playModeStateChanged -= this.OnPlayModeChanged;
            Themes.Changed -= this.ApplyStudioTheme;
            this.UnsubscribeGraph();
            this.refresh?.Pause();
            this.canvas?.Dispose();
            this.inspector?.Unbind();
            this.serializedGraph?.Dispose();
            this.serializedGraph = null;
        }

        public void CreateGUI() {
            this.SaveCamera();
            this.UnsubscribeGraph();
            this.rootVisualElement.Clear();
            this.rootVisualElement.UnregisterCallback<KeyDownEvent>(this.OnWindowKeyDown, TrickleDown.TrickleDown);
            this.rootVisualElement.RegisterCallback<KeyDownEvent>(this.OnWindowKeyDown, TrickleDown.TrickleDown);
            this.rootVisualElement.AddToClassList("becs-graph-studio");
            this.rootVisualElement.EnableInClassList("studio-dark", EditorGUIUtility.isProSkin);
            this.rootVisualElement.EnableInClassList("studio-light", !EditorGUIUtility.isProSkin);
            EditorUIUtils.ApplyDefaultStyles(this.rootVisualElement);
            this.ApplyStudioTheme();

            var searchRow = new VisualElement(); searchRow.AddToClassList("studio-search-row");
            var searchCaption = new Label("Find system"); searchCaption.AddToClassList("studio-navigation-caption"); searchRow.Add(searchCaption);
            this.systemSearch = new TextField { tooltip = "Search system names and types across project graphs" }; this.systemSearch.SetValueWithoutNotify(this.systemQuery);
            this.systemSearch.AddToClassList("studio-system-search");
            this.systemSearch.RegisterValueChangedCallback(evt => { this.systemQuery = evt.newValue; this.UpdateSearchClear(); this.RefreshGraphPicker(); });
            this.systemSearch.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Return && this.systemHits.Count > 0) { this.OpenSystemHit(this.systemHits[0]); evt.StopPropagation(); }
                if (evt.keyCode == KeyCode.Escape) this.systemSearch.value = "";
            }, TrickleDown.TrickleDown);
            searchRow.Add(this.systemSearch);
            this.clearSearch = new Button(() => this.systemSearch.value = "") { text = "×", tooltip = "Clear search" };
            this.clearSearch.AddToClassList("studio-search-clear"); this.systemSearch.Add(this.clearSearch);
            this.UpdateSearchClear();
            this.rootVisualElement.Add(searchRow);
            this.searchResults = CreateStudioScroll(); this.searchResults.AddToClassList("studio-search-results");
            EditorUIUtils.AddVerticalScrollFades(this.searchResults);
            this.rootVisualElement.Add(this.searchResults);
            var graphs = new VisualElement();
            graphs.AddToClassList("studio-projects");
            var graphsLabel = new Label("Project graphs");
            graphsLabel.AddToClassList("studio-navigation-caption");
            graphs.Add(graphsLabel);
            var scroll = CreateStudioScroll(ScrollViewMode.Horizontal);
            scroll.style.flexGrow = 1;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("studio-navigation-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            this.graphPicker = scroll.contentContainer;
            this.graphPicker.style.flexDirection = FlexDirection.Row;
            EditorUIUtils.AddHorizontalScrollFades(scroll);
            graphs.Add(scroll);
            this.rootVisualElement.Add(graphs);

            this.usedGraphRow = new VisualElement();
            this.usedGraphRow.AddToClassList("studio-projects");
            var usedLabel = new Label("Used subgraphs");
            usedLabel.AddToClassList("studio-navigation-caption");
            this.usedGraphRow.Add(usedLabel);
            var usedScroll = CreateStudioScroll(ScrollViewMode.Horizontal);
            usedScroll.style.flexGrow = 1;
            usedScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            usedScroll.AddToClassList("studio-navigation-scroll");
            usedScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            this.usedGraphPicker = usedScroll.contentContainer;
            this.usedGraphPicker.style.flexDirection = FlexDirection.Row;
            EditorUIUtils.AddHorizontalScrollFades(usedScroll);
            this.usedGraphRow.Add(usedScroll);
            this.rootVisualElement.Add(this.usedGraphRow);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("studio-toolbar");
            this.breadcrumbs = new VisualElement();
            this.breadcrumbs.AddToClassList("studio-breadcrumbs");
            this.breadcrumbs.style.flexGrow = 1;
            toolbar.Add(this.breadcrumbs);
            this.phaseField = new Button(this.ShowPhaseMenu); this.phaseField.AddToClassList("studio-phase-filter");
            this.UpdatePhaseButton(); toolbar.Add(this.phaseField);
            toolbar.Add(new Button(() => this.canvas?.FrameAll()) { text = "Reset view" });
            this.saveButton = new Button(this.Save) { text = "Save" }; toolbar.Add(this.saveButton);
            this.compileButton = new Button(this.Compile) { text = "Compile Graphs" }; toolbar.Add(this.compileButton);
            var planToggle = new Toggle { text = "Compiler plan", value = this.showCompilerPlan };
            planToggle.RegisterValueChangedCallback(evt => {
                this.showCompilerPlan = evt.newValue;
                this.UpdateCompilerPlan();
            });
            toolbar.Add(planToggle);
            this.rootVisualElement.Add(toolbar);

            var workspace = new VisualElement();
            workspace.style.flexDirection = FlexDirection.Row;
            workspace.style.flexGrow = 1;
            workspace.style.flexBasis = 0;
            workspace.style.minHeight = 0;
            this.canvasHost = new VisualElement();
            this.canvasHost.AddToClassList("studio-canvas-host");
            this.canvasHost.style.flexGrow = 1;
            this.canvasHost.style.minWidth = 0;
            this.canvasHost.style.minHeight = 0;
            this.canvasHost.style.flexBasis = 0;
            workspace.Add(this.canvasHost);
            var inspectorScroll = CreateStudioScroll();
            inspectorScroll.AddToClassList("studio-inspector");
            inspectorScroll.style.width = 320;
            inspectorScroll.style.flexShrink = 0;
            this.inspector = inspectorScroll.contentContainer;
            this.inspector.RegisterCallback<SerializedPropertyChangeEvent>(this.OnPropertyChanged);
            workspace.Add(inspectorScroll);
            this.rootVisualElement.Add(workspace);

            this.compilerPlan = new TextField { multiline = true, isReadOnly = true };
            this.compilerPlan.AddToClassList("studio-compiler-plan");
            this.compilerPlan.style.height = 180;
            monoFont ??= Font.CreateDynamicFontFromOSFont(new[] { "Menlo", "Consolas", "DejaVu Sans Mono", "Courier New" }, 12);
            var planInput = this.compilerPlan.Q(className: "unity-text-field__input");
            if (planInput != null) { planInput.style.unityFont = monoFont; planInput.style.unityFontDefinition = new FontDefinition { font = monoFont }; }
            this.rootVisualElement.Add(this.compilerPlan);
            this.status = new Label();
            this.status.AddToClassList("studio-status");
            this.rootVisualElement.Add(this.status);
            this.RefreshGraphPicker();
            this.path.RemoveAll(graph => graph == null);
            if (this.currentGraph != null) {
                if (this.path.Count == 0 || this.path[this.path.Count - 1] != this.currentGraph) {
                    this.path.Clear();
                    this.path.Add(this.currentGraph);
                }
                this.LoadCurrentGraph();
            } else this.OpenRoot(null);
        }

        private void RefreshGraphPicker() {
            if (this.graphPicker == null) return;
            if (this.canvas != null && this.currentGraph == null) this.OpenRoot(null);
            this.graphPicker.Clear();
            if (this.graphAssets == null) this.graphAssets = AssetDatabase.FindAssets("t:SystemsGraph")
                         .Select(guid => AssetDatabase.LoadAssetAtPath<SystemsGraph>(AssetDatabase.GUIDToAssetPath(guid)))
                         .Where(graph => graph != null).OrderBy(graph => graph.name, StringComparer.Ordinal).ToList();
            this.FindSystems();
            var root = this.path.Count > 0 ? this.path[0] : this.currentGraph;
            foreach (var graph in this.graphAssets.Where(graph => graph != null && !graph.isInnerGraph)) {
                var button = new Button(() => this.OpenRoot(graph)) { text = graph.name };
                button.AddToClassList("studio-project-button");
                button.EnableInClassList("studio-project-active", graph == root);
                button.EnableInClassList("studio-search-match", this.systemHits.Any(hit => hit.path[0] == graph));
                button.tooltip = AssetDatabase.GetAssetPath(graph);
                this.graphPicker.Add(button);
            }
            this.usedGraphPicker.Clear();
            var visited = new HashSet<SystemsGraph>();
            void AppendUsed(List<SystemsGraph> graphPath) {
                var graph = graphPath[graphPath.Count - 1];
                if (graph == null || !visited.Add(graph)) return;
                if (graphPath.Count > 1) {
                    var capturedPath = graphPath.ToList();
                    var button = new Button(() => this.OpenPath(capturedPath)) {
                        text = string.Join(" › ", graphPath.Skip(1).Select(value => value.name)),
                        tooltip = string.Join(" › ", graphPath.Select(value => value.name)) + "\n" + AssetDatabase.GetAssetPath(graph),
                    };
                    button.AddToClassList("studio-project-button");
                    button.EnableInClassList("studio-project-active", graph == this.currentGraph);
                    button.EnableInClassList("studio-search-match", this.systemHits.Any(hit => hit.path.Contains(graph)));
                    this.usedGraphPicker.Add(button);
                }
                foreach (var nested in graph.nodes.OfType<GraphNode>()) {
                    if (nested.graphValue == null) continue;
                    var next = graphPath.ToList(); next.Add(nested.graphValue); AppendUsed(next);
                }
            }
            if (root != null) AppendUsed(new List<SystemsGraph> { root });
            this.usedGraphRow.style.display = this.usedGraphPicker.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            this.canvas?.HighlightSystems(this.systemQuery);
        }

        private void UpdateSearchClear() {
            if (this.clearSearch != null) this.clearSearch.style.display = string.IsNullOrEmpty(this.systemQuery) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ShowPhaseMenu() {
            var menu = new GenericMenu();
            void Set(int mask) { this.phaseMask = mask; this.UpdatePhaseButton(); this.canvas?.SetPhaseMask(mask); this.DrawInspector(); this.UpdateCompilerPlan(); }
            menu.AddItem(new GUIContent("All"), this.phaseMask == FeaturesGraphStudioMetadata.AllPhases, () => Set(FeaturesGraphStudioMetadata.AllPhases));
            menu.AddItem(new GUIContent("None"), this.phaseMask == 0, () => Set(0)); menu.AddSeparator("");
            foreach (var method in FeaturesGraphStudioMetadata.Phases) {
                var bit = FeaturesGraphStudioMetadata.PhaseBit(method);
                menu.AddItem(new GUIContent(method.ToString()), (this.phaseMask & bit) != 0, () => Set(this.phaseMask ^ bit));
            }
            menu.ShowAsContext();
        }

        private void UpdatePhaseButton() {
            if (this.phaseField == null) return;
            this.phaseField.text = this.phaseMask == FeaturesGraphStudioMetadata.AllPhases ? "All ▾" : this.phaseMask == 0 ? "None ▾" :
                string.Join(" + ", FeaturesGraphStudioMetadata.Phases.Where(method => FeaturesGraphStudioMetadata.Includes(this.phaseMask, method))) + " ▾";
        }

        private void UpdateDirtyButtons() {
            var visited = new HashSet<SystemsGraph>(); bool unsaved = false, uncompiled = false;
            void Visit(SystemsGraph graph) {
                if (graph == null || !visited.Add(graph)) return;
                unsaved |= EditorUtility.IsDirty(graph);
                if (!graph.builtInGraph && EditorUtility.IsDirty(graph)) SourceGeneratorInputRefresh.DeferGraphCompilation(graph);
                uncompiled |= SourceGeneratorInputRefresh.IsGraphCompilationDeferred(graph);
                foreach (var child in graph.nodes.OfType<GraphNode>()) Visit(child.graphValue);
            }
            Visit(this.path.Count > 0 ? this.path[0] : this.currentGraph);
            if (this.saveButton != null) this.saveButton.text = unsaved ? "Save *" : "Save";
            if (this.compileButton != null) this.compileButton.text = uncompiled ? "Compile Graphs *" : "Compile Graphs";
        }

        private void OnWindowKeyDown(KeyDownEvent evt) {
            if (evt.actionKey && evt.keyCode == KeyCode.F) {
                this.systemSearch?.Focus(); this.systemSearch?.SelectAll();
                evt.StopPropagation();
            }
        }

        private void FindSystems() {
            this.systemHits.Clear(); this.searchResults.Clear();
            var query = (this.systemQuery ?? "").Trim();
            this.searchResults.style.display = query.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (query.Length == 0) return;
            foreach (var root in this.graphAssets.Where(graph => graph != null && !graph.isInnerGraph)) {
                var visited = new HashSet<SystemsGraph>();
                void Visit(List<SystemsGraph> graphPath) {
                    var graph = graphPath[graphPath.Count - 1];
                    if (!visited.Add(graph)) return;
                    foreach (var node in graph.nodes.OfType<SystemNode>()) {
                        var typeName = node.system?.GetType().FullName ?? "";
                        if (FeaturesGraphStudioMetadata.GetSystemTitle(node.system).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            typeName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            this.systemHits.Add(new SystemHit { path = graphPath.ToList(), node = node });
                    }
                    foreach (var child in graph.nodes.OfType<GraphNode>()) {
                        if (child.graphValue == null) continue;
                        var next = graphPath.ToList(); next.Add(child.graphValue); Visit(next);
                    }
                }
                Visit(new List<SystemsGraph> { root });
            }
            var count = new Label(this.systemHits.Count == 0 ? "No systems found" : $"{this.systemHits.Count} matches · Enter opens the first result");
            count.AddToClassList("studio-search-count"); this.searchResults.Add(count);
            foreach (var hit in this.systemHits) {
                var result = new Button(() => this.OpenSystemHit(hit)); result.AddToClassList("studio-search-result");
                var title = new Label(FeaturesGraphStudioMetadata.GetSystemTitle(hit.node.system)); title.AddToClassList("studio-result-title"); result.Add(title);
                var location = new Label(string.Join(" › ", hit.path.Select(graph => graph.name))); location.AddToClassList("studio-result-path"); result.Add(location);
                result.tooltip = hit.node.system?.GetType().FullName; this.searchResults.Add(result);
            }
        }

        private void OpenSystemHit(SystemHit hit) {
            this.OpenPath(hit.path);
            if (this.currentGraph != hit.path.Last() || !this.currentGraph.nodes.Contains(hit.node)) return;
            this.canvas.Select(hit.node); this.SelectNode(hit.node);
            this.canvas.schedule.Execute(() => this.canvas.FrameNode(hit.node)).ExecuteLater(40);
        }

        private void OpenPath(List<SystemsGraph> graphPath) {
            if (graphPath.Count == 0 || graphPath.Any(graph => graph == null)) return;
            for (var i = 1; i < graphPath.Count; ++i)
                if (!graphPath[i - 1].nodes.OfType<GraphNode>().Any(node => node.graphValue == graphPath[i])) {
                    this.RefreshGraphPicker(); this.SetStatus("This subgraph path has changed."); return;
                }
            this.UnsubscribeGraph();
            this.path = graphPath.ToList();
            this.currentGraph = this.path[this.path.Count - 1];
            this.LoadCurrentGraph();
        }

        private void ApplyStudioTheme() {
            EditorUIUtils.ApplyWindowIcon(this, "Graph Studio", "ME.BECS.Resources/Icons/icon-graphstudio.png");
            var root = this.rootVisualElement;
            EditorUIUtils.ApplyDefaultStyles(root);
            root.AddToClassList("world-dashboard");
            root.AddToClassList("becs-editor-window");
            root.EnableInClassList("dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("light", !EditorGUIUtility.isProSkin);
            root.EnableInClassList("studio-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("studio-light", !EditorGUIUtility.isProSkin);
            // Match Hierarchy's explicit text inheritance, preserving Unity's own sheets.
            var skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
            if (skin != null && skin.font != null) root.style.unityFont = skin.font;
            root.style.fontSize = 12;
            root.style.color = StyleKeyword.Null;
            this.AttachStudioStyles();
            root.MarkDirtyRepaint();
        }

        private void AttachStudioStyles() {
            // Editor resources can be imported after CreateGUI during a domain reload.
            // Use the same asset-aware loader as the other BECS editor windows, then
            // retry on projectChanged instead of leaving an open window unstyled.
            var dashboard = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldDashboard.uss", false);
            if (dashboard != null && !this.rootVisualElement.styleSheets.Contains(dashboard))
                this.rootVisualElement.styleSheets.Add(dashboard);
            var styles = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/FeaturesGraphStudio.uss", false);
            if (styles != null && !this.rootVisualElement.styleSheets.Contains(styles))
                this.rootVisualElement.styleSheets.Add(styles);
        }

        private void OnProjectChanged() {
            this.ApplyStudioTheme();
            this.graphAssets = null;
            this.RefreshGraphPicker();
            this.UpdateDirtyButtons();
        }

        private void OpenRoot(SystemsGraph graph) {
            this.UnsubscribeGraph();
            this.path.Clear();
            if (graph != null) this.path.Add(graph);
            this.currentGraph = graph;
            if (this.canvasHost != null) this.LoadCurrentGraph();
        }

        internal void OpenSubgraph(GraphNode node) {
            if (node.graphValue == null) { this.SetStatus("Assign a subgraph first."); return; }
            if (this.path.Contains(node.graphValue)) {
                this.SetStatus("Cannot open a recursive subgraph reference.");
                return;
            }
            this.UnsubscribeGraph();
            this.path.Add(node.graphValue);
            this.currentGraph = node.graphValue;
            this.LoadCurrentGraph();
        }

        private void NavigateTo(int index) {
            this.UnsubscribeGraph();
            this.path.RemoveRange(index + 1, this.path.Count - index - 1);
            this.currentGraph = this.path[index];
            this.LoadCurrentGraph();
        }

        private void UnsubscribeGraph() {
            if (this.currentGraph != null) this.currentGraph.onGraphChanges -= this.OnAssetChanged;
        }

        private void SaveCamera() {
            if (this.canvas != null && this.canvasPath != null) this.cameraStates[this.canvasPath] = this.canvas.CaptureCamera();
        }

        private void LoadCurrentGraph() {
            this.SaveCamera();
            this.refresh?.Pause();
            this.inspector.Unbind();
            this.serializedGraph?.Dispose();
            this.serializedGraph = null;
            this.selectedNode = null;
            this.canvas?.Dispose();
            this.canvasHost.Clear();
            this.canvas = null; this.canvasPath = null;
            this.breadcrumbs.Clear();
            for (var i = 0; i < this.path.Count; ++i) {
                var index = i;
                if (i > 0) {
                    var separator = new Label("›"); separator.AddToClassList("studio-breadcrumb-separator"); this.breadcrumbs.Add(separator);
                }
                var crumb = new Button(() => this.NavigateTo(index)) { text = this.path[i].name };
                crumb.AddToClassList("studio-breadcrumb-item");
                if (i == this.path.Count - 1) crumb.AddToClassList("studio-breadcrumb-current");
                this.breadcrumbs.Add(crumb);
            }
            if (this.currentGraph != null) {
                this.currentGraph.onGraphChanges -= this.OnAssetChanged;
                this.currentGraph.onGraphChanges += this.OnAssetChanged;
                this.serializedGraph = new SerializedObject(this.currentGraph);
                this.canvas = new FeaturesGraphStudioView(this, this.currentGraph, this.phaseMask);
                this.canvasHost.Add(this.canvas);
                this.canvasPath = string.Join("/", this.path.Select(graph => graph.GetInstanceID().ToString()));
                var loadedCanvas = this.canvas;
                if (this.cameraStates.TryGetValue(this.canvasPath, out var camera))
                    loadedCanvas.schedule.Execute(() => loadedCanvas.RestoreCamera(camera));
                else loadedCanvas.schedule.Execute(loadedCanvas.FrameAll);
                this.SetStatus(this.currentGraph.builtInGraph ? "Built-in graph · read only" :
                    "Automatic layout · + on a connection inserts a node · + below a node adds a branch · drag to pan · wheel to zoom.");
            } else {
                var empty = new Label("Select a SystemsGraph asset to start.");
                empty.AddToClassList("empty-label");
                this.canvasHost.Add(empty);
                this.SetStatus("No graph selected.");
            }
            this.DrawInspector();
            this.UpdateCompilerPlan();
            this.RefreshGraphPicker();
            this.UpdateDirtyButtons();
        }

        internal void SelectNode(BaseNode node) {
            if (this.selectedNode == node) return;
            this.selectedNode = node;
            if (this.rebuilding) return;
            this.DrawInspector();
        }

        private void DrawInspector() {
            if (this.inspector == null) return;
            this.inspector.Unbind();
            this.inspector.Clear();
            if (this.currentGraph == null || this.selectedNode == null || !this.currentGraph.nodes.Contains(this.selectedNode)) {
                var empty = new VisualElement(); empty.AddToClassList("studio-inspector-empty");
                empty.Add(new Label("Node details"));
                var hint = new Label("Select a system to view its methods and edit its fields."); hint.AddToClassList("studio-note"); empty.Add(hint);
                this.inspector.Add(empty); return;
            }
            this.serializedGraph.Update();
            this.inspectorSnapshot = EditorJsonUtility.ToJson(this.currentGraph);
            var node = this.selectedNode;
            var property = this.serializedGraph.FindProperty("nodes").GetArrayElementAtIndex(this.currentGraph.nodes.IndexOf(node));
            var heading = new VisualElement(); heading.AddToClassList("studio-inspector-heading");
            var kind = new Label(node is SystemNode ? "SYSTEM" : node is GraphNode ? "SUBGRAPH" : "GRAPH NODE"); kind.AddToClassList("studio-note"); heading.Add(kind);
            var title = new Label(node is SystemNode typed ? FeaturesGraphStudioMetadata.GetSystemTitle(typed.system) : node.name);
            title.AddToClassList("studio-inspector-title"); heading.Add(title);
            this.inspector.Add(heading);
            var editable = !this.currentGraph.builtInGraph && !EditorApplication.isPlayingOrWillChangePlaymode;
            if (!editable) { var note = new Label("Read only"); note.AddToClassList("studio-note"); heading.Add(note); }
            if (node is SystemNode system) {
                var selection = this.InspectorSection("System type"); selection.AddToClassList("studio-system-selection");
                var systemProperty = property.FindPropertyRelative("system");
                this.AddProperty(systemProperty, "System", editable, selection);
                if (system.system != null) {
                    var typeNote = new Label(system.system.GetType().Namespace); typeNote.AddToClassList("studio-note"); selection.Add(typeNote);
                    var methods = this.InspectorSection("Lifecycle methods");
                    foreach (var callback in FeaturesGraphStudioMetadata.GetCallbacks(system.system)) {
                        var button = new Button(() => {
                            this.phaseMask = FeaturesGraphStudioMetadata.PhaseBit(callback.phase); this.UpdatePhaseButton(); this.RefreshCanvas(); this.DrawInspector(); this.UpdateCompilerPlan();
                        });
                        button.AddToClassList("studio-inspector-method");
                        button.EnableInClassList("studio-method-active", FeaturesGraphStudioMetadata.Includes(this.phaseMask, callback.phase));
                        button.Add(new Label(callback.phase.ToString()));
                        var badge = new Label(callback.burst ? "Burst" : "C#"); badge.AddToClassList(callback.burst ? "studio-burst" : "studio-note"); button.Add(badge);
                        button.tooltip = callback.source; methods.Add(button);
                    }
                } else { var hint = new Label("Choose a system type above."); hint.AddToClassList("studio-note"); selection.Add(hint); }
            } else if (node is GraphNode nested) {
                var section = this.InspectorSection("Referenced graph");
                var field = new ObjectField("Graph") { objectType = typeof(SystemsGraph), allowSceneObjects = false, value = nested.graphValue };
                field.SetEnabled(editable);
                field.RegisterValueChangedCallback(evt => {
                    Undo.RegisterCompleteObjectUndo(this.currentGraph, "Assign subgraph"); nested.graphValue = evt.newValue as SystemsGraph;
                    this.currentGraph.NotifyNodeChanged(node); this.GraphChanged();
                });
                section.Add(field); section.Add(new Button(() => this.OpenSubgraph(nested)) { text = "Open subgraph" });
            }
        }

        private VisualElement InspectorSection(string title) {
            var section = new VisualElement(); section.AddToClassList("studio-inspector-section");
            var caption = new Label(title); caption.AddToClassList("studio-section-title"); section.Add(caption);
            this.inspector.Add(section); return section;
        }

        private void AddProperty(SerializedProperty property, string label, bool editable, VisualElement parent) {
            if (property == null) return;
            var field = label == null ? new PropertyField(property) : new PropertyField(property, label);
            field.SetEnabled(editable);
            if (parent.ClassListContains("studio-system-selection")) { field.style.minHeight = 38; field.style.flexShrink = 0; }
            parent.Add(field); field.Bind(this.serializedGraph);
        }

        private void OnPropertyChanged(SerializedPropertyChangeEvent evt) {
            if (this.rebuilding || this.currentGraph == null || this.selectedNode == null) return;
            // Binding also emits change events on initial values. Only actual asset edits
            // may rebuild the canvas; selecting a card must retain its action buttons.
            var snapshot = EditorJsonUtility.ToJson(this.currentGraph);
            if (snapshot == this.inspectorSnapshot) return;
            this.inspectorSnapshot = snapshot;
            this.currentGraph.NotifyNodeChanged(this.selectedNode);
            this.GraphChanged();
        }

        internal void InsertOnEdge(Type type, SerializableEdge edge) {
            if (this.currentGraph == null || this.canvas == null || this.canvas.IsReadOnly) return;
            var node = FeaturesGraphStudioCommands.InsertOnEdge(this.currentGraph, type, edge, out var error);
            if (node == null) { this.SetStatus(error); return; }
            this.selectedNode = node;
            this.GraphChanged();
        }

        internal void InsertNode(Type type, BaseNode anchor, bool parallel) {
            if (this.currentGraph == null || this.currentGraph.builtInGraph || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var node = FeaturesGraphStudioCommands.Insert(this.currentGraph, type, anchor, parallel, out var error);
            if (node == null) { this.SetStatus(error); return; }
            this.selectedNode = node;
            this.GraphChanged();
        }

        internal void GraphChanged(bool refreshCanvas = true) {
            if (this.currentGraph == null) return;
            EditorUtility.SetDirty(this.currentGraph);
            SourceGeneratorInputRefresh.DeferGraphCompilation(this.currentGraph);
            this.UpdateDirtyButtons();
            if (refreshCanvas) this.QueueRefresh();
            else this.canvas?.RefreshExecution();
            this.SetStatus("Graph modified · use Save or Compile Graphs.");
        }

        private void OnAssetChanged(GraphChanges changes) => this.QueueRefresh();

        private void QueueRefresh() {
            this.refresh?.Pause();
            this.refresh = this.rootVisualElement.schedule.Execute(() => {
                this.RefreshCanvas();
                this.DrawInspector();
                this.UpdateCompilerPlan();
                this.RefreshGraphPicker();
            });
            this.refresh.ExecuteLater(100);
        }

        private void RefreshCanvas() {
            if (this.canvas == null || this.currentGraph == null) return;
            this.rebuilding = true;
            try {
                this.UpdatePhaseButton();
                this.canvas.SetPhaseMask(this.phaseMask);
                this.canvas.Rebuild();
                this.canvas.Select(this.selectedNode);
            } finally { this.rebuilding = false; }
        }

        private void OnUndo() {
            if (this.currentGraph == null) return;
            // Port caches need to be deserialized after a managed-reference Undo.
            this.currentGraph.Deserialize();
            this.selectedNode = null;
            this.QueueRefresh();
            SourceGeneratorInputRefresh.DeferGraphCompilation(this.currentGraph);
            this.UpdateDirtyButtons();
        }

        private void OnPlayModeChanged(PlayModeStateChange state) => this.QueueRefresh();

        private void Save() {
            if (this.currentGraph == null) return;
            var visited = new HashSet<SystemsGraph>();
            void SaveGraph(SystemsGraph graph) {
                if (graph == null || !visited.Add(graph)) return;
                if (!graph.builtInGraph && EditorUtility.IsDirty(graph)) {
                    // Set the manual compilation flag before saving: saving may
                    // enqueue an input refresh, but must not publish this draft.
                    SourceGeneratorInputRefresh.DeferGraphCompilation(graph);
                    AssetDatabase.SaveAssetIfDirty(graph);
                }
                foreach (var child in graph.nodes.OfType<GraphNode>()) SaveGraph(child.graphValue);
            }
            SaveGraph(this.path.Count > 0 ? this.path[0] : this.currentGraph);
            this.UpdateDirtyButtons();
            this.SetStatus("Graphs saved. Compile Graphs to publish changes.");
        }

        private void Compile() {
            if (this.currentGraph == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) {
                this.SetStatus("Wait for Edit Mode and compilation/import to finish.");
                return;
            }
            this.Save();
            // Compile publishes the project snapshot. Save all changed graph
            // assets before export, including changes made outside this window.
            foreach (var graph in this.graphAssets ?? new List<SystemsGraph>())
                if (graph != null && !graph.builtInGraph && EditorUtility.IsDirty(graph)) {
                    SourceGeneratorInputRefresh.DeferGraphCompilation(graph);
                    AssetDatabase.SaveAssetIfDirty(graph);
                }
            var requestedGraph = this.currentGraph;
            var queued = SourceGeneratorInputRefresh.RequestExport(successful => {
                if (this == null || this.currentGraph != requestedGraph) return;
                this.UpdateDirtyButtons();
                this.SetStatus(successful ?
                    "Graph inputs exported. Unity compilation will update the compiler plan." :
                    "Graph export cancelled or incomplete. See Console for diagnostics.");
            });
            this.SetStatus(queued ? "Analyzing graph inputs in background..." : "Export already running or unavailable; retry later.");
        }

        internal void SetStatus(string message) {
            if (this.status != null) this.status.text = message;
        }

        private void UpdateCompilerPlan() {
            if (this.compilerPlan == null) return;
            this.compilerPlan.style.display = this.showCompilerPlan ? DisplayStyle.Flex : DisplayStyle.None;
            if (!this.showCompilerPlan) return;
            if (this.phaseMask == 0) { this.compilerPlan.SetValueWithoutNotify("No lifecycle methods selected."); return; }
            var root = this.path.Count > 0 ? this.path[0] : this.currentGraph;
            if (root == null) { this.compilerPlan.SetValueWithoutNotify("No graph selected."); return; }
            try {
                var snapshot = SourceGeneratorGraphTopology.Serialize(root);
                var plans = new List<string>();
                var snapshots = new List<string>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    if (assembly.IsDynamic) continue;
                    foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>()) {
                        if (attribute.Key == "ME.BECS.GraphLifecyclePlan.v1") {
                            var rows = attribute.Value?.Split('\n');
                            if (rows != null && rows.Length >= 3 && rows[0] == root.GetId().ToString(System.Globalization.CultureInfo.InvariantCulture) && FeaturesGraphStudioMetadata.Phases.Any(method => FeaturesGraphStudioMetadata.Includes(this.phaseMask, method) && rows[1] == method.ToString()))
                                plans.Add(attribute.Value);
                        } else if (attribute.Key == "ME.BECS.TypeInput.v1") {
                            var fields = attribute.Value?.Split('\t');
                            if (fields != null && fields.Length == 6 && fields[0] == "runtime" && fields[1] == "graph-topology" &&
                                fields[4] == root.GetId().ToString(System.Globalization.CultureInfo.InvariantCulture))
                                snapshots.Add(Encoding.UTF8.GetString(Convert.FromBase64String(fields[5])));
                        }
                    }
                }
                string WithoutSync(string text) => string.Join("\n", text.Split('\n').Where(line => !line.StartsWith("sync\t", StringComparison.Ordinal)));
                if (snapshots.Count != 1 || WithoutSync(snapshots[0]) != WithoutSync(snapshot)) {
                    this.compilerPlan.SetValueWithoutNotify("Compiler plan is missing or stale. Compile Graphs, then wait for Unity compilation.\nNo generated source files are read.");
                    return;
                }
                this.compilerPlan.SetValueWithoutNotify(plans.Count > 0 ?
                    root.name + " · " + this.phaseField.text + "\nCompiler lifecycle metadata (flattened dependencies and call groups):\n" + string.Join("\n\n", plans) :
                    "No unique compiled lifecycle plan. Compile Graphs and check Console.");
            } catch (Exception exception) {
                this.compilerPlan.SetValueWithoutNotify("Graph diagnostics: " + exception.Message);
            }
        }
    }
}
