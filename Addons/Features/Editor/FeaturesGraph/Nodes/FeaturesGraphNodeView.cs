using System.Reflection;

namespace ME.BECS.Editor.FeaturesGraph.Nodes {
    
    [ME.BECS.Extensions.GraphProcessor.NodeCustomEditor(typeof(ME.BECS.FeaturesGraph.Nodes.FeaturesGraphNode))]
    public class FeaturesGraphNodeView : ME.BECS.Extensions.GraphProcessor.BaseNodeView {

        private UnityEngine.UIElements.VisualElement container;

        public override void RedrawInspector(bool fromInspector = false) {
            
            this.container.Clear();
            this.Draw();

            base.RedrawInspector(fromInspector);
            
        }

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
                
                var container = new UnityEngine.UIElements.VisualElement();
                this.container = container;
                this.Add(this.container);

                this.Draw();

            }
            
            base.DrawDefaultInspector(fromInspector);
            
        }

        private void CollectDependencies(ref int iter, ME.BECS.Extensions.GraphProcessor.BaseNode root, System.Collections.Generic.HashSet<System.Type> results) {

            ++iter;
            if (iter == 10000) {
                UnityEngine.Debug.LogWarning("Max iterations while CollectDependencies");
                return;
            }
            
            if (root is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode && systemNode.system != null) {

                var type = systemNode.system.GetType();
                var dependenciesAttributes = type.GetCustomAttributes<RequiredDependenciesAttribute>();
                foreach (var dep in dependenciesAttributes) {
                    foreach (var depType in dep.types) {
                        var i = 0;
                        if (this.IsTypeContainsInGraph(ref i, depType, root.graph) == false) {
                            results.Add(depType);
                        }
                    }
                }

            } else if (root is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode && graphNode.graphValue != null) {

                foreach (var node in graphNode.graphValue.nodes) {

                    this.CollectDependencies(ref iter, node, results);

                }
                
            }
            
        }

        protected virtual void Draw() {

            var types = new System.Collections.Generic.HashSet<System.Type>();
            var iter = 0;
            this.CollectDependencies(ref iter, this.nodeTarget, types);
            
            if (types.Count > 0) {
                var isInnerGraph = this.nodeTarget.graph is ME.BECS.FeaturesGraph.SystemsGraph systemsGraph && systemsGraph.isInnerGraph;
                var requiredContainer = new UnityEngine.UIElements.VisualElement();
                var label = new UnityEngine.UIElements.Label("Dependencies:");
                label.AddToClassList("required-dependencies-header");
                requiredContainer.Add(label);
                requiredContainer.AddToClassList("required-dependencies");
                this.container.Add(requiredContainer);
                var hasFailed = false;
                foreach (var uniqueType in types) {
                    var typeStr = EditorUtils.GetComponentName(uniqueType);
                    var namespaceStr = EditorUtils.GetComponentNamespace(uniqueType);
                    var depContainer = new UnityEngine.UIElements.VisualElement();
                    depContainer.AddToClassList("required-dependencies-container");
                    iter = 0;
                    if (this.HasDependency(ref iter, this.nodeTarget, uniqueType) == true) {
                        depContainer.AddToClassList("node-required-dependency-checked");
                    } else {
                        hasFailed = true;
                    }
                    requiredContainer.Add(depContainer);

                    var checkbox = new UnityEngine.UIElements.VisualElement();
                    checkbox.AddToClassList("node-required-dependency-checkbox");

                    {
                        var lbl = new UnityEngine.UIElements.Label("\u2713");
                        lbl.AddToClassList("node-required-dependency-true");
                        checkbox.Add(lbl);
                    }
                    if (isInnerGraph == true) {
                        var lbl = new UnityEngine.UIElements.Label("\u26A0");
                        lbl.AddToClassList("node-required-dependency-warning");
                        checkbox.Add(lbl);
                    } else {
                        var lbl = new UnityEngine.UIElements.Label("✕");
                        lbl.AddToClassList("node-required-dependency-false");
                        checkbox.Add(lbl);
                    }
                    depContainer.Add(checkbox);

                    var typeContainer = new UnityEngine.UIElements.VisualElement();
                    depContainer.Add(typeContainer);

                    {
                        label = new UnityEngine.UIElements.Label($"{typeStr}");
                        label.AddToClassList("node-required-dependency-typename");
                        typeContainer.Add(label);
                    }

                    {
                        label = new UnityEngine.UIElements.Label($"{namespaceStr}");
                        label.AddToClassList("node-required-dependency-namespace");
                        typeContainer.Add(label);
                    }
                }

                if (hasFailed == true) {
                    if (isInnerGraph == true) {
                        requiredContainer.AddToClassList("dependencies-warning");
                    } else {
                        requiredContainer.AddToClassList("dependencies-failed");
                    }
                }
            }

        }

        private bool HasDependency(ref int iter, ME.BECS.Extensions.GraphProcessor.BaseNode node, System.Type type) {
            
            ++iter;
            if (iter == 10000) {
                UnityEngine.Debug.LogWarning("Max iterations while HasDependency");
                return false;
            }
            
            foreach (var port in node.inputPorts) {

                foreach (var edge in port.GetEdges()) {
                    var checkNode = edge.outputPort.owner;
                    if (checkNode is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode && graphNode.graphValue != null) {
                        var i = 0;
                        if (this.IsTypeContainsInGraph(ref i, type, graphNode.graphValue) == true) {
                            return true;
                        }
                    }

                    if (checkNode is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode && systemNode.system != null) {
                        if (systemNode.system.GetType() == type) return true;
                    }
                    
                    if (this.HasDependency(ref iter, checkNode, type) == true) return true;
                }
                
            }
            
            return false;
            
        }

        private bool IsTypeContainsInGraph(ref int iter, System.Type type, ME.BECS.Extensions.GraphProcessor.BaseGraph graph) {

            ++iter;
            if (iter == 10000) {
                UnityEngine.Debug.LogWarning("Max iterations while HasDependency");
                return false;
            }

            if (graph == null) {
                UnityEngine.Debug.LogError("graph is null: " + graph);
            }

            if (graph.nodes == null) {
                UnityEngine.Debug.LogError("graph.nodes is null in graph " + graph);
            }

            foreach (var node in graph.nodes) {

                if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode &&
                    systemNode.system != null) {

                    if (systemNode.system.GetType() == type) return true;

                } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode &&
                           graphNode.graphValue != null) {

                    if (this.IsTypeContainsInGraph(ref iter, type, graphNode.graphValue) == true) return true;

                }

            }

            return false;

        }

    }

}