using System.Linq;
using UnityEngine;

namespace ME.BECS.FeaturesGraph {

    [CreateAssetMenu(menuName = "ME.BECS/Features Graph")]
    public class SystemsGraph : Extensions.GraphProcessor.BaseGraph {

        public SystemGroup runtimeRootSystemGroup;

        [ContextMenu("Update Sync State")]
        public void UpdateSyncStateForced() {
            
            var startNode = this.GetStartNode(0);
            startNode.syncCount = 0;
            foreach (var node in this.nodes) {
                node.syncCount = this.GetParallelBranches(node);
                node.syncPoint = node.syncCount == 0;
            }
            /*
            var q = new System.Collections.Generic.Queue<ME.BECS.Extensions.GraphProcessor.BaseNode>();
            var visited = new System.Collections.Generic.HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode>();
            q.Enqueue(startNode);
            var max = 100_000;
            while (q.Count > 0) {

                if (--max == 0) {
                    Debug.LogError("max iter");
                    return;
                }
                var curNode = q.Dequeue();

                var failed = false;
                foreach (var port in curNode.inputPorts) {
                    var edges = port.GetEdges();
                    foreach (var edge in edges) {
                        if (visited.Contains(edge.outputNode) == false) {
                            failed = true;
                            break;
                        }
                    }
                    if (failed == true) {
                        break;
                    }
                }

                if (failed == true) {
                    q.Enqueue(curNode);
                    continue;
                }

                if (visited.Add(curNode) == false) continue;
                var input = curNode.GetInputNodes().ToList();
                curNode.syncCount -= input.Count;
                curNode.syncPoint = curNode.syncCount == 0;
                if (input.Count == 1) {
                    foreach (var node in input) {
                        if (node.syncPoint == false) {
                            curNode.syncCount = node.syncCount;
                            curNode.syncPoint = false;
                            break;
                        }
                    }
                }

                var output = curNode.GetOutputNodes().ToList();
                //Debug.Log("NODE: " + curNode.name + ", input: " + input.Count + ", output: " + output.Count + ", node.syncCount: " + curNode.syncCount);
                foreach (var node in output) {
                    node.syncCount += output.Count;
                }
                
                foreach (var node in output) {
                    if (visited.Contains(node) == false) {
                        q.Enqueue(node);
                    }
                }
            }*/
            
        }
        
        private int GetParallelBranches(ME.BECS.Extensions.GraphProcessor.BaseNode node) {

            if (node is ME.BECS.FeaturesGraph.Nodes.StartNode) {
                return 0;
            }

            var accumulator = 0;
            var inputNodes = node.GetOutputNodes().ToList();

            accumulator -= inputNodes.Count;
            foreach (var inputNode in inputNodes) {
                accumulator += inputNode.GetInputNodes().Count();
                accumulator += this.GetParallelBranches(inputNode);
            }

            return accumulator;

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