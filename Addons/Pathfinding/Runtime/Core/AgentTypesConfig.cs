using UnityEngine;

namespace ME.BECS.Pathfinding {
    
    [CreateAssetMenu(menuName = "ME.BECS/Pathfinding/Agent Types Config")]
    public class AgentTypesConfig : ScriptableObject {

        public GraphProperties graphProperties;
        public ME.BECS.Units.AgentType[] agentTypes;

    }

}