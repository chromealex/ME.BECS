using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using ME.BECS.Editor;
using UnityEditor.Experimental.GraphView;

namespace ME.BECS.Blueprints.Editor {
    
    using ME.BECS.Extensions.GraphProcessor;
    
    [ME.BECS.Extensions.GraphProcessor.NodeCustomEditor(typeof(BlueprintNode))]
    public class NodeView : BaseNodeView {

        public override bool expanded => true;
        public override void OnCreated() {
            
            base.OnCreated();

            this.styleSheets.Add(GraphEditor.nodeStyleSheet);

        }

        public override bool RefreshPorts() {
            
            this.styleSheets.Add(GraphEditor.nodeStyleSheet);

            return base.RefreshPorts();
            
        }

    }

    [ME.BECS.Extensions.GraphProcessor.NodeCustomEditor(typeof(ME.BECS.Blueprints.Nodes.Operation))]
    public class OperationNodeView : NodeView {

        protected override void DrawDefaultInspector(bool fromInspector = false) {

            if (fromInspector == false) {

                var node = (ME.BECS.Blueprints.Nodes.Operation)this.nodeTarget;
                var drop = new DropdownField();
                this.controlsContainer.Add(drop);
                var enumType = typeof(ME.BECS.Blueprints.Nodes.Operation.OpType);
                var memberInfos = enumType.GetMembers();
                var values = System.Enum.GetValues(enumType);
                var items = memberInfos.Where(x => x.DeclaringType == enumType && x.GetCustomAttribute(typeof(HeaderAttribute)) != null).ToArray();
                drop.choices = items.Select(x => {
                    return ((HeaderAttribute)x.GetCustomAttribute(typeof(HeaderAttribute))).header;
                }).ToList();
                drop.index = System.Array.IndexOf(values, node.operationType);
                drop.RegisterValueChangedCallback((evt) => {
                    node.operationType = (ME.BECS.Blueprints.Nodes.Operation.OpType)values.GetValue(drop.index);
                });

            }
            
        }

    }

    public class BlueprintGraphView : BaseGraphView {
        
        public BlueprintGraphView(UnityEditor.EditorWindow window) : base(window) { }

    }

    public class GraphEditor : BaseGraphWindow {

        public static void ShowWindow(Graph graph) {

            var win = GraphEditor.CreateInstance<GraphEditor>();
            win.titleContent = new GUIContent("Blueprint Graph", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-blueprintgraph.png"));
            if (graph != null) {
                win.SelectAsset(graph);
            } else {
                win.OnSelectionChanged();
            }
            win.Show();

        }

        [UnityEditor.Callbacks.OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line) {
            var graph = UnityEditor.EditorUtility.InstanceIDToObject(instanceID) as ME.BECS.Blueprints.Graph;
            if (graph != null) {
                ShowWindow(graph);
                return true;
            }
            return false;
        }

        protected override void OnEnable() {
            
            base.OnEnable();

            UnityEditor.Selection.selectionChanged -= this.OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += this.OnSelectionChanged;

        }

        protected override void OnDestroy() {
            
            UnityEditor.Selection.selectionChanged -= this.OnSelectionChanged;
            if (this.graph != null) { 
                this.graph.onGraphChanges -= this.OnGraphChanged;
            }
            base.OnDestroy();
            
        }

        private void OnSelectionChanged() {

            var graph = UnityEditor.Selection.activeObject as Graph;
            if (graph != null) {

                this.SelectAsset(graph);

            }

        }

        private void OnGraphChanged(GraphChanges obj) {
        
            //Debug.Log("Dirty");
            this.hasUnsavedChanges = true;
            this.UpdateToolbar();

            if (obj.addedNode != null) {
                var node = (Graph.Node)obj.addedNode;
                node.OnCreated();
            }
            
        }

        private void UpdateToolbar() {
            this.saveButton.SetEnabled(true);//this.hasUnsavedChanges);

            if (this.graphView != null) {
                if (contextMenu == null) contextMenu = new ContextualMenuManipulator(this.graphView.BuildContextualMenu);
                this.graphView.RemoveManipulator(contextMenu);
                this.graphView.AddManipulator(contextMenu);
            }
        }

        private void SelectAsset(BaseGraph graph) {
            
            if (this.graph != null) { 
                this.graph.onGraphChanges -= this.OnGraphChanged;
            }
            
            this.titleContent = new GUIContent(graph.name, this.titleContent.image);
            this.graph = graph;
            this.graph.InitializeValidation();
            this.graph.onGraphChanges -= this.OnGraphChanged;
            this.graph.onGraphChanges += this.OnGraphChanged;
            this.hasUnsavedChanges = false;
            this.graphView = null;
            this.rootView.Clear();
            this.InitializeGraph(this.graph);
            
        }

        private static StyleSheet styleSheetBase;
        private static StyleSheet styleSheetTooltip;
        public static StyleSheet nodeStyleSheet;
        
        private void LoadStyle() {
            if (styleSheetBase == null) {
                styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/BlueprintsGraphEditorWindow.uss");
            }
            if (styleSheetTooltip == null) {
                styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }
            if (nodeStyleSheet == null) {
                nodeStyleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/BlueprintsGraphNode.uss");
            }
        }

        protected override void Update() {
            
            base.Update();
            
            if (this.background != null) this.background.MarkDirtyRepaint();
            
        }

        private Vector3 prevScale;
        private Vector3 prevPos;
        private const float maxOpacity = 1f;

        private void OnTransformChanged(UnityEditor.Experimental.GraphView.GraphView graphview) {
            this.OnTransformChanged(graphview, false);
        }

        private void OnTransformChanged(UnityEditor.Experimental.GraphView.GraphView graphview, bool forced) {

            if (this.graphView == null) return;
            if (forced == false &&
                this.prevScale == this.graphView.viewTransform.scale &&
                this.prevPos == this.graphView.viewTransform.position) return;

            this.prevScale = this.graphView.viewTransform.scale;
            this.prevPos = this.graphView.viewTransform.position;
            
            var scaleX = this.graphView.viewTransform.scale.x;
            var scaleY = this.graphView.viewTransform.scale.y;
            var op = Mathf.Lerp(0f, maxOpacity, Mathf.Clamp01(scaleX - 0.25f) * 2f);
            this.background.style.opacity = new StyleFloat(op);
            this.background.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Top, this.graphView.viewTransform.position.x));
            this.background.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Top, this.graphView.viewTransform.position.y));
            if (op <= 0f) {
                this.background.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(512f, 512f));
            } else {
                this.background.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(512f * scaleX, 512f * scaleY));
            }
            this.background.MarkDirtyRepaint();

            //this.graphView.focusable = true;
            //this.graphView.pickingMode = PickingMode.Position;
            //this.graphView.UnregisterCallback<ContextualMenuPopulateEvent>(this.graphView.BuildContextualMenu);
            //this.graphView.RegisterCallback<ContextualMenuPopulateEvent>(this.graphView.BuildContextualMenu);

            if (this.graphView != null) {
                if (contextMenu == null) contextMenu = new ContextualMenuManipulator(this.graphView.BuildContextualMenu);
                this.graphView.RemoveManipulator(contextMenu);
                this.graphView.AddManipulator(contextMenu);
            }
            
        }
        
        private UnityEngine.UIElements.VisualElement background;
        private UnityEditor.UIElements.Toolbar toolbar;
        private Button saveButton;
        private static IManipulator contextMenu;

        protected override void InitializeWindow(BaseGraph graph) {
            var view = new BlueprintGraphView(this);
            view.RegisterCallback<MouseMoveEvent>((evt) => {
                this.OnTransformChanged(view);
            });
            this.wantsMouseMove = true;
            view.viewTransformChanged += this.OnTransformChanged;
            this.rootView.Add(view);
            this.LoadStyle();
            view.styleSheets.Add(styleSheetBase);
            {
                var gridSpacing = 10f;
                var gridBlockSpacing = gridSpacing * 10f;
                var gridColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
                var gridBlockColor = new Color(0.1f, 0.1f, 0.1f, 1f);
                var back = new IMGUIContainer(() => {
                    static void DrawGrid(Vector2 min, Vector2 max, float spacing, Color gridColor, Vector2 offset, float opacity) {
                        
                        offset.x %= spacing;
                        offset.y %= spacing;
                        min.x -= offset.x;
                        max.x -= offset.x;
                        min.y -= offset.y;
                        max.y -= offset.y;
                        
                        //Get the bounding points, clamped by the spacing
                        Vector2 start = new Vector2(
                            Mathf.Ceil(min.x / spacing) * spacing,
                            Mathf.Ceil(min.y / spacing) * spacing
                        );
                        Vector2 end = new Vector2(
                            Mathf.Floor(max.x / spacing) * spacing,
                            Mathf.Floor(max.y / spacing) * spacing
                        );

                        gridColor.a *= opacity;
                        
                        //Find the number of interactions will be done for each axis
                        int widthLines = Mathf.CeilToInt((end.x - start.x) / spacing);
                        int heightLines = Mathf.CeilToInt((end.y - start.y) / spacing);

                        //Start the line rendering elements
                        UnityEditor.Handles.BeginGUI();
                        UnityEditor.Handles.color = gridColor;

                        //Render the grid lines
                        for (int x = 0; x <= widthLines; x++) {
                            UnityEditor.Handles.DrawLine(
                                new Vector3(start.x + x * spacing + offset.x, min.y + offset.y),
                                new Vector3(start.x + x * spacing + offset.x, max.y + offset.y)
                            );
                        }
                        for (int y = 0; y <= heightLines; y++) {
                            UnityEditor.Handles.DrawLine(
                                new Vector3(min.x + offset.x, start.y + y * spacing + offset.y),
                                new Vector3(max.x + offset.x, start.y + y * spacing + offset.y)
                            );
                        }

                        //End the rendering
                        UnityEditor.Handles.EndGUI();
                    }

                    var size = view.worldBound.size;
                    DrawGrid(Vector2.zero, size, gridSpacing * this.graphView.viewTransform.scale.x, gridColor, this.graphView.viewTransform.position, this.background.style.opacity.value);
                    DrawGrid(Vector2.zero, size, gridBlockSpacing * this.graphView.viewTransform.scale.x, gridBlockColor, this.graphView.viewTransform.position, 1f);
                });
                back.MarkDirtyRepaint();
                back.AddToClassList("background");
                this.background = back;
                view.Add(back);
                back.SendToBack();
            }
            if (this.toolbar != null && this.rootView.Contains(this.toolbar) == true) this.rootView.Remove(this.toolbar);
            var toolbar = new UnityEditor.UIElements.Toolbar();
            this.toolbar = toolbar;
            {
                var saveButton = new UnityEditor.UIElements.ToolbarButton(() => {
                    {
                        // Apply connections
                        var graph = (Graph)this.graph;
                        graph.connections = new Connection[graph.edges.Count];
                        for (var index = 0; index < graph.edges.Count; ++index) {
                            var edge = graph.edges[index];
                            var from = (Graph.Node)edge.outputNode;
                            var to = (Graph.Node)edge.inputNode;
                            var connection = new Connection();
                            connection.from = from.id;
                            connection.fromIndex = 0;
                            connection.to = to.id;
                            connection.toIndex = to.inputPorts.IndexOf(edge.inputPort);
                            graph.connections[index] = connection;
                        }
                    }
                    this.graphView.SaveGraphToDisk();
                    this.hasUnsavedChanges = false;
                    this.ShowNotification(new GUIContent("Graph Saved"), 1f);
                    this.UpdateToolbar();
                });
                saveButton.text = "Save Graph";
                this.saveButton = saveButton;
                toolbar.Add(saveButton);
            }
            {
                var centerButton = new UnityEditor.UIElements.ToolbarButton(() => {
                    this.graphView.ResetPositionAndZoom();
                });
                centerButton.text = "Center Graph";
                toolbar.Add(centerButton);
            }
            {
                var compileButton = new UnityEditor.UIElements.ToolbarButton(() => {
                    var graph = (Graph)this.graph;
                    var text = graph.Generate();
                    TextAsset blueprintSystem;
                    if (text.components.Count > 0) {
                        blueprintSystem = EditorUtils.LoadResource<TextAsset>("ME.BECS.Resources/Templates/Blueprint-SystemWithComponents.txt");
                    } else {
                        blueprintSystem = EditorUtils.LoadResource<TextAsset>("ME.BECS.Resources/Templates/Blueprint-System.txt");
                    }

                    var template = blueprintSystem.text;
                    var asm = EditorUtils.GetAssemblyInfo(this.graph);
                    var tpl = new Tpl(template);
                    var counters = new Dictionary<string, int>();
                    var variables = new Dictionary<string, string>();
                    var str = text.ToString();

                    var name = EditorUtils.GetCodeName(this.graph.name);
                    if (name.EndsWith("System") == false) name += "System";
                    variables.Add("LOGIC", EditorUtils.FormatCode(str.Split('\n', System.StringSplitOptions.RemoveEmptyEntries), defaultIndent: 4));
                    variables.Add("NAMESPACE", asm.name);
                    variables.Add("NAME", name);
                    variables.Add("PARALLEL", graph.systemType == Graph.SystemJobType.Parallel ? "Parallel" : string.Empty);
                    if (text.components.Count > 0) {
                        var components = new System.Collections.Generic.List<string>();
                        var componentsWithVars = new System.Collections.Generic.List<string>();
                        foreach (var kv in text.components) {
                            components.Add(kv.Key.FullName);
                            componentsWithVars.Add(kv.Key.FullName + " " + kv.Value.componentVariableName);
                        }

                        variables.Add("COMPONENTS", string.Join(", ", components));
                        variables.Add("COMPONENTS_WITH_VARS", "ref " + string.Join(", ref ", componentsWithVars));
                    }
                    var result = tpl.GetString(counters, variables);
                    //Debug.Log(result);
                    var path = $"{System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(graph))}/{name}.cs";
                    System.IO.File.WriteAllText(path, result);
                    AssetDatabase.ImportAsset(path);
                });
                compileButton.text = "Compile Graphs";
                toolbar.Add(compileButton);
            }
            
            this.rootView.Add(toolbar);
            
            this.UpdateToolbar();
            if (this.graphView != null) this.OnTransformChanged(view, true);
            
        }

        protected override void InitializeGraphView(BaseGraphView view) {
            
            this.rootView.styleSheets.Add(nodeStyleSheet);

            base.InitializeGraphView(view);
            
        }

    }

}