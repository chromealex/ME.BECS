#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {

    public struct AgentComponent : IComponent {

        public Filter filter;

    }
    
    public struct TargetComponent : IComponent {

        public static TargetComponent Create(in Ent targetInfo, in Ent graphEnt) => new TargetComponent() {
            target = targetInfo,
            graphEnt = graphEnt,
        };

        public Ent target;
        public Ent graphEnt;

    }

    public struct TargetInfoComponent : IComponent {
        
        public float3 position;
        public uint volume;

    }

    public struct TargetPathComponent : IComponent {

        public Path path;
        public MemArrayAuto<byte> chunksToUpdate;

    }

}