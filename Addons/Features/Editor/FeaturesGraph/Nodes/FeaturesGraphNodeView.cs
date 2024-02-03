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

                var container = new UnityEngine.UIElements.VisualElement();
                this.container = container;
                this.Add(this.container);

                this.Draw();

            }
            
            base.DrawDefaultInspector(fromInspector);
            
        }

        protected virtual void Draw() {

            var types = new System.Collections.Generic.HashSet<System.Type>();
            if (this.nodeTarget is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode &&
                systemNode.system != null) {

                var type = systemNode.system.GetType();
                var tooltip = type.GetCustomAttribute<UnityEngine.TooltipAttribute>();
                if (tooltip != null) {

                    var typeStr = EditorUtils.GetComponentName(type);
                    var label = new UnityEngine.UIElements.Label($"<b>{typeStr}</b>\n{tooltip.tooltip}");
                    label.AddToClassList("node-tooltip");
                    this.container.Add(label);

                }
                    
                var dependenciesAttributes = type.GetCustomAttributes<RequiredDependenciesAttribute>();
                foreach (var dep in dependenciesAttributes) {
                    foreach (var depType in dep.types) types.Add(depType);
                }

            } else if (this.nodeTarget is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode &&
                       graphNode.graphValue != null) {

                foreach (var node in graphNode.graphValue.nodes) {

                    if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode sysNode &&
                        sysNode.system != null) {

                        var type = sysNode.system.GetType();

                        var dependenciesAttributes = type.GetCustomAttributes<RequiredDependenciesAttribute>();
                        foreach (var dep in dependenciesAttributes) {
                            foreach (var depType in dep.types) {
                                if (this.IsTypeContainsInGraph(depType, graphNode.graphValue) == false) {
                                    types.Add(depType);
                                }
                            }
                        }

                    }

                }
                
            }
            
            if (types.Count > 0) {
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
                    if (this.HasDependency(this.nodeTarget, uniqueType) == true) {
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
                    {
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
                if (hasFailed == true) requiredContainer.AddToClassList("dependencies-failed");
            }

        }

        private bool HasDependency(ME.BECS.Extensions.GraphProcessor.BaseNode node, System.Type type) {

            foreach (var port in node.inputPorts) {

                foreach (var edge in port.GetEdges()) {
                    var checkNode = edge.outputPort.owner;
                    if (checkNode is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode && graphNode.graphValue != null) {
                        if (this.IsTypeContainsInGraph(type, graphNode.graphValue) == true) {
                            return true;
                        }
                    }

                    if (checkNode is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode && systemNode.system != null) {
                        if (systemNode.system.GetType() == type) return true;
                    }
                    
                    if (this.HasDependency(checkNode, type) == true) return true;
                }
                
            }
            
            return false;
            
        }

        private bool IsTypeContainsInGraph(System.Type type, ME.BECS.FeaturesGraph.SystemsGraph graph) {

            foreach (var node in graph.nodes) {

                if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode &&
                    systemNode.system != null) {

                    if (systemNode.system.GetType() == type) return true;

                }

            }

            return false;

        }

    }

}