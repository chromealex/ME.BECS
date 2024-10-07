/*using ME.BECS.Editor.Extensions.SubclassSelector;
using UnityEditor.UIElements;

namespace ME.BECS.Blueprints.Editor {
    
    using UnityEditor;
    using UnityEngine;
    using ME.BECS.Editor;
    using UnityEngine.UIElements;

    public class BlueprintEditorWindow : EditorWindow {

        private Graph graph;

        public static void ShowWindow(Graph graph) {

            var win = BlueprintEditorWindow.CreateInstance<BlueprintEditorWindow>();
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

        protected void OnEnable() {
            
            UnityEditor.Selection.selectionChanged -= this.OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += this.OnSelectionChanged;

        }

        protected void OnDestroy() {
            
            UnityEditor.Selection.selectionChanged -= this.OnSelectionChanged;
            
        }

        private void OnSelectionChanged() {

            var graph = UnityEditor.Selection.activeObject as Graph;
            if (graph != null) {

                this.SelectAsset(graph);

            }

        }
        
        private void SelectAsset(Graph graph) {

            this.graph = graph;
            this.titleContent = new GUIContent(graph.name, this.titleContent.image);
            this.hasUnsavedChanges = false;
            
        }
        
        private static StyleSheet styleSheetBase;
        private static StyleSheet styleSheetTooltip;
        private static StyleSheet nodeStyleSheet;
        
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

        private void CreateGUI() {

            this.LoadStyle();
            
            var root = new VisualElement();
            root.styleSheets.Add(styleSheetBase);
            root.styleSheets.Add(styleSheetTooltip);
            root.styleSheets.Add(nodeStyleSheet);
            this.rootVisualElement.Add(root);

            var linesRoot = new VisualElement();
            root.Add(linesRoot);
            var so = new SerializedObject(this.graph);
            so.Update();
            var lines = so.FindProperty("lines");
            for (int i = 0; i < lines.arraySize; ++i) {

                var line = lines.GetArrayElementAtIndex(i);
                this.DrawLine(line.FindPropertyRelative("node"), linesRoot);

            }

            {
                // Add line button
                var button = new Button(() => {
                    ++lines.arraySize;
                    var lastLine = lines.GetArrayElementAtIndex(lines.arraySize - 1);
                    lastLine.FindPropertyRelative("node").managedReferenceValue = null;
                    this.DrawLine(lastLine, linesRoot);
                    so.ApplyModifiedProperties();
                });
                button.text = "Add Line";
                linesRoot.Add(button);
            }

        }

        public void DrawLine(SerializedProperty line, VisualElement root) {

            var item = new VisualElement();
            item.AddToClassList("line");
            var prop = new UnityEditor.UIElements.PropertyField(line);
            prop.BindProperty(line);
            item.Add(prop);
            root.Add(item);

        }

    }

}*/