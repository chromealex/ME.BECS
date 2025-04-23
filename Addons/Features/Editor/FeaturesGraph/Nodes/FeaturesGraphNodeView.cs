using System.Reflection;
using UnityEngine.UIElements;

namespace ME.BECS.Editor.FeaturesGraph.Nodes {
    
    [ME.BECS.Extensions.GraphProcessor.NodeCustomEditor(typeof(ME.BECS.FeaturesGraph.Nodes.FeaturesGraphNode))]
    public class FeaturesGraphNodeView : ME.BECS.Extensions.GraphProcessor.BaseNodeView {

        private UnityEngine.UIElements.VisualElement container;

        protected override void UpdateSync(UnityEngine.UIElements.VisualElement container) {
            
            base.UpdateSync(container);

            if (this.nodeTarget is ME.BECS.FeaturesGraph.Nodes.GraphNode ||
                this.nodeTarget is ME.BECS.FeaturesGraph.Nodes.SystemNode) {

                var syncLabels = container.Q(className: "sync-labels");
                if (syncLabels == null) {

                    syncLabels = new UnityEngine.UIElements.VisualElement();
                    syncLabels.AddToClassList("sync-labels");
                    container.Add(syncLabels);

                }
                
                Run(Method.Awake);
                Run(Method.Start);
                Run(Method.Update);
                Run(Method.Destroy);
                Run(Method.DrawGizmos);

                void Run(Method method) {

                    this.nodeTarget.ValidateSyncPoints();
                    var syncPointData = this.nodeTarget.GetSyncPoint(method);
                    if (syncPointData.hasMethod == false) return;
                    
                    var syncLabel = syncLabels.Q(className: "sync-label-" + method.ToString()) as Label;
                    if (syncLabel == null) {

                        syncLabel = new UnityEngine.UIElements.Label();
                        syncLabel.AddToClassList("sync-label");
                        syncLabel.AddToClassList("sync-label-" + method.ToString());
                        syncLabel.tooltip = method.ToString();
                        syncLabels.Add(syncLabel);

                    }

                    {
                        if (syncPointData.syncPoint == true) {
                            syncLabel.text = method.ToString();
                        } else {
                            syncLabel.text = $"{method.ToString()}: {syncPointData.syncCount}";
                        }
                    }

                }

            }
            
        }

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
                        if (this.nodeTarget.graph.isInnerGraph == true || root.graph == null || this.IsTypeContainsInGraph(ref i, depType, root.graph) == false) {
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
            
            if (this.nodeTarget is ME.BECS.FeaturesGraph.Nodes.SystemNode node && node.system != null) {
                var type = System.Type.GetType("ME.BECS.Editor.StaticMethods, ME.BECS.Gen.Editor");
                if (type != null) {
                    {
                        var errors = (System.Collections.Generic.List<ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error>)type
                            .GetMethod("GetSystemDependenciesErrors").Invoke(null, new object[] { node.system.GetType() });
                        if (errors.Count > 0) {
                            var container = new VisualElement();
                            container.AddToClassList("errors");
                            foreach (var err in errors) {
                                var lbl = new Label("<color=red>\u26A0</color> " + err.message);
                                container.Add(lbl);
                            }

                            this.container.Add(container);
                        }
                    }

                    var list = (System.Collections.Generic.List<ComponentDependencyGraphInfo>)type.GetMethod("GetSystemComponentsDependencies")
                                                                                                  .Invoke(null, new object[] { node.system.GetType() });
                    var requiredContainer = new Foldout();
                    requiredContainer.value = UnityEditor.EditorPrefs.GetBool($"Foldouts.graphs.{node.system.GetType().FullName}");
                    requiredContainer.RegisterValueChangedCallback(evt => { UnityEditor.EditorPrefs.SetBool($"Foldouts.graphs.{node.system.GetType().FullName}", evt.newValue); });
                    requiredContainer.AddToClassList("required-dependencies");
                    this.container.Add(requiredContainer);
                    var ro = 0;
                    var wo = 0;
                    var rw = 0;
                    foreach (var item in list) {
                        var op = (RefOp)item.op;
                        var typeStr = EditorUtils.GetComponentName(item.type);
                        var namespaceStr = EditorUtils.GetComponentNamespace(item.type);
                        var depContainer = new UnityEngine.UIElements.VisualElement();
                        depContainer.AddToClassList("required-dependencies-container");
                        requiredContainer.Add(depContainer);
                        var checkbox = new UnityEngine.UIElements.VisualElement();
                        checkbox.AddToClassList("node-required-dependency-checkbox");

                        if (op == RefOp.ReadOnly) {
                            ++ro;
                            checkbox.AddToClassList("node-required-dependency-checked");
                            var lbl = new UnityEngine.UIElements.Label("RO");
                            lbl.AddToClassList("node-required-dependency-true");
                            checkbox.Add(lbl);
                        } else if (op == RefOp.ReadWrite) {
                            ++rw;
                            var lbl = new UnityEngine.UIElements.Label("RW");
                            lbl.AddToClassList("node-required-dependency-warning");
                            checkbox.Add(lbl);
                        } else if (op == RefOp.WriteOnly) {
                            ++wo;
                            var lbl = new UnityEngine.UIElements.Label("WO");
                            lbl.AddToClassList("node-required-dependency-warning");
                            checkbox.Add(lbl);
                        }

                        depContainer.Add(checkbox);

                        var typeContainer = new UnityEngine.UIElements.VisualElement();
                        depContainer.Add(typeContainer);

                        {
                            var label = new UnityEngine.UIElements.Label($"{typeStr}");
                            label.AddToClassList("node-required-dependency-typename");
                            typeContainer.Add(label);
                        }

                        {
                            var label = new UnityEngine.UIElements.Label($"{namespaceStr}");
                            label.AddToClassList("node-required-dependency-namespace");
                            typeContainer.Add(label);
                        }
                    }

                    requiredContainer.text = "Components Usage (RO: " + ro + ", RW: " + rw + ", WO: " + wo + ")";
                }
            }
            
            if (types.Count > 0) {
                var isInnerGraph = this.nodeTarget.graph is ME.BECS.FeaturesGraph.SystemsGraph systemsGraph && systemsGraph.isInnerGraph;
                var requiredContainer = new UnityEngine.UIElements.VisualElement();
                var label = new UnityEngine.UIElements.Label("Dependencies:");
                label.AddToClassList("required-dependencies-header");
                requiredContainer.Add(label);
                requiredContainer.AddToClassList("required-dependencies");
                this.container.Add(requiredContainer);
                var hasWarning = false;
                var hasFailed = false;
                foreach (var uniqueType in types) {
                    var typeStr = EditorUtils.GetComponentName(uniqueType);
                    var namespaceStr = EditorUtils.GetComponentNamespace(uniqueType);
                    var depContainer = new UnityEngine.UIElements.VisualElement();
                    depContainer.AddToClassList("required-dependencies-container");
                    iter = 0;
                    var localState = 0;
                    if (this.HasDependency(ref iter, this.nodeTarget, uniqueType) == true) {
                        depContainer.AddToClassList("node-required-dependency-checked");
                        localState = 0;
                    } else {
                        var i = 0;
                        if (isInnerGraph == false || this.IsTypeContainsInGraph(ref i, uniqueType, this.nodeTarget.graph) == true) {
                            depContainer.AddToClassList("node-required-dependency-failed");
                            depContainer.tooltip = "Dependency required by this system because it is contained in current graph or this graph is root!";
                            localState = 1;
                            hasFailed = true;
                        } else {
                            depContainer.AddToClassList("node-required-dependency-warning");
                            depContainer.tooltip = "Dependency required by this system, so this graph must be connected at the upper level to required system!";
                            localState = 2;
                            hasWarning = true;
                        }
                    }
                    requiredContainer.Add(depContainer);

                    var checkbox = new UnityEngine.UIElements.VisualElement();
                    checkbox.AddToClassList("node-required-dependency-checkbox");

                    {
                        var lbl = new UnityEngine.UIElements.Label("\u2713");
                        lbl.AddToClassList("node-required-dependency-true");
                        checkbox.Add(lbl);
                    }
                    if (localState == 2) {
                        var lbl = new UnityEngine.UIElements.Label("\u26A0");
                        lbl.AddToClassList("node-required-dependency-warning");
                        checkbox.Add(lbl);
                    } else if (localState == 1) {
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

                if (hasFailed == true || hasWarning == true) {
                    if (hasFailed == false) {
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