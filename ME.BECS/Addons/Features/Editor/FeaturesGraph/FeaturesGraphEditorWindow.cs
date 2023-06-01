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
            
            base.OnDestroy();
            
        }

        private void OnSelectionChanged() {

            var graph = UnityEditor.Selection.activeObject as BaseGraph;
            if (graph != null) {

                this.SelectAsset(graph);

            }

        }

        private void SelectAsset(BaseGraph graph) {
            
            this.titleContent = new GUIContent(graph.name, this.titleContent.image);
            this.graph = graph;
            this.graph.InitializeValidation();
            this.graph.onGraphChanges += this.OnGraphChanged;
            this.hasUnsavedChanges = false;
            this.graphView = null;
            this.rootView.Clear();
            this.InitializeGraph(this.graph);
            
        }

        private void OnGraphChanged(GraphChanges obj) {
            this.hasUnsavedChanges = true;
            this.UpdateToolbar();
        }

        private void UpdateToolbar() {
            this.saveButton.SetEnabled(true);//this.hasUnsavedChanges);
        }

        private Vector3 prevScale;
        private Vector3 prevPos;
        private void OnTransformChanged(UnityEditor.Experimental.GraphView.GraphView graphview) {

            if (this.prevScale == this.graphView.viewTransform.scale &&
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
            
        }

        private UnityEngine.UIElements.Image background;
        private UnityEditor.UIElements.Toolbar toolbar;
        private UnityEngine.UIElements.Button saveButton;
        private const float maxOpacity = 0.2f;
        protected override void InitializeWindow(BaseGraph graph) {
            var view = new FeaturesGraphView(this);
            view.RegisterCallback<MouseMoveEvent>((evt) => {
                this.OnTransformChanged(view);
            });
            this.wantsMouseMove = true;
            view.viewTransformChanged += this.OnTransformChanged;
            this.rootView.Add(view);
            {
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
            this.rootView.Add(toolbar);
            
            this.UpdateToolbar();
            if (this.graphView != null) this.OnTransformChanged(view);
        }

    }

}