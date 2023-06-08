using System.Reflection;

namespace ME.BECS.Editor.FeaturesGraph.Nodes {
    
    using UnityEditor;
    
    [ME.BECS.Extensions.GraphProcessor.NodeCustomEditor(typeof(ME.BECS.FeaturesGraph.Nodes.SystemNode))]
    public class FeaturesGraphSystemNodeView : FeaturesGraphNodeView {

        protected override void DrawDefaultInspector(bool fromInspector = false) {

            if (fromInspector == false) {

                if (this.nodeTarget is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode &&
                    systemNode.system != null) {

                    var type = systemNode.system.GetType();
                    var tooltip = type.GetCustomAttribute<UnityEngine.TooltipAttribute>();
                    if (tooltip != null) {

                        var typeStr = EditorUtils.GetComponentName(type);
                        var label = new UnityEngine.UIElements.Label($"<b>{typeStr}</b>\n{tooltip.tooltip}");
                        label.AddToClassList("node-tooltip");
                        this.Add(label);

                    }

                }

            }
            
            base.DrawDefaultInspector(fromInspector);
            
        }

        protected override void CreateLabels(UnityEngine.UIElements.VisualElement container) {

            var node = this.nodeTarget as ME.BECS.FeaturesGraph.Nodes.SystemNode;
            if (node?.system != null) {
                var isBurst = node.system.GetType().GetCustomAttribute<Unity.Burst.BurstCompileAttribute>();
                if (isBurst != null) {
                    var burstLabel = new UnityEngine.UIElements.Label("Burst");
                    burstLabel.tooltip = "System run with Burst Compiler";
                    burstLabel.AddToClassList("burst-label");
                    container.Add(burstLabel);
                }
            }
            
        }

        public override void BuildContextualMenu(UnityEngine.UIElements.ContextualMenuPopulateEvent evt) {
            
            var system = (this.nodeTarget as ME.BECS.FeaturesGraph.Nodes.SystemNode).system;
            if (system != null) {
                evt.menu.AppendAction($"Open Script {system.GetType().Name}...", (e) => OpenScript(), this.OpenScriptStatus);
            }
            
        }

        private UnityEngine.UIElements.DropdownMenuAction.Status OpenScriptStatus(UnityEngine.UIElements.DropdownMenuAction arg) {

            var system = (this.nodeTarget as ME.BECS.FeaturesGraph.Nodes.SystemNode).system;
            if (system != null) {
                var script = FindScriptFromClassName(system.GetType().Name);
                if (script != null) return UnityEngine.UIElements.DropdownMenuAction.Status.Normal;
            }
            
            return UnityEngine.UIElements.DropdownMenuAction.Status.Disabled;

        }

        private void OpenScript() {
            
            var system = (this.nodeTarget as ME.BECS.FeaturesGraph.Nodes.SystemNode).system;
            if (system != null) {
                var script = FindScriptFromClassName(system.GetType().Name);
                if (script != null) AssetDatabase.OpenAsset(script.GetInstanceID(), 0, 0);
            }
            
        }
        
        static MonoScript FindScriptFromClassName(string className)
        {
            var scriptGUIDs = AssetDatabase.FindAssets($"t:script {className}");

            if (scriptGUIDs.Length == 0)
                return null;

            foreach (var scriptGUID in scriptGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(scriptGUID);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);

                if (script != null && string.Equals(className, System.IO.Path.GetFileNameWithoutExtension(assetPath), System.StringComparison.OrdinalIgnoreCase))
                    return script;
            }

            return null;
        }

    }

}