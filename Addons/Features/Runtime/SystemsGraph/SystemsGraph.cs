using System.Linq;
using UnityEngine;

namespace ME.BECS.FeaturesGraph {

    [CreateAssetMenu(menuName = "ME.BECS/Features Graph")]
    public class SystemsGraph : Extensions.GraphProcessor.BaseGraph {

        public SystemGroup runtimeRootSystemGroup;

        public class Graph {

            public class Node {

                public ME.BECS.Extensions.GraphProcessor.BaseNode node;
                public System.Collections.Generic.List<Node> input = new System.Collections.Generic.List<Node>();
                public System.Collections.Generic.List<Node> output = new System.Collections.Generic.List<Node>();

            }

            public Node startNode;
            public System.Collections.Generic.Dictionary<ME.BECS.Extensions.GraphProcessor.BaseNode, Node> nodes = new System.Collections.Generic.Dictionary<ME.BECS.Extensions.GraphProcessor.BaseNode, Node>();
            
            public Graph(ME.BECS.Extensions.GraphProcessor.BaseNode startNode, System.Func<ME.BECS.Extensions.GraphProcessor.BaseNode, bool> filter) {
                
                this.startNode = new Node() { node = startNode };
                var root = this.startNode;
                {
                    {
                        if (this.nodes.TryGetValue(root.node, out var n) == false) {
                            n = new Node() { node = root.node };
                            this.nodes.Add(root.node, n);
                        }
                    }
                }

                var q = new System.Collections.Generic.Queue<Node>();
                q.Enqueue(root);
                while (q.Count > 0) {
                    
                    var current = q.Dequeue();
                    var list = current.node.GetOutputNodes().ToList();
                    foreach (var node in list) {
                        if (filter.Invoke(node) == true || node is ME.BECS.FeaturesGraph.Nodes.ExitNode) {
                            if (this.nodes.TryGetValue(node, out var n) == false) {
                                n = new Node() { node = node };
                                this.nodes.Add(node, n);
                            }
                            n.input.Add(current);
                            current.output.Add(n);
                            q.Enqueue(n);
                        } else {
                            // Connect all inputs with all outputs
                            foreach (var input in node.GetInputNodes()) {
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
                                }
                            }
                        }
                    }
                }

            }

        }
        
        [ContextMenu("Update Sync State")]
        public void UpdateSyncStateForced() {

            Run(Method.Awake, typeof(IAwake));
            Run(Method.Update, typeof(IUpdate));
            Run(Method.Start, typeof(IStart));
            Run(Method.Destroy, typeof(IDestroy));
            Run(Method.DrawGizmos, typeof(IDrawGizmos));
            
            void Run(Method method, System.Type type) {

                var startNode = this.GetStartNode(0);
                var graph = new Graph(startNode, Filter);

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