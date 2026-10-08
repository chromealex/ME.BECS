using UnityEngine;

namespace ME.BECS.Pathfinding {
    
    /// <summary>
    /// Configures the supported pathfinding agent types.
    /// </summary>
    [CreateAssetMenu(menuName = "ME.BECS/Pathfinding/Agent Types Config")]
    public class AgentTypesConfig : ScriptableObject {

        /// <summary>
        /// Graph properties used by <c>AgentTypesConfig</c>.
        /// </summary>
        public GraphProperties graphProperties;
        /// <summary>
        /// Agent types used by <c>AgentTypesConfig</c>.
        /// </summary>
        public ME.BECS.Units.AgentType[] agentTypes;

    }

}