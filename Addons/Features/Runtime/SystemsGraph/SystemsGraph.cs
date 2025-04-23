using System.Linq;
using UnityEngine;

namespace ME.BECS.FeaturesGraph {

    [CreateAssetMenu(menuName = "ME.BECS/Features Graph")]
    public class SystemsGraph : Extensions.GraphProcessor.BaseGraph {

        public SystemGroup runtimeRootSystemGroup;

        public class Graph {

            public class Node {

                public ME.BECS.Extensions.GraphProcessor.BaseNode node;
                public System.Collections.Generic.HashSet<Node> input = new System.Collections.Generic.HashSet<Node>();
                public System.Collections.Generic.HashSet<Node> output = new System.Collections.Generic.HashSet<Node>();

            }

            public Node startNode;
            public System.Collections.Generic.Dictionary<ME.BECS.Extensions.GraphProcessor.BaseNode, Node> nodes = new System.Collections.Generic.Dictionary<ME.BECS.Extensions.GraphProcessor.BaseNode, Node>();
            
            public Graph(ME.BECS.Extensions.GraphProcessor.BaseNode startNode, System.Func<ME.BECS.Extensions.GraphProcessor.BaseNode, bool> filter) {
                
                this.startNode = new Node() { node = startNode };
                var root = this.startNode;
                this.nodes.Add(root.node, this.startNode);

                var removeNodes = new System.Collections.Generic.HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode>();
                var max = 10_000;
                var q = new System.Collections.Generic.Queue<Node>();
                q.Enqueue(root);
                while (q.Count > 0) {

                    if (--max == 0) {
                        Debug.LogError("max iter");
                        break;
                    }
                    
                    var current = q.Dequeue();
                    var list = current.node.GetOutputNodes().ToList();
                    for (var index = 0; index < list.Count; ++index) {
                        var node = list[index];
                        if (filter.Invoke(node) == false && node is not ME.BECS.FeaturesGraph.Nodes.ExitNode) {
                            removeNodes.Add(node);
                        }
                        //if (filter.Invoke(node) == true || node is ME.BECS.FeaturesGraph.Nodes.ExitNode) {
                            if (this.nodes.TryGetValue(node, out var n) == false) {
                                n = new Node() { node = node };
                                this.nodes.Add(node, n);
                            }

                            n.input.Add(current);
                            current.output.Add(n);
                            q.Enqueue(n);
                        //} else {
                        //    removeNodes.Add(node);
                            // Connect all inputs with all outputs
                            /*foreach (var input in node.GetInputNodes()) {
                                if (this.nodes.TryGetValue(input, out var n) == false) {
                                    n = new Node() { node = input };
                                    this.nodes.Add(input, n);
                                }

                                foreach (var output in node.GetOutputNodes()) {
                                    if (this.nodes.TryGetValue(output, out var n2) == false) {
                                        n2 = new Node() { node = output };
                                        this.nodes.Add(output, n2);
                                    }

                                    n.output.Add(n2);
                                    n2.input.Add(n);
                                    list.Add(n2.node);
                                }
                            }*/
                        //}
                    }
                }

                if (removeNodes.Count > 0) {
                    q.Clear();
                    var n = this.startNode;
                    q.Enqueue(n);
                    while (q.Count > 0) {
                        var node = q.Dequeue();
                        if (removeNodes.Contains(node.node) == true) {
                            // Connect all inputs with all outputs
                            foreach (var input in node.input) {
                                foreach (var output in node.output) {
                                    input.output.Add(output);
                                    output.input.Add(input);
                                    q.Enqueue(output);
                                }
                            }

                            foreach (var input in node.input) {
                                input.output.Remove(node);
                            }

                            foreach (var output in node.output) {
                                output.input.Remove(node);
                            }

                            removeNodes.Remove(node.node);
                        } else {
                            foreach (var output in node.output) {
                                q.Enqueue(output);
                            }
                        }
                    }
                }

            }

        }
        
        [ContextMenu("Update Sync State")]
        public void UpdateSyncStateForced() {

            foreach (var node in this.nodes) {
                node.ResetSyncPoints();
            }

            Run(Method.Awake, typeof(IAwake));
            Run(Method.Update, typeof(IUpdate));
            Run(Method.Start, typeof(IStart));
            Run(Method.Destroy, typeof(IDestroy));
            Run(Method.DrawGizmos, typeof(IDrawGizmos));
            
            void Run(Method method, System.Type type) {

                var startNode = this.GetStartNode(0);
                var exitNode = this.GetEndNode();
                var graph = new Graph(startNode, Filter);
                foreach (var kv in graph.nodes) {
                    var node = kv.Value;
                    if (node.output.Count > 1) {
                        foreach (var item in node.output) {
                            if (item.node == exitNode) {
                                item.input.Remove(node);
                                node.output.Remove(item);
                                break;
                            }
                        }
                    }
                }

                bool Filter(ME.BECS.Extensions.GraphProcessor.BaseNode node) {
                    if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode) {
                        if (systemNode.system != null) {
                            if (System.Array.IndexOf(systemNode.system.GetType().GetInterfaces(), type) >= 0) {
                                return true;
                            }
                        }
                    }

                    if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                        if (graphNode.graphValue != null) {
                            foreach (var n in graphNode.graphValue.nodes) {
                                if (Filter(n) == true) return true;
                            }
                        }
                    }
                    return false;
                }

                startNode.ResetSyncPoints();
                startNode.syncCount = 0;
                var visited = new System.Collections.Generic.HashSet<Graph.Node>();
                foreach (var kv in graph.nodes) {
                    var node = kv.Value;
                    visited.Clear();
                    this.CollectParents(node, visited);
                    var accumulator = 0;
                    accumulator -= node.input.Count;
                    foreach (var n in visited) {
                        accumulator += n.output.Count;
                        accumulator -= n.input.Count;
                    }

                    node.node.ValidateSyncPoints();
                    node.node.syncCount = accumulator;
                    node.node.SetSyncPoint(method, accumulator, node.node.syncCount == 0, Filter(node.node));
                    node.node.syncPoint = node.node.syncCount == 0;
                }

            }
            
            /*var visited = new System.Collections.Generic.HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode>();
            foreach (var node in this.nodes) {
                visited.Clear();
                this.CollectParents(node, visited);
                var accumulator = 0;
                accumulator -= node.GetInputNodes().Count();
                foreach (var n in visited) {
                    accumulator += n.GetOutputNodes().Count();
                    accumulator -= n.GetInputNodes().Count();
                }

                node.ValidateSyncPoints();
                node.syncCount = accumulator;
                node.SetSyncPoint(Method.Awake, accumulator, node.syncCount == 0, true);
                node.SetSyncPoint(Method.Start, accumulator, node.syncCount == 0, true);
                node.SetSyncPoint(Method.Update, accumulator, node.syncCount == 0, true);
                node.SetSyncPoint(Method.Destroy, accumulator, node.syncCount == 0, true);
                node.SetSyncPoint(Method.DrawGizmos, accumulator, node.syncCount == 0, true);
                node.syncPoint = node.syncCount == 0;
            }*/

        }
        
        private void CollectParents(Graph.Node node, System.Collections.Generic.HashSet<Graph.Node> visited) {

            if (node.node is ME.BECS.FeaturesGraph.Nodes.StartNode) {
                visited.Add(node);
                return;
            }

            {
                var inputNodes = node.input;
                foreach (var inputNode in inputNodes) {
                    visited.Add(inputNode);
                    this.CollectParents(inputNode, visited);
                }
            }

        }

        private void CollectParents(ME.BECS.Extensions.GraphProcessor.BaseNode node, System.Collections.Generic.HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode> visited) {

            if (node is ME.BECS.FeaturesGraph.Nodes.StartNode) {
                visited.Add(node);
                return;
            }

            {
                var inputNodes = node.GetInputNodes();
                foreach (var inputNode in inputNodes) {
                    visited.Add(inputNode);
                    this.CollectParents(inputNode, visited);
                }
            }

        }

        public override void UpdateSyncState() {
            
            if (this.builtInGraph == true) return;

            this.UpdateSyncStateForced();

        }

        public override void InitializeValidation() {

            base.InitializeValidation();

            this.ValidateStartNode();

        }

        private void ValidateStartNode() {

            if (this.nodes.Any(x => x is ME.BECS.FeaturesGraph.Nodes.StartNode) == false) {
                this.AddNode(new Nodes.StartNode() {
                    position = new Rect(0f, 0f, 100f, 100f),
                    GUID = "StartNode",
                });
            }

            if (this.nodes.Any(x => x is ME.BECS.FeaturesGraph.Nodes.ExitNode) == false) {
                this.AddNode(new Nodes.ExitNode() {
                    position = new Rect(500f, 0f, 100f, 100f),
                    GUID = "EndNode",
                });
            }

        }

        public SystemGroup DoAwake(ref World world, ushort updateType) {
            
            var rootSystemGroup = SystemGroup.Create(updateType);

            if (SystemsStatic.RaiseInitialize(this.GetId(), ref rootSystemGroup) == false) {
                
                this.runtimeRootSystemGroup = rootSystemGroup;
                var processor = new Extensions.GraphProcessor.ProcessGraphProcessor(this);
                processor.Run();
                world.AssignRootSystemGroup(rootSystemGroup);
                return this.runtimeRootSystemGroup;

            }

            return rootSystemGroup;

        }

        public bool IsValidStartNodeOrOther(ME.BECS.Extensions.GraphProcessor.BaseNode pStartNode, int index) {
            if (pStartNode is not ME.BECS.FeaturesGraph.Nodes.StartNode) return true;
            var k = 0;
            for (var i = 0; i < this.nodes.Count; ++i) {
                var node = this.nodes[i];
                if (node is ME.BECS.FeaturesGraph.Nodes.StartNode n) {
                    if (index == k) {
                        return pStartNode == n;
                    }
                    ++k;
                }
            }

            return false;
        }
        
        public ME.BECS.Extensions.GraphProcessor.BaseNode GetStartNode(int index) {
            var k = 0;
            for (var i = 0; i < this.nodes.Count; ++i) {
                var node = this.nodes[i];
                if (node is ME.BECS.FeaturesGraph.Nodes.StartNode n) {
                    if (index == k) return n;
                    ++k;
                }
            }

            return null;
        }

        public ME.BECS.Extensions.GraphProcessor.BaseNode GetEndNode() {
            foreach (var node in this.nodes) {
                if (node is ME.BECS.FeaturesGraph.Nodes.ExitNode n) {
                    return n;
                }
            }
            return null;
        }

    }

}