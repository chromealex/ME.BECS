
namespace ME.BECS.FeaturesGraph.Nodes {

    using g = System.Collections.Generic;
    using Extensions.GraphProcessor;

    /// <summary>
    /// Defines a system node entry in the associated graph.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("System")]
    public class SystemNode : FeaturesGraphNode {

        /// <summary>
        /// Input nodes used by <c>SystemNode</c>.
        /// </summary>
        [Input(name = "In Nodes", allowMultiple = true)]
        public g::List<SystemHandle> inputNodes;

        /// <summary>
        /// Output nodes used by <c>SystemNode</c>.
        /// </summary>
        [Output(name = "Out Nodes", allowMultiple = true)]
        public g::List<SystemHandle> outputNodes;

        /// <summary>
        /// System instance used by the associated operation.
        /// </summary>
        [UnityEngine.SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: true, runtimeAssembliesOnly: true, showSelector: true, showGenericTypes: true)]
        public ISystem system;

        /// <summary>
        /// Display or lookup name of this entry.
        /// </summary>
        public override string name {
            get {
                #if UNITY_EDITOR
                if (this.system != null) {
                    return UnityEditor.ObjectNames.NicifyVariableName(this.system.GetType().Name);
                }
                #endif

                return "System Node";
            }
        }

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

            // Skip nodes without input connections
            if (this.inputNodes == null || this.inputNodes.Count == 0) return;
            var handle = (this.inputNodes.Count == 1 ? this.inputNodes[0] : this.runtimeSystemGroup.Combine(this.inputNodes));
            this.runtimeHandle = handle;
            if (this.enabled == true && this.IsGroupEnabled() == true && this.system != null) this.runtimeHandle = this.runtimeSystemGroup.Add(this.system, handle);
            //UnityEngine.Debug.Log("Process: " + this.system + ", input handles: " + string.Join(", ", this.inputNodes) + ", output handle: " + this.runtimeHandle);
            
        }

        private void GetHandles(BaseNode node, g::List<SystemHandle> list) {
            if (node is RelayNode relayNode) {
                var combined = new g::List<SystemHandle>();
                foreach (var val in relayNode.input.nodes) {
                    this.GetHandles(val, combined);
                }
                list.Add(this.runtimeSystemGroup.Combine(combined));
            } else if (node is FeaturesGraphNode graphNode) {
                list.Add(graphNode.runtimeHandle);
            } else {
                throw new System.Exception($"Unknown node type {node}");
            }
        }
        
        /// <summary>
        /// Returns inputs.
        /// </summary>
        [CustomPortInput(nameof(SystemNode.inputNodes), typeof(SystemHandle), allowCast = true)]
        public void GetInputs(g::List<SerializableEdge> edges) {
            var list = new System.Collections.Generic.List<SystemHandle>(edges.Count);
            foreach (var input in edges) {
                this.GetHandles(input.outputNode, list);
            }

            this.inputNodes = list;
        }
        
    }
    
}