using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ME.BECS.Extensions.GraphProcessor;
using UnityEngine.UIElements;

namespace ME.BECS.Editor.FeaturesGraph {

    public class FeaturesGraphEditorWindow : BaseGraphWindow {

        public static void ShowWindow(BaseGraph graph = null) {

            var win = FeaturesGraphEditorWindow.GetWindow<FeaturesGraphEditorWindow>();
            win.titleContent = new GUIContent("Features Graph", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-featuresgraph.png"));
            if (graph != null) {
                win.SelectAsset(graph);
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

        }

        protected override void OnDestroy() {
            
            UnityEditor.Selection.selectionChanged -= this.OnSelectionChanged;
            if (this.graph != null) { 
                this.graph.onGraphChanges -= this.OnGraphChanged;
            }
            base.OnDestroy();
            
        }

        private void OnSelectionChanged() {

            var graph = UnityEditor.Selection.activeObject as BaseGraph;
            if (graph != null) {

                this.SelectAsset(graph);

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

        private void OnGraphChanged(GraphChanges obj) {
        
            //Debug.Log("Dirty");
            this.hasUnsavedChanges = true;
            this.UpdateToolbar();
        }

        private void UpdateToolbar() {
            this.saveButton.SetEnabled(true);//this.hasUnsavedChanges);
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
            
        }

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (styleSheetBase == null) {
                styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/FeaturesGraphEditorWindow.uss");
            }
        }
        
        private UnityEngine.UIElements.VisualElement background;
        private UnityEditor.UIElements.Toolbar toolbar;
        private UnityEngine.UIElements.Button saveButton;
        private const float maxOpacity = 1f;
        protected override void InitializeWindow(BaseGraph graph) {
            var view = new FeaturesGraphView(this);
            view.RegisterCallback<MouseMoveEvent>((evt) => {
                this.OnTransformChanged(view);
            });
            this.wantsMouseMove = true;
            view.viewTransformChanged += this.OnTransformChanged;
            this.rootView.Add(view);
            this.LoadStyle();
            view.styleSheets.Add(styleSheetBase);
            /*{
                var vg = new Image();
                vg.image = EditorUtils.LoadResource<Texture>("ME.BECS.Resources/Icons/vignette.png");
                vg.scaleMode = ScaleMode.StretchToFill;
                vg.AddToClassList("header-vignette");
                vg.focusable = false;
                vg.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
                view.Add(vg);
                vg.SendToBack();
            }
            {
                var icon = new Image();
                icon.style.backgroundImage = new StyleBackground(EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/logo-back.png"));
                icon.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat));
                icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(512f, 512f));
                this.background = icon;
                icon.AddToClassList("header-icon");
                icon.focusable = false;
                icon.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
                view.Add(icon);
                icon.SendToBack();
            }*/
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
        }

    }

}