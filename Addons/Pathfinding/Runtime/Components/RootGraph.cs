
namespace ME.BECS.Pathfinding {
    
    using Unity.Mathematics;

    public struct RootGraphComponent : IComponent {

        public MemArrayAuto<ChunkComponent> chunks;
        public MemArrayAuto<ulong> changedChunks;
        public uint width => this.properties.chunksCountX;
        public uint height => this.properties.chunksCountY;
        public float3 position => this.properties.position;
        public uint chunkWidth => this.properties.chunkWidth;
        public uint chunkHeight => this.properties.chunkHeight;
        public float nodeSize => this.properties.nodeSize;
        
        public float agentRadius;
        public float agentMaxSlope;
        public GraphProperties properties;

    }

}