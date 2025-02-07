#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {

    public struct ChunkComponent {

        public float3 center;
        public MemArray<Node> nodes;
        public ChunkCache cache;
        public ChunkPortals portals;

    }

}