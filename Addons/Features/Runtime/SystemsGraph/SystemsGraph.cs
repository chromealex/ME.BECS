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
                var visited = new System.Collections.Generic.HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode>();
                this.CollectParents(node, visited);
                var accumulator = 0;
                accumulator -= node.GetInputNodes().Count();
                foreach (var n in visited) {
                    accumulator += n.GetOutputNodes().Count();
                    accumulator -= n.GetInputNodes().Count();
                }
                node.syncCount = accumulator;
                node.syncPoint = node.syncCount == 0;
            }
            
        }
        
        private void CollectParents(ME.BECS.Extensions.GraphProcessor.BaseNode node, System.Collections.Generic.HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode> visited) {

            if (node is ME.BECS.FeaturesGraph.Nodes.StartNode) {
                visited.Add(node);
                return;
            }
            
            var inputNodes = node.GetInputNodes().ToList();
            
            foreach (var inputNode in inputNodes) {
                visited.Add(inputNode);
                this.CollectParents(inputNode, visited);
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