
using System.Linq;

namespace ME.BECS.FeaturesGraph.Nodes {

    using g = System.Collections.Generic;
    using Extensions.GraphProcessor;

    /// <summary>
    /// Defines a features graph node entry in the associated graph.
    /// </summary>
    [System.Serializable]
    public abstract class FeaturesGraphNode : BaseNode {
        
        /// <summary>
        /// Tests whether the context is compatible.
        /// </summary>
        [IsCompatibleWithGraph]
        public static bool IsCompatible(BaseGraph graph) => graph is FeaturesGraph.SystemsGraph;

        /// <summary>
        /// Custom runtime system root used by <c>FeaturesGraphNode</c>.
        /// </summary>
        [UnityEngine.HideInInspector]
        public FeaturesGraph.SystemsGraph customRuntimeSystemRoot;
        /// <summary>
        /// Features graph used by <c>FeaturesGraphNode</c>.
        /// </summary>
        public SystemsGraph featuresGraph => this.customRuntimeSystemRoot != null ? this.customRuntimeSystemRoot : this.graph as SystemsGraph;
        
        /// <summary>
        /// Runtime handle used by <c>FeaturesGraphNode</c>.
        /// </summary>
        public SystemHandle runtimeHandle;

        /// <summary>
        /// Runtime system group used by <c>FeaturesGraphNode</c>.
        /// </summary>
        public ref SystemGroup runtimeSystemGroup => ref this.featuresGraph.runtimeRootSystemGroup;

    }

}