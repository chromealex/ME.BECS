#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {

    /// <summary>
    /// Defines chunk component data used by entity processing.
    /// </summary>
    public struct ChunkComponent {

        /// <summary>
        /// Center of the represented bounds.
        /// </summary>
        public float3 center;
        /// <summary>
        /// Nodes composing the associated graph.
        /// </summary>
        public MemArray<Node> nodes;
        /// <summary>
        /// Cached data reused by the associated operation.
        /// </summary>
        public ChunkCache cache;
        /// <summary>
        /// Portals used by <c>ChunkComponent</c>.
        /// </summary>
        public ChunkPortals portals;

    }

}