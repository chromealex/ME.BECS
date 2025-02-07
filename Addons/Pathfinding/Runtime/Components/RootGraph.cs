#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    public struct RootGraphComponent : IComponent {

        public MemArrayAuto<ChunkComponent> chunks;
        public MemArrayAuto<ulong> changedChunks;
        public uint width => this.properties.chunksCountX;
        public uint height => this.properties.chunksCountY;
        public float3 position => this.properties.position;
        public uint chunkWidth => this.properties.chunkWidth;
        public uint chunkHeight => this.properties.chunkHeight;
        public tfloat nodeSize => this.properties.nodeSize;
        
        public tfloat agentRadius;
        public tfloat agentMaxSlope;
        public GraphProperties properties;

    }

}