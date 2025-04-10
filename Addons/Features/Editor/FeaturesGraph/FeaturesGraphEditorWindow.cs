using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ME.BECS.Extensions.GraphProcessor;
using UnityEngine.UIElements;

namespace ME.BECS.Editor.FeaturesGraph {

    public class FeaturesGraphEditorWindow : BaseGraphWindow {

        [System.Serializable]
        public class BreadcrumbItem : System.IEquatable<BreadcrumbItem> {

            public string label;
            public BaseGraph graph;

            public bool Equals(BreadcrumbItem other) {
                if (other is null) {
                    return false;
                }

                if (ReferenceEquals(this, other)) {
                    return true;
                }

                return this.label == other.label && Equals(this.graph, other.graph);
            }

            public override bool Equals(object obj) {
                if (obj is null) {
                    return false;
                }

                if (ReferenceEquals(this, obj)) {
                    return true;
                }

                if (obj.GetType() != this.GetType()) {
                    return false;
                }

                return this.Equals((BreadcrumbItem)obj);
            }

            public override int GetHashCode() {
                return System.HashCode.Combine(this.label, this.graph);
            }

        }
        
        public System.Collections.Generic.List<BreadcrumbItem> breadcrumbs = new System.Collections.Generic.List<BreadcrumbItem>();

        private void MoveTo(BaseGraph graph) {

            var index = -1;
            for (int i = 0; i < this.breadcrumbs.Count; ++i) {
                if (this.breadcrumbs[i].graph == graph) {
                    index = i;
                    break;
                }
            }

            if (index >= 0) {
                this.breadcrumbs.RemoveRange(index, this.breadcrumbs.Count - index);
            }
            
            this.OnOpen(graph);
            
        }

        public static void ShowWindow(BaseGraph graph = null) {

            var win = FeaturesGraphEditorWindow.GetWindow<FeaturesGraphEditorWindow>();
            win.titleContent = new GUIContent("Features Graph", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-featuresgraph.png"));
            if (graph != null) {
                win.OnOpen(graph);
            } else {
                win.OnSelectionChanged();
            }
            win.Show();

        }

        [UnityEditor.Callbacks.OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line) {
            var project = UnityEditor.EditorUtility.InstanceIDToObject(instanceID) as ME.BECS.FeaturesGraph.SystemsGraph;
            if (project != null) {
                FeaturesGraphEditorWindow.ShowWindow(project);
                return true;
            }
            return false;
        }

        protected override void OnEnable() {
            
            base.OnEnable();

            UnityEditor.Selection.selectionChanged -= this.OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += this.OnSelectionChanged;
            
            if (this.graphView is FeaturesGraphView view) view.UpdateEnableState();
            
        }

        protected override void OnDestroy() {
            
            ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.onOpen -= this.OnOpen;
            UnityEditor.Selection.selectionChanged -= this.OnSelectionChanged;
            if (this.graph != null) { 
                this.graph.onGraphChanges -= this.OnGraphChanged;
            }
            base.OnDestroy();
            
        }

        private void OnSelectionChanged() {

            var graph = UnityEditor.Selection.activeObject as ME.BECS.FeaturesGraph.SystemsGraph;
            if (graph != null) {

                this.breadcrumbs.Clear();
                this.OnOpen(graph);

            }

        }

        private void SelectAsset(BaseGraph graph) {

            if (this.breadcrumbs.Count == 0) {
                this.OnOpen(graph);
                return;
            }
            
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

        private void OnGraphChanged(GraphChanges obj) {
        
            //Debug.Log("Dirty");
            this.hasUnsavedChanges = true;
            this.UpdateToolbar();
        }

        private void UpdateToolbar() {
            this.saveButton.SetEnabled(true);//this.hasUnsavedChanges);

            if (this.graphView != null) {
                if (contextMenu == null) contextMenu = new ContextualMenuManipulator(this.graphView.BuildContextualMenu);
                this.graphView.RemoveManipulator(contextMenu);
                this.graphView.AddManipulator(contextMenu);
            }
        }

        protected override void Update() {
            
            base.Update();
            
            if (this.background != null) this.background.MarkDirtyRepaint();
            
        }

        private Vector3 prevScale;
        private Vector3 prevPos;

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
            
            if (this.graphView != null) {
                if (contextMenu == null) contextMenu = new ContextualMenuManipulator(this.graphView.BuildContextualMenu);
                this.graphView.RemoveManipulator(contextMenu);
                this.graphView.AddManipulator(contextMenu);
            }
            
            this.OnScaleChanged();
            if (this.graphView is FeaturesGraphView view) view.UpdateEnableState();

        }

        private static IManipulator contextMenu;

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (styleSheetBase == null) {
                styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/FeaturesGraphEditorWindow.uss");
            }
        }
        
        private void OnScaleChanged() {
            
            var scaleX = this.graphView.viewTransform.scale.x;
            var op = Mathf.Lerp(0f, maxOpacity, Mathf.Clamp01(scaleX - 0.25f) * 2f);
            this.background.opacity = op;

        }

        private void OnFocus() {
            if (this.graphView is FeaturesGraphView view) view.UpdateEnableState();
        }

        protected override void InitializeGraphView(BaseGraphView viewBase) {
            base.InitializeGraphView(viewBase);
            if (viewBase is FeaturesGraphView view) view.UpdateEnableState();
        }

        private GridBackground background;
        private UnityEditor.UIElements.ToolbarBreadcrumbs breadcrumb;
        private UnityEditor.UIElements.Toolbar toolbar;
        private UnityEngine.UIElements.Button saveButton;
        private const float maxOpacity = 1f;
        protected override void InitializeWindow(BaseGraph graph) {
            
            var view = new FeaturesGraphView(this);
            view.isEditable = graph.builtInGraph == false;
            view.RegisterCallback<MouseMoveEvent>((evt) => {
                this.OnTransformChanged(view);
            });
            this.wantsMouseMove = true;
            var grid = new GridBackground();
            this.background = grid;
            view.Add(grid);
            grid.SendToBack();
            
            view.viewTransformChanged += this.OnTransformChanged;
            this.rootView.Add(view);
            this.LoadStyle();
            view.styleSheets.Add(styleSheetBase);

            if (view.isEditable == false) {
                var editableLock = new Image();
                editableLock.AddToClassList("editable-lock");
                editableLock.image = EditorUtils.LoadResource<Texture2D>("editable-lock", false);
                {
                    var label = new Label("Readonly Mode");
                    label.AddToClassList("editable-header");
                    editableLock.Add(label);
                }
                {
                    var label = new Label("Edit mode is disabled because of this graph is built-in");
                    label.AddToClassList("editable-description");
                    editableLock.Add(label);
                }
                view.Add(editableLock);
            }

            ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.onOpen -= this.OnOpen;
            ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.onOpen += this.OnOpen;
            
            if (this.toolbar != null && this.rootView.Contains(this.toolbar) == true) this.rootView.Remove(this.toolbar);
            var toolbar = new UnityEditor.UIElements.Toolbar();
            this.toolbar = toolbar;
            {
                var saveButton = new UnityEditor.UIElements.ToolbarButton(() => {
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
                    CodeGenerator.RegenerateBurstAOT();
                });
                compileButton.text = "Compile Graphs";
                toolbar.Add(compileButton);
            }
            this.rootView.Add(toolbar);
            
            this.UpdateToolbar();
            if (this.graphView != null) this.OnTransformChanged(view, true);

            if (this.breadcrumb != null && this.rootView.Contains(this.breadcrumb) == true) this.rootView.Remove(this.breadcrumb);
            this.breadcrumb = new UnityEditor.UIElements.ToolbarBreadcrumbs();
            this.rootView.Add(this.breadcrumb);

            this.UpdateBreadcrumbs();
            view.UpdateEnableState();

        }

        private void UpdateBreadcrumbs() {
            
            this.breadcrumb.Clear();
            foreach (var item in this.breadcrumbs) {
                this.breadcrumb.PushItem(item.label, () => {
                    this.MoveTo(item.graph);
                });
            }
            
        }

        private void OnOpen(Object asset) {

            if (asset is ME.BECS.FeaturesGraph.SystemsGraph graph) {
                var item = new BreadcrumbItem() {
                    label = graph.name,
                    graph = graph,
                };
                if (this.breadcrumbs.Contains(item) == false) this.breadcrumbs.Add(item);
                this.SelectAsset(graph);
            }
            
        }

    }

}