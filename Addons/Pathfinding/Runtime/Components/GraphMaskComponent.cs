#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {

    /// <summary>
    /// Defines configuration-backed entity data for graph mask.
    /// </summary>
    [ComponentGroup(typeof(PathfindingComponentGroup))]
    public struct GraphMaskComponent : IConfigComponent, IConfigInitialize {

        /// <summary>
        /// Offset into the associated storage or coordinate space.
        /// </summary>
        public float2 offset;
        /// <summary>
        /// Size of the represented value in the units used by this API.
        /// </summary>
        public uint2 size;
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public tfloat height;
        /// <summary>
        /// Heights size x used by <c>GraphMaskComponent</c>.
        /// </summary>
        public uint heightsSizeX;
        /// <summary>
        /// Obstacle channel used by <c>GraphMaskComponent</c>.
        /// </summary>
        public ObstacleChannel obstacleChannel;
        /// <summary>
        /// Whether ignore graph radius behavior or state is selected.
        /// </summary>
        public bbool ignoreGraphRadius;
        /// <summary>
        /// Cost assigned to this entry by the associated calculation.
        /// </summary>
        public byte cost;
        /// <summary>
        /// Graph mask used to select the applicable bits or entries.
        /// </summary>
        public int graphMask;

        /// <summary>
        /// Initializes graph mask component state from the supplied context.
        /// </summary>
        public void OnInitialize(in Ent ent) {

            var tr = ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
            GraphUtils.CreateGraphMask(in ent, tr.position, tr.rotation, this.size, this.cost, this.height, this.obstacleChannel, this.ignoreGraphRadius);

        }

    }

    /// <summary>
    /// Defines graph mask runtime component data used by entity processing.
    /// </summary>
    [ComponentGroup(typeof(PathfindingComponentGroup))]
    public struct GraphMaskRuntimeComponent : IComponentDestroy {

        /// <summary>
        /// Height samples used by the geometry or graph.
        /// </summary>
        public MemArrayAuto<tfloat> heights;
        /// <summary>
        /// Nodes composing the associated graph.
        /// </summary>
        public ListAuto<GraphNodeMemory> nodes;
        /// <summary>
        /// Nodes lock used by <c>GraphMaskRuntimeComponent</c>.
        /// </summary>
        public LockSpinner nodesLock;
        
        /// <summary>
        /// Destroys the referenced instance and applies its registered destruction handling.
        /// </summary>
        public unsafe void Destroy(in Ent ent) {

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
    
    /// <summary>
    /// Stores per-entity state for is graph mask dirty.
    /// </summary>
    [ComponentGroup(typeof(PathfindingComponentGroup))]
    public struct IsGraphMaskDirtyComponent : IComponent {}

}