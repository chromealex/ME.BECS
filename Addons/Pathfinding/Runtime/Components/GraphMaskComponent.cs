namespace ME.BECS.Pathfinding {

    using Unity.Mathematics;
    
    public struct GraphMaskComponent : IConfigComponent, IConfigInitialize {

        public float2 offset;
        public uint2 size;
        public byte ignoreGraphRadius;
        public byte cost;
        public uint heightsSizeX;
        public ObstacleChannel obstacleChannel;

        public void OnInitialize(in Ent ent) {
            
        }

    }

    public struct GraphMaskRuntimeComponent : IComponentDestroy {

        public MemArrayAuto<float> heights;
        public ListAuto<GraphNodeMemory> nodes;
        public LockSpinner nodesLock;
        
        public unsafe void Destroy() {

            var nextTick = this.nodes.ent.World.state->tick + 1UL;
            this.nodesLock.Lock();
            for (uint i = 0u; i < this.nodes.Count; ++i) {
                var node = this.nodes[i];
                var state = node.graph.World.state;
                var chunk = node.graph.Read<RootGraphComponent>();
                chunk.changedChunks[node.node.chunkIndex] = nextTick;
                ref var nodeData = ref chunk.chunks[node.node.chunkIndex].nodes[state, node.node.nodeIndex];
                nodeData = node.memory;
            }
            this.nodesLock.Unlock();
            
        }

    }
    
    public struct IsGraphMaskDirtyComponent : IComponent {}

}