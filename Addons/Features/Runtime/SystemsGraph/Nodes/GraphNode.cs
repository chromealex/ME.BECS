
namespace ME.BECS.FeaturesGraph.Nodes {

    using g = System.Collections.Generic;
    using Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Graph")]
    public class GraphNode : FeaturesGraphNode {

        [Input(name = "In Nodes", allowMultiple = true)]
        public g::List<SystemHandle> inputNodes;

        [Output(name = "Out Nodes", allowMultiple = true)]
        public g::List<SystemHandle> outputNodes;

        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: false, runtimeAssembliesOnly: true, showSelector: true)]
        public FeaturesGraph.SystemsGraph graphValue;

        public override string name {
            get {
                if (this.graphValue != null) {
                    return this.graphValue.name;
                }

                return "Graph Node";
            }
        }

        public override string style => "graph-node";
        public override UnityEngine.Color color => new UnityEngine.Color32(80, 0, 166, 255);

        public static System.Type GetTypeFromPropertyField(string typeName) {
            if (typeName == string.Empty) return null;
            var splitIndex = typeName.IndexOf(' ');
            var assembly = System.Reflection.Assembly.Load(typeName.Substring(0, splitIndex));
            return assembly.GetType(typeName.Substring(splitIndex + 1));
        }

        protected override void Process() {

            //UnityEngine.Debug.Log("Graph Node: " + this.name);
            // Skip nodes without input connections
            if (this.inputNodes == null || this.inputNodes.Count == 0) return;
            var handle = (this.inputNodes.Count == 1 ? this.inputNodes[0] : this.runtimeSystemGroup.Combine(this.inputNodes));
            this.runtimeHandle = handle;
            if (this.enabled == true && this.IsGroupEnabled() == true && this.graphValue != null) {
                
                var processor = new Extensions.GraphProcessor.ProcessGraphProcessor(this.graphValue);
                var systemGroup = SystemGroup.Create(this.runtimeSystemGroup.updateType);
                this.graphValue.runtimeRootSystemGroup = systemGroup;
                for (int i = 0; i < processor.processList.Count; ++i) {
                    ((FeaturesGraphNode)processor.processList[i]).customRuntimeSystemRoot = this.featuresGraph;
                }
                
                foreach (var node in processor.processList) {
                    if (node is StartNode startNode) {
                        startNode.rootDependsOn = this.runtimeHandle;
                        break;
                    }
                }
                processor.Run();
                foreach (var node in processor.processList) {
                    if (node is ExitNode exitNode) {
                        this.runtimeHandle = exitNode.runtimeHandle;
                        break;
                    }
                }
                
                this.runtimeHandle = this.runtimeSystemGroup.Add(this.graphValue.runtimeRootSystemGroup, this.runtimeHandle);

            }
            
        }
        
        [CustomPortInput(nameof(GraphNode.inputNodes), typeof(SystemHandle), allowCast = true)]
        public void GetInputs(g::List<SerializableEdge> edges) {
            var list = new System.Collections.Generic.List<SystemHandle>(edges.Count);
            foreach (var input in edges) {
                var handle = ((FeaturesGraphNode)input.outputNode).runtimeHandle;
                list.Add(handle);
            }

            this.inputNodes = list;
        }
        
    }
    
}