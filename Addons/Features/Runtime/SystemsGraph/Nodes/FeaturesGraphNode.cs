
namespace ME.BECS.FeaturesGraph.Nodes {

    using g = System.Collections.Generic;
    using Extensions.GraphProcessor;

    [System.Serializable]
    public abstract class FeaturesGraphNode : BaseNode {
        
        [IsCompatibleWithGraph]
        public static bool IsCompatible(BaseGraph graph) => graph is FeaturesGraph.SystemsGraph;

        public override void UpdateSyncState() {
            
            var q = new System.Collections.Generic.Queue<BaseNode>();
            var visited = new System.Collections.Generic.HashSet<BaseNode>();
            q.Enqueue(((SystemsGraph)this.graph).GetStartNode(0));
            var count = 0;
            var max = 100_000;
            while (q.Count > 0) {
                if (--max == 0) {
                    UnityEngine.Debug.LogError("max iter");
                    return;
                }
                var node = q.Dequeue();
                if (node == null) continue;

                var failed = false;
                foreach (var port in node.inputPorts) {
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
                    q.Enqueue(node);
                    continue;
                }
                
                visited.Add(node);

                var cnt = 0;
                foreach (var port in node.inputPorts) {
                    var edges = port.GetEdges();
                    if (edges.Count > 1) cnt += edges.Count - 1;
                }
                count -= cnt;
                
                if (node == this) {
                    break;
                }
                // look up forward and count split connections
                foreach (var port in node.outputPorts) {
                    var edges = port.GetEdges();
                    if (edges.Count > 1) count += edges.Count - 1;
                    foreach (var edge in edges) {
                        if (edge.inputNode == this || this.HasBackward(edge.inputNode) == true) {
                            q.Enqueue(edge.inputNode);
                        }
                    }
                }
                
            }
            
            this.syncCount = count;
            if (count != 0) {
                this.syncPoint = false;
            } else {
                this.syncPoint = true;
            }
	        
        }

        [UnityEngine.HideInInspector]
        public FeaturesGraph.SystemsGraph customRuntimeSystemRoot;
        public SystemsGraph featuresGraph => this.customRuntimeSystemRoot != null ? this.customRuntimeSystemRoot : this.graph as SystemsGraph;
        
        public SystemHandle runtimeHandle;

        public ref SystemGroup runtimeSystemGroup => ref this.featuresGraph.runtimeRootSystemGroup;

    }

}