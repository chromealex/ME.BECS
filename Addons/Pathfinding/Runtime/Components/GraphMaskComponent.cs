#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {

    public struct GraphMaskComponent : IConfigComponent, IConfigInitialize {

        public float2 offset;
        public uint2 size;
        public tfloat height;
        public uint heightsSizeX;
        public ObstacleChannel obstacleChannel;
        public byte ignoreGraphRadius;
        public byte cost;
        public int graphMask;

        public void OnInitialize(in Ent ent) {

            var tr = ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
            GraphUtils.CreateGraphMask(in ent, tr.position, tr.rotation, this.size, this.cost, this.height, this.obstacleChannel, this.ignoreGraphRadius == 1);

        }

    }

    public struct GraphMaskRuntimeComponent : IComponentDestroy {

        public MemArrayAuto<tfloat> heights;
        public ListAuto<GraphNodeMemory> nodes;
        public LockSpinner nodesLock;
        
        public unsafe void Destroy() {

            var nextTick = this.nodes.ent.World.CurrentTick + 1UL;
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