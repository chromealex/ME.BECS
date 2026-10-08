#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    /// <summary>
    /// Stores per-entity state for root graph.
    /// </summary>
    [ComponentGroup(typeof(PathfindingComponentGroup))]
    public struct RootGraphComponent : IComponent {

        /// <summary>
        /// Chunks composing the associated graph or storage.
        /// </summary>
        public MemArrayAuto<ChunkComponent> chunks;
        /// <summary>
        /// Changed chunks used by <c>RootGraphComponent</c>.
        /// </summary>
        public MemArrayAuto<ulong> changedChunks;
        /// <summary>
        /// Gets width; this implementation returns <c>this.properties.chunksCountX</c>.
        /// </summary>
        public uint width => this.properties.chunksCountX;
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public uint height => this.properties.chunksCountY;
        /// <summary>
        /// Position in the coordinate space used by the containing API.
        /// </summary>
        public float3 position => this.properties.position;
        /// <summary>
        /// Gets chunk width; this implementation returns <c>this.properties.chunkWidth</c>.
        /// </summary>
        public uint chunkWidth => this.properties.chunkWidth;
        /// <summary>
        /// Gets chunk height; this implementation returns <c>this.properties.chunkHeight</c>.
        /// </summary>
        public uint chunkHeight => this.properties.chunkHeight;
        /// <summary>
        /// Gets node size; this implementation returns <c>this.properties.nodeSize</c>.
        /// </summary>
        public tfloat nodeSize => this.properties.nodeSize;
        
        /// <summary>
        /// Agent radius used by <c>RootGraphComponent</c>.
        /// </summary>
        public tfloat agentRadius;
        /// <summary>
        /// Agent max slope used by <c>RootGraphComponent</c>.
        /// </summary>
        public tfloat agentMaxSlope;
        /// <summary>
        /// Configuration values used by this operation.
        /// </summary>
        public GraphProperties properties;

        /// <summary>
        /// Global area used by <c>RootGraphComponent</c>.
        /// </summary>
        public uint globalArea;

    }

}