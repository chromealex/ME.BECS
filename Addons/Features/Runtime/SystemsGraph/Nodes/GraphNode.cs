
namespace ME.BECS.FeaturesGraph.Nodes {

    using g = System.Collections.Generic;
    using Extensions.GraphProcessor;

    /// <summary>
    /// Defines a graph node entry in the associated graph.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Graph")]
    public class GraphNode : FeaturesGraphNode {

        /// <summary>
        /// Input nodes used by <c>GraphNode</c>.
        /// </summary>
        [Input(name = "In Nodes", allowMultiple = true)]
        public g::List<SystemHandle> inputNodes;

        /// <summary>
        /// Output nodes used by <c>GraphNode</c>.
        /// </summary>
        [Output(name = "Out Nodes", allowMultiple = true)]
        public g::List<SystemHandle> outputNodes;

        /// <summary>
        /// Graph value used by <c>GraphNode</c>.
        /// </summary>
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: false, runtimeAssembliesOnly: true, showSelector: true)]
        public FeaturesGraph.SystemsGraph graphValue;

        /// <summary>
        /// Display or lookup name of this entry.
        /// </summary>
        public override string name {
            get {
                if (this.graphValue != null) {
                    return this.graphValue.name;
                }

                return "Graph Node";
            }
        }

        /// <summary>
        /// Style used by <c>GraphNode</c>.
        /// </summary>
        public override string style => "graph-node";
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public override UnityEngine.Color color => new UnityEngine.Color32(80, 0, 166, 255);

        /// <summary>
        /// Returns type from property field.
        /// </summary>
        public static System.Type GetTypeFromPropertyField(string typeName) {
            if (typeName == string.Empty) return null;
            var splitIndex = typeName.IndexOf(' ');
            var assembly = System.Reflection.Assembly.Load(typeName.Substring(0, splitIndex));
            return assembly.GetType(typeName.Substring(splitIndex + 1));
        }

        /// <summary>
        /// Processes the supplied inputs using this implementation.
        /// </summary>
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
        
        /// <summary>
        /// Returns inputs.
        /// </summary>
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