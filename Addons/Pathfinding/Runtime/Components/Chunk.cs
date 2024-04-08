namespace ME.BECS.Pathfinding {

    using Unity.Mathematics;
    
    public struct ChunkComponent {

        public float3 center;
        public MemArray<Node> nodes;
        public ChunkCache cache;
        public ChunkPortals portals;

    }

}