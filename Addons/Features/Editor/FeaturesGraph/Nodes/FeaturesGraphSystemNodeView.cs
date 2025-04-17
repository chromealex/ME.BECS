using System.Reflection;
using UnityEngine.UIElements;

namespace ME.BECS.Editor.FeaturesGraph.Nodes {
    
    using UnityEditor;
    
    [ME.BECS.Extensions.GraphProcessor.NodeCustomEditor(typeof(ME.BECS.FeaturesGraph.Nodes.SystemNode))]
    public class FeaturesGraphSystemNodeView : FeaturesGraphNodeView {

        private static void AddLabel(UnityEngine.UIElements.VisualElement container, string label, string tooltip, bool burst) {
            
            var burstLabel = new UnityEngine.UIElements.Label(label);
            burstLabel.tooltip = tooltip;
            burstLabel.AddToClassList(burst == true ? "burst-label" : "no-burst-label");
            container.Add(burstLabel);
                        
        }

        private static bool IsBurstMethod(ISystem system, System.Type interfaceType, bool systemBurst, out bool hasInterface) {
            var isBurst = systemBurst;
            hasInterface = false;
            if (interfaceType.IsInstanceOfType(system) == true) {
                hasInterface = true;
                var map = system.GetType().GetInterfaceMap(interfaceType);
                var method = map.TargetMethods[0];
                {
                    var withBurst = method.GetCustomAttribute<Unity.Burst.BurstCompileAttribute>() != null;
                    if (withBurst == true) {
                        isBurst = true;
                    } else {
                        var noBurst = method.GetCustomAttribute<WithoutBurstAttribute>() != null;
                        if (noBurst == true) {
                            isBurst = false;
                        }
                    }
                }
            }

            return isBurst;
        }

        protected override void CreateLabels(UnityEngine.UIElements.VisualElement container) {

            var node = this.nodeTarget as ME.BECS.FeaturesGraph.Nodes.SystemNode;
            if (node?.system != null) {
                var isBurst = node.system.GetType().GetCustomAttribute<Unity.Burst.BurstCompileAttribute>() != null;
                var awakeBurst = IsBurstMethod(node.system, typeof(IAwake), isBurst, out var hasAwake);
                var updateBurst = IsBurstMethod(node.system, typeof(IUpdate), isBurst, out var hasUpdate);
                var disposeBurst = IsBurstMethod(node.system, typeof(IDestroy), isBurst, out var hasDestroy);
                var drawGizmosBurst = IsBurstMethod(node.system, typeof(IDrawGizmos), isBurst, out var hasDrawGizmos);

                if (node.system.GetType().IsGenericType == true) {
                    if (node.system.GetType().GetCustomAttribute<SystemGenericParallelModeAttribute>() != null) {
                        AddLabel(container, "Parallel", "Systems will run in parallel mode", true);
                    } else {
                        AddLabel(container, "One-by-one", "Systems will run one-by-one", true);
                    }
                }
                
                if (awakeBurst == true && updateBurst == true && disposeBurst == true && isBurst == true) {
                    AddLabel(container, "Burst", "System run with Burst Compiler", true);
                } else if (isBurst == false && awakeBurst == false && updateBurst == false && disposeBurst == false) {
                    AddLabel(container, "No Burst", "System run without Burst Compiler", false);
                } else {
                    if (hasAwake == true) {
                        if (awakeBurst == true) {
                            AddLabel(container, "Awake Burst", "Method run with Burst Compiler", true);
                        } else {
                            AddLabel(container, "Awake No Burst", "Method run without Burst Compiler", false);
                        }
                    }

                    if (hasUpdate == true) {
                        if (updateBurst == true) {
                            AddLabel(container, "Update Burst", "Method run with Burst Compiler", true);
                        } else {
                            AddLabel(container, "Update No Burst", "Method run without Burst Compiler", false);
                        }
                    }

                    if (hasDestroy == true) {
                        if (disposeBurst == true) {
                            AddLabel(container, "Destroy Burst", "Method run with Burst Compiler", true);
                        } else {
                            AddLabel(container, "Destroy No Burst", "Method run without Burst Compiler", false);
                        }
                    }
                    
                    if (hasDrawGizmos == true) {
                        if (drawGizmosBurst == true) {
                            AddLabel(container, "Draw Gizmos Burst", "Method run with Burst Compiler", true);
                        } else {
                            AddLabel(container, "Draw Gizmos No Burst", "Method run without Burst Compiler", false);
                        }
                    }
                }
            }
            
        }

        public override void BuildContextualMenu(UnityEngine.UIElements.ContextualMenuPopulateEvent evt) {
            
            var system = (this.nodeTarget as ME.BECS.FeaturesGraph.Nodes.SystemNode)?.system;
            if (system != null) {
                evt.menu.AppendAction($"Open Script {system.GetType().Name}...", (e) => this.OpenScript(), this.OpenScriptStatus);
            }
            
        }

        private UnityEngine.UIElements.DropdownMenuAction.Status OpenScriptStatus(UnityEngine.UIElements.DropdownMenuAction arg) {

            var system = (this.nodeTarget as ME.BECS.FeaturesGraph.Nodes.SystemNode)?.system;
            if (system != null) {
                var script = FindScriptFromClassName(system.GetType().Name, system.GetType().Namespace);
                if (script != null) return UnityEngine.UIElements.DropdownMenuAction.Status.Normal;
            }
            
            return UnityEngine.UIElements.DropdownMenuAction.Status.Disabled;

        }

        private void OpenScript() {
            
            var system = (this.nodeTarget as ME.BECS.FeaturesGraph.Nodes.SystemNode)?.system;
            if (system != null) {
                var script = FindScriptFromClassName(system.GetType().Name, system.GetType().Namespace);
                if (script != null) AssetDatabase.OpenAsset(script.GetInstanceID(), 0, 0);
            }
            
        }
        
        private static MonoScript FindScriptFromClassName(string className, string @namespace) {
            
            var scriptGUIDs = AssetDatabase.FindAssets($"t:script {className}");
            if (scriptGUIDs.Length == 0) return null;

            foreach (var scriptGUID in scriptGUIDs) {
                var assetPath = AssetDatabase.GUIDToAssetPath(scriptGUID);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);

                if (script != null && string.Equals(className, System.IO.Path.GetFileNameWithoutExtension(assetPath), System.StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(@namespace) == true || System.Text.RegularExpressions.Regex.IsMatch(script.text, @$"namespace\s+{@namespace}", System.Text.RegularExpressions.RegexOptions.Singleline) == true))
                    return script;
            }

            return null;
        }

    }

}