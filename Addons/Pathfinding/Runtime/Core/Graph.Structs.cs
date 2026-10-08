#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.Pathfinding {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using ME.BECS.Transforms;
    using Unity.Jobs;
    using ME.BECS.Jobs;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Defines the supported direction values.
    /// </summary>
    public enum Direction : uint {

        /// <summary>
        /// Up left option for <c>Direction</c>.
        /// </summary>
        UpLeft = 0u,
        /// <summary>
        /// Up option for <c>Direction</c>.
        /// </summary>
        Up = 1u,
        /// <summary>
        /// Up right option for <c>Direction</c>.
        /// </summary>
        UpRight = 2u,
        /// <summary>
        /// Right option for <c>Direction</c>.
        /// </summary>
        Right = 3u,
        /// <summary>
        /// Down right option for <c>Direction</c>.
        /// </summary>
        DownRight = 4u,
        /// <summary>
        /// Down option for <c>Direction</c>.
        /// </summary>
        Down = 5u,
        /// <summary>
        /// Down left option for <c>Direction</c>.
        /// </summary>
        DownLeft = 6u,
        /// <summary>
        /// Left option for <c>Direction</c>.
        /// </summary>
        Left = 7u,

    }

    /// <summary>
    /// Defines the supported path state values.
    /// </summary>
    public enum PathState : byte {

        /// <summary>
        /// Not calculated option for <c>PathState</c>.
        /// </summary>
        NotCalculated,
        /// <summary>
        /// Success option for <c>PathState</c>.
        /// </summary>
        Success,
        /// <summary>
        /// Failed option for <c>PathState</c>.
        /// </summary>
        Failed,

    }

    /// <summary>
    /// Defines the supported node flag values.
    /// </summary>
    public enum NodeFlag : uint {

        /// <summary>
        /// None option for <c>NodeFlag</c>.
        /// </summary>
        None = 0,

    }

    /// <summary>
    /// Identifies an obstacle channel used when evaluating graph traversal.
    /// </summary>
    [System.Serializable]
    public struct ObstacleChannel : System.IEquatable<ObstacleChannel> {

        /// <summary>
        /// Obstacle used by <c>ObstacleChannel</c>.
        /// </summary>
        public static readonly ObstacleChannel Obstacle = 0u;
        /// <summary>
        /// Building used by <c>ObstacleChannel</c>.
        /// </summary>
        public static readonly ObstacleChannel Building = 1u;
        /// <summary>
        /// Slope used by <c>ObstacleChannel</c>.
        /// </summary>
        public static readonly ObstacleChannel Slope = 2u;

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public uint value;

        private ObstacleChannel(uint value) {
            this.value = value;
        }
        
        /// <summary>
        /// Converts the supplied value to <c>uint</c>.
        /// </summary>
        public static implicit operator uint(ObstacleChannel c) => c.value;
        /// <summary>
        /// Converts the supplied value to <c>int</c>.
        /// </summary>
        public static implicit operator int(ObstacleChannel c) => (int)c.value;
        /// <summary>
        /// Converts the supplied value to <c>ObstacleChannel</c>.
        /// </summary>
        public static implicit operator ObstacleChannel(uint c) => new ObstacleChannel(c);
        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        public static bool operator ==(ObstacleChannel a, ObstacleChannel b) {
            return a.value == b.value;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        public static bool operator !=(ObstacleChannel a, ObstacleChannel b) {
            return !(a == b);
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public bool Equals(ObstacleChannel other) {
            return this.value == other.value;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public override bool Equals(object obj) {
            return obj is ObstacleChannel other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            return (int)this.value;
        }

    }

    /// <summary>
    /// Configures graph behavior and storage.
    /// </summary>
    [System.Serializable]
    public struct GraphProperties {

        /// <summary>
        /// Position in the coordinate space used by the containing API.
        /// </summary>
        public float3 position;
        /// <summary>
        /// Chunk width used by <c>GraphProperties</c>.
        /// </summary>
        public uint chunkWidth;
        /// <summary>
        /// Chunk height used by <c>GraphProperties</c>.
        /// </summary>
        public uint chunkHeight;
        /// <summary>
        /// Node size used by <c>GraphProperties</c>.
        /// </summary>
        public tfloat nodeSize;
        /// <summary>
        /// Chunks count x used by <c>GraphProperties</c>.
        /// </summary>
        public uint chunksCountX;
        /// <summary>
        /// Chunks count y used by <c>GraphProperties</c>.
        /// </summary>
        public uint chunksCountY;

    }

    /// <summary>
    /// Defines the operations required by filter.
    /// </summary>
    public interface IFilter {

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        bool IsValid(in NodeInfo info, in RootGraphComponent root);

    }

    /// <summary>
    /// Filters candidates according to the filter condition.
    /// </summary>
    [System.Serializable]
    public struct Filter : IFilter {

        /// <summary>
        /// Whether ignore non walkable behavior or state is selected.
        /// </summary>
        public bbool ignoreNonWalkable;
        /// <summary>
        /// Bit flags controlling the associated behavior.
        /// </summary>
        public NodeFlag flags;

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        public readonly bool IsValid(in NodeInfo info, in RootGraphComponent root) {
            if (this.ignoreNonWalkable == false && info.node.walkable == false) return false;
            if (info.node.flags == 0) return true;
            return ((uint)this.flags & info.node.flags) != 0;
        }

    }
    
    /// <summary>
    /// Caches graph-chunk traversal data for reuse.
    /// </summary>
    public struct ChunkCache {

        /// <summary>
        /// Identifies a pair of portals used by a cached path.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        public struct PortalPair {

            /// <summary>
            /// From portal idx used by <c>ChunkCache.PortalPair</c>.
            /// </summary>
            [FieldOffset(0)]
            public uint fromPortalIdx;
            /// <summary>
            /// To portal idx used by <c>ChunkCache.PortalPair</c>.
            /// </summary>
            [FieldOffset(4)]
            public uint toPortalIdx;
            /// <summary>
            /// Packed representation of this value.
            /// </summary>
            [FieldOffset(0)]
            public ulong pack;

            /// <summary>
            /// Initializes <c>PortalPair</c> from the supplied from portal, to portal.
            /// </summary>
            [INLINE(256)]
            public PortalPair(PortalInfo fromPortal, PortalInfo toPortal) {
                this.pack = default;
                this.fromPortalIdx = fromPortal.portalIndex;
                this.toPortalIdx = toPortal.portalIndex;
            }

        }

        // key = portal id to portal id pair
        // value = cache
        private EquatableDictionary<ulong, Path.Chunk> data;
        private LockSpinner lockSpinner;

        /// <summary>
        /// Attempts to get cache and reports whether the operation succeeded.
        /// </summary>
        [INLINE(256)]
        public bool TryGetCache(in MemoryAllocator allocator, PortalInfo fromPortalId, PortalInfo toPortalId, out Path.Chunk chunk) {
            this.lockSpinner.Lock();
            var result = this.data.TryGetValue(in allocator, new PortalPair(fromPortalId, toPortalId).pack, out chunk);
            this.lockSpinner.Unlock();
            return result;
        }

        /// <summary>
        /// Invalidates cache.
        /// </summary>
        [INLINE(256)]
        public void InvalidateCache(ref MemoryAllocator allocator, PortalInfo fromPortalId, PortalInfo toPortalId) {
            this.lockSpinner.Lock();
            var chunk = this.data.GetValueAndRemove(ref allocator, new PortalPair(fromPortalId, toPortalId).pack);
            if (chunk.flowField.IsCreated == true) chunk.flowField.Dispose(ref allocator);
            this.lockSpinner.Unlock();
        }

        /// <summary>
        /// Invalidates cache.
        /// </summary>
        [INLINE(256)]
        public void InvalidateCache(ref MemoryAllocator allocator, in ChunkComponent chunk) {
            for (uint i = 0u; i < chunk.portals.list.Count; ++i) {
                var portalFrom = chunk.portals.list[in allocator, i];
                for (uint j = 0u; j < portalFrom.localNeighbours.Count; ++j) {
                    var toPortal = portalFrom.localNeighbours[in allocator, j];
                    this.InvalidateCache(ref allocator, portalFrom.portalInfo, toPortal.portalInfo);
                }
            }
        }

        /// <summary>
        /// Invalidate current cache if exist
        /// Call if chunk is dirty
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="fromPortal"></param>
        /// <param name="toPortal"></param>
        /// <param name="chunk"></param>
        [INLINE(256)]
        public void UpdateCache(ref MemoryAllocator allocator, PortalInfo fromPortal, PortalInfo toPortal, in Path.Chunk chunk) {

            this.InvalidateCache(ref allocator, fromPortal, toPortal);
            var key = new PortalPair(fromPortal, toPortal).pack;
            var chunkCopy = chunk.Clone(ref allocator);
            this.lockSpinner.Lock();
            if (this.data.TryAdd(ref allocator, key, chunkCopy) == false) {
                chunkCopy.flowField.Dispose(ref allocator);
            }
            this.lockSpinner.Unlock();

        }

        /// <summary>
        /// Creates <c>ChunkCache</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static unsafe ChunkCache Create(safe_ptr<State> state, uint capacity) {
            return new ChunkCache() {
                data = new EquatableDictionary<ulong, Path.Chunk>(ref state.ptr->allocator, capacity),
            };
        }

    }

    /// <summary>
    /// Stores height data sampled by the graph.
    /// </summary>
    public unsafe struct Heights {

        private GraphHeights data;

        /// <summary>
        /// Releases the resources owned by this heights instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            this.data.Dispose();
        }

        /// <summary>
        /// Creates default.
        /// </summary>
        [INLINE(256)]
        public static Heights CreateDefault(World world) {
            return new Heights() {
                data = new GraphHeights() {
                    heightMap = new MemArray<tfloat>(ref world.state.ptr->allocator, 1u),
                },
            };
        }

        /// <summary>
        /// Creates <c>Heights</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static Heights Create(float3 offset, UnityEngine.TerrainData terrain, World world) {
            return new Heights() {
                data = new GraphHeights(offset, terrain, world),
            };
        }

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        public bool IsValid() {
            return this.data.IsValid;
        }

        /// <summary>
        /// Returns height.
        /// </summary>
        [INLINE(256)]
        public readonly tfloat GetHeight(float3 worldPosition) {
            if (this.data.heightMap.Length == 1) return 0f;
            return this.data.SampleHeight(worldPosition);
        }

        /// <summary>
        /// Returns height.
        /// </summary>
        [INLINE(256)]
        public readonly tfloat GetHeight(float3 worldPosition, out float3 normal) {
            normal = math.up();
            if (this.data.heightMap.Length == 1) return 0f;
            return this.data.SampleHeight(worldPosition, out normal);
        }

    }
    
    /// <summary>
    /// Describes a graph node and its traversal metadata.
    /// </summary>
    public ref struct NodeInfo {

        /// <summary>
        /// Node processed or represented by this entry.
        /// </summary>
        public Node node;
        /// <summary>
        /// Chunk index used to locate the associated entry.
        /// </summary>
        public uint chunkIndex;
        /// <summary>
        /// Node index used to locate the associated entry.
        /// </summary>
        public uint nodeIndex;

        /// <summary>
        /// Initializes <c>NodeInfo</c> from the supplied node, chunk index, node index.
        /// </summary>
        public NodeInfo(Node node, uint chunkIndex, uint nodeIndex) {
            this.node = node;
            this.chunkIndex = chunkIndex;
            this.nodeIndex = nodeIndex;
        }

    }
    
    /// <summary>
    /// Defines a node entry in the associated graph.
    /// </summary>
    [System.Serializable]
    public struct Node {

        /// <summary>
        /// Whether walkable behavior or state is selected.
        /// </summary>
        public bool walkable => this.cost < Graph.UNWALKABLE;
        /// <summary>
        /// Cost assigned to this entry by the associated calculation.
        /// </summary>
        public int cost;
        /// <summary>
        /// Obstacle channel used by <c>Node</c>.
        /// </summary>
        public ObstacleChannel obstacleChannel;
        #if PATHFINDING_FLAGS
        /// <summary>
        /// Bit flags controlling the associated behavior.
        /// </summary>
        public uint flags;
        #else
        /// <summary>
        /// Bit flags controlling the associated behavior.
        /// </summary>
        public uint flags => 0u;
        #endif
        #if PATHFINDING_NORMALS
        /// <summary>
        /// Normal used by <c>Node</c>.
        /// </summary>
        public float3 normal;
        #endif
        #if PATHFINDING_HEIGHTS
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public tfloat height;
        #else
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public tfloat height {
            get => 0f;
            set { }
        }
        #endif

    }

    /// <summary>
    /// Defines the supported side values.
    /// </summary>
    public enum Side : byte {

        /// <summary>
        /// None option for <c>Side</c>.
        /// </summary>
        None = 0,
        /// <summary>
        /// Up option for <c>Side</c>.
        /// </summary>
        Up,
        /// <summary>
        /// Down option for <c>Side</c>.
        /// </summary>
        Down,
        /// <summary>
        /// Left option for <c>Side</c>.
        /// </summary>
        Left,
        /// <summary>
        /// Right option for <c>Side</c>.
        /// </summary>
        Right,

    }

    /// <summary>
    /// Connects graph regions across chunk boundaries.
    /// </summary>
    public struct Portal {

        /// <summary>
        /// Stores a connection record used by <c>Portal</c>.
        /// </summary>
        public struct Connection {

            /// <summary>
            /// Number of elements in the associated storage.
            /// </summary>
            public uint length;
            /// <summary>
            /// Portal info used by <c>Portal.Connection</c>.
            /// </summary>
            public PortalInfo portalInfo;

        }

        /// <summary>
        /// Portal info used by <c>Portal</c>.
        /// </summary>
        public PortalInfo portalInfo;
        /// <summary>
        /// Area used by <c>Portal</c>.
        /// </summary>
        public uint area;
        /// <summary>
        /// Global area used by <c>Portal</c>.
        /// </summary>
        public uint globalArea;
        /// <summary>
        /// Position in the coordinate space used by the containing API.
        /// </summary>
        public float3 position;
        /// <summary>
        /// Range start used by <c>Portal</c>.
        /// </summary>
        public uint rangeStart;
        /// <summary>
        /// Range start node index used to locate the associated entry.
        /// </summary>
        public uint rangeStartNodeIndex;
        /// <summary>
        /// Size of the represented value in the units used by this API.
        /// </summary>
        public uint size;
        /// <summary>
        /// Side used by <c>Portal</c>.
        /// </summary>
        public Side side;
        /// <summary>
        /// Axis used by <c>Portal</c>.
        /// </summary>
        public uint2 axis;
        /// <summary>
        /// Local neighbours used by <c>Portal</c>.
        /// </summary>
        public ListAuto<Connection> localNeighbours;
        /// <summary>
        /// Remote neighbours used by <c>Portal</c>.
        /// </summary>
        public ListAuto<Connection> remoteNeighbours;

        /// <summary>
        /// Range end used by <c>Portal</c>.
        /// </summary>
        public uint rangeEnd => this.rangeStart + this.size;
        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.area != 0u;

    }

    /// <summary>
    /// Describes a graph portal and the route information associated with it.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct PortalInfo {

        /// <summary>
        /// Chunk index used to locate the associated entry.
        /// </summary>
        [FieldOffset(0)]
        public uint chunkIndex;
        /// <summary>
        /// Portal index used to locate the associated entry.
        /// </summary>
        [FieldOffset(4)]
        public uint portalIndex;
        /// <summary>
        /// Packed representation of this value.
        /// </summary>
        [FieldOffset(0)]
        public ulong pack;

        /// <summary>
        /// Sentinel value representing an invalid entry.
        /// </summary>
        public static readonly PortalInfo Invalid = new PortalInfo() { chunkIndex = uint.MaxValue, portalIndex = uint.MaxValue };

        /// <summary>
        /// Indicates is valid.
        /// </summary>
        public bool IsValid => this.chunkIndex != uint.MaxValue && this.portalIndex != uint.MaxValue;

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() {
            return $"Chunk: {this.chunkIndex}, portal: {this.portalIndex}";
        }

    }

    /// <summary>
    /// Stores the portals connecting a graph chunk to its neighbors.
    /// </summary>
    public struct ChunkPortals {

        /// <summary>
        /// List storage used by this instance.
        /// </summary>
        public List<Portal> list;

    }

    /// <summary>
    /// Describes a computed path and its traversal results.
    /// </summary>
    public struct PathInfo {

        /// <summary>
        /// Nodes composing the associated graph.
        /// </summary>
        public Unity.Collections.NativeList<PortalInfo> nodes;
        /// <summary>
        /// Path state used by <c>PathInfo</c>.
        /// </summary>
        public PathState pathState;
        /// <summary>
        /// Ending value or destination of the associated operation.
        /// </summary>
        public float3 to;

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            int hash = 0;
            foreach (var portalInfo in this.nodes) {
                hash += (int)((portalInfo.portalIndex + 17) ^ portalInfo.chunkIndex);
            }
            return hash;
        }

    }

    /// <summary>
    /// Stores the route and per-chunk flow data used by a pathfinding agent.
    /// </summary>
    public unsafe struct Path {

        /// <summary>
        /// Defines chunk state and operations for <c>Path</c>.
        /// </summary>
        public struct Chunk {

            /// <summary>
            /// Stores a item record used by <c>Path.Chunk</c>.
            /// </summary>
            public struct Item {

                /// <summary>
                /// Direction used by <c>Path.Chunk.Item</c>.
                /// </summary>
                public byte direction;
                /// <summary>
                /// Indicates has line of sight.
                /// </summary>
                public bbool hasLineOfSight;
                /// <summary>
                /// Best cost used by <c>Path.Chunk.Item</c>.
                /// </summary>
                public tfloat bestCost;

            }

            /// <summary>
            /// Flow field used by <c>Path.Chunk</c>.
            /// </summary>
            public MemArray<Item> flowField;
            /// <summary>
            /// Indicates has line of sight.
            /// </summary>
            public bbool hasLineOfSight;

            /// <summary>
            /// Creates a copy of the supplied state using the requested allocation context.
            /// </summary>
            [INLINE(256)]
            public readonly Chunk Clone(ref MemoryAllocator allocator) {
                var chunk = new Chunk {
                    flowField = new MemArray<Item>(ref allocator, this.flowField),
                };
                return chunk;
            }

        }

        /// <summary>
        /// Defines target state and operations for <c>Path</c>.
        /// </summary>
        public struct Target {

            /// <summary>
            /// Defines the supported target type values.
            /// </summary>
            public enum TargetType : byte {
                /// <summary>
                /// Point option for <c>Path.Target.TargetType</c>.
                /// </summary>
                Point  = 0,
                /// <summary>
                /// Rect option for <c>Path.Target.TargetType</c>.
                /// </summary>
                Rect   = 1,
                /// <summary>
                /// Radius option for <c>Path.Target.TargetType</c>.
                /// </summary>
                Radius = 2,
                /// <summary>
                /// Points option for <c>Path.Target.TargetType</c>.
                /// </summary>
                Points = 3,
            }
            
            /// <summary>
            /// Type descriptor used by the associated operation.
            /// </summary>
            public TargetType type;
            /// <summary>
            /// Center of the represented bounds.
            /// </summary>
            public float3 center;
            /// <summary>
            /// Size of the represented value in the units used by this API.
            /// </summary>
            public float2 size;
            /// <summary>
            /// Positions used by <c>Path.Target</c>.
            /// </summary>
            public ListAuto<float3> positions;

            /// <summary>
            /// Radius used by the associated shape or query.
            /// </summary>
            public tfloat radius {
                [INLINE(256)] get => this.size.x;
                [INLINE(256)] set => this.size.x = value;
            }

            /// <summary>
            /// Number of elements that fit in the currently reserved storage.
            /// </summary>
            public int Capacity {
                [INLINE(256)]
                get {
                    if (this.type == TargetType.Point) return 1;
                    if (this.type == TargetType.Radius) {
                        return (int)(this.radius * this.radius);
                    }
                    // TargetType.Rect
                    return (int)(this.size.x * this.size.y);
                }
            }

            /// <summary>
            /// Initializes <c>Target</c> from the supplied other.
            /// </summary>
            [INLINE(256)]
            public Target(Target other) {
                this.type = other.type;
                this.center = other.center;
                this.size = other.size;
                this.positions = other.positions;
            }

            /// <summary>
            /// Populates nodes.
            /// </summary>
            [INLINE(256)]
            public void FillNodes(in RootGraphComponent root, safe_ptr<State> state, ref Unity.Collections.NativeHashSet<Graph.TempNode> set, tfloat agentRadius) {
                
                switch (this.type) {
                    case TargetType.Points: {
                        for (uint i = 0u; i < this.positions.Count; ++i) {
                            var point = this.positions[i];
                            var targetChunkIndex = Graph.GetChunkIndex(in root, in point, true);
                            var targetChunk = root.chunks[state, targetChunkIndex];
                            var targetNodeIndex = Graph.GetNodeIndex(in root, in targetChunk, in point, false);
                            set.Add(new Graph.TempNode() {
                                chunkIndex = targetChunkIndex,
                                nodeIndex = targetNodeIndex,
                            });
                        }
                        return;
                    }
                    
                    case TargetType.Point: {
                        var targetChunkIndex = Graph.GetChunkIndex(in root, in this.center, true);
                        var targetChunk = root.chunks[state, targetChunkIndex];
                        var targetNodeIndex = Graph.GetNodeIndex(in root, in targetChunk, in this.center, false);
                        set.Add(new Graph.TempNode() {
                            chunkIndex = targetChunkIndex,
                            nodeIndex = targetNodeIndex,
                        });
                        return;
                    }

                    case TargetType.Rect: {
                        var width = math.max(1u, (uint)math.round((this.size.x + agentRadius * 2f) / root.nodeSize));
                        var height = math.max(1u, (uint)math.round((this.size.y + agentRadius * 2f) / root.nodeSize));
                        var corner = this.center - new float3((this.size.x + agentRadius * 2f) * 0.5f, 0f, (this.size.y + agentRadius * 2f) * 0.5f);
                        for (uint x = 0u; x < width; ++x) {
                            for (uint y = 0u; y < height; ++y) {
                                var pos = corner + new float3(x * root.nodeSize, 0f, y * root.nodeSize);
                                var targetChunkIndex = Graph.GetChunkIndex(in root, in pos, true);
                                var targetChunk = root.chunks[state, targetChunkIndex];
                                var targetNodeIndex = Graph.GetNodeIndex(in root, in targetChunk, in pos, false);
                                set.Add(new Graph.TempNode() {
                                    chunkIndex = targetChunkIndex,
                                    nodeIndex = targetNodeIndex,
                                });
                            }
                        }
                        return;
                    }

                    case TargetType.Radius: {
                        var width = math.max(1u, (uint)math.round((this.radius + agentRadius * 2f) / root.nodeSize));
                        var height = math.max(1u, (uint)math.round((this.radius + agentRadius * 2f) / root.nodeSize));
                        var corner = this.center - new float3((this.radius + agentRadius * 2f) * 0.5f, 0f, (this.radius + agentRadius * 2f) * 0.5f);
                        var radiusSq = this.radius * this.radius;
                        for (uint x = 0u; x < width; ++x) {
                            for (uint y = 0u; y < height; ++y) {
                                var pos = corner + new float3(x * root.nodeSize, 0f, y * root.nodeSize);
                                var dist = math.distancesq(pos, this.center);
                                if (dist > radiusSq) continue;
                                var targetChunkIndex = Graph.GetChunkIndex(in root, in pos, true);
                                var targetChunk = root.chunks[state, targetChunkIndex];
                                var targetNodeIndex = Graph.GetNodeIndex(in root, in targetChunk, in pos, false);
                                set.Add(new Graph.TempNode() {
                                    chunkIndex = targetChunkIndex,
                                    nodeIndex = targetNodeIndex,
                                });
                            }
                        }

                        break;
                    }
                }

            }

            /// <summary>
            /// Populates chunks.
            /// </summary>
            [INLINE(256)]
            public void FillChunks(in RootGraphComponent root, safe_ptr<State> state, ref Unity.Collections.NativeHashSet<uint> set) {
                
                switch (this.type) {
                    case TargetType.Points: {
                        for (uint i = 0u; i < this.positions.Count; ++i) {
                            var point = this.positions[i];
                            var targetChunkIndex = Graph.GetChunkIndex(in root, in point, true);
                            set.Add(targetChunkIndex);
                        }
                        return;
                    }

                    case TargetType.Point: {
                        var targetChunkIndex = Graph.GetChunkIndex(in root, in this.center, true);
                        set.Add(targetChunkIndex);
                        return;
                    }

                    case TargetType.Rect: {
                        var width = math.max(1u, (uint)math.round(this.size.x / root.nodeSize));
                        var height = math.max(1u, (uint)math.round(this.size.y / root.nodeSize));
                        for (uint x = 0u; x < width; ++x) {
                            for (uint y = 0u; y < height; ++y) {
                                var pos = this.center - new float3(this.size.x * 0.5f, 0f, this.size.y * 0.5f) + new float3(x * root.nodeSize, 0f, y * root.nodeSize);
                                var targetChunkIndex = Graph.GetChunkIndex(in root, in pos, true);
                                set.Add(targetChunkIndex);
                            }
                        }
                        return;
                    }

                    case TargetType.Radius: {
                        var width = math.max(1u, (uint)math.round(this.radius / root.nodeSize));
                        var height = math.max(1u, (uint)math.round(this.radius / root.nodeSize));
                        var radiusSq = this.radius * this.radius;
                        for (uint x = 0u; x < width; ++x) {
                            for (uint y = 0u; y < height; ++y) {
                                var pos = this.center - new float3(this.radius * 0.5f, 0f, this.radius * 0.5f) + new float3(x * root.nodeSize, 0f, y * root.nodeSize);
                                var dist = math.distancesq(pos, this.center);
                                if (dist > radiusSq) continue;
                                var targetChunkIndex = Graph.GetChunkIndex(in root, in pos, true);
                                set.Add(targetChunkIndex);
                            }
                        }

                        break;
                    }
                }
                
            }

            /// <summary>
            /// Tests whether the specified value is present.
            /// </summary>
            [INLINE(256)]
            public bool Contains(float3 position, tfloat radiusSq) {
                switch (this.type) {
                    case TargetType.Point:
                        return math.lengthsq(position - this.center) <= radiusSq;

                    case TargetType.Radius: {
                        var r = math.sqrt(radiusSq) + this.radius;
                        return math.lengthsq(position - this.center) <= r * r;
                    }

                    case TargetType.Rect: {
                        var r = math.sqrt(radiusSq);
                        return new Rect(this.center.xz, new float2(this.size.x + r * 2f, this.size.y + r * 2f)).Contains(position.xz);
                    }
                }
                return false;
            }

            /// <summary>
            /// Creates <c>Target</c> using the supplied creation arguments.
            /// </summary>
            [INLINE(256)]
            public static Target Create(in float3 position) {
                return new Target() {
                    type = TargetType.Point,
                    center = position,
                };
            }

            /// <summary>
            /// Creates <c>Target</c> using the supplied creation arguments.
            /// </summary>
            [INLINE(256)]
            public static Target Create(in ListAuto<float3> positions) {
                return new Target() {
                    type = TargetType.Points,
                    positions = positions,
                };
            }

            /// <summary>
            /// Creates <c>Target</c> using the supplied creation arguments.
            /// </summary>
            [INLINE(256)]
            public static Target Create(in Bounds rect) {
                return new Target() {
                    type = TargetType.Rect,
                    center = rect.center,
                    size = new float2(rect.size.x, rect.size.z),
                };
            }
            
            /// <summary>
            /// Creates <c>Target</c> using the supplied creation arguments.
            /// </summary>
            [INLINE(256)]
            public static Target Create(in float3 position, tfloat radius) {
                return new Target() {
                    type = TargetType.Radius,
                    center = position,
                    radius = radius,
                };
            }

        }
        
        /// <summary>
        /// Graph used by the associated operation.
        /// </summary>
        public Ent graph;
        /// <summary>
        /// Chunks composing the associated graph or storage.
        /// </summary>
        public MemArray<Chunk> chunks;
        /// <summary>
        /// Starting value or source of the associated operation.
        /// </summary>
        public MemAllocatorPtr<List<float3>> from;
        /// <summary>
        /// Ending value or destination of the associated operation.
        /// </summary>
        public Target to;
        /// <summary>
        /// Hierarchy path hash used by <c>Path</c>.
        /// </summary>
        public MemAllocatorPtr<int> hierarchyPathHash;
        /// <summary>
        /// Filter restricting the entries considered by this operation.
        /// </summary>
        public Filter filter;
        /// <summary>
        /// Indicates is recalculation required.
        /// </summary>
        public byte isRecalculationRequired;

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.graph.IsAlive() == true && this.from.IsValid() == true && this.chunks.IsCreated == true;

        /// <summary>
        /// Releases the resources owned by this path instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(in World world) {
            
            for (uint i = 0; i < this.chunks.Length; ++i) {
                var chunk = this.chunks[world.state, i];
                chunk.flowField.Dispose(ref world.state.ptr->allocator);
            }

            this.from.As(in world.state.ptr->allocator).Dispose(ref world.state.ptr->allocator);
            this.from.Dispose(ref world.state.ptr->allocator);
            this.chunks.Dispose(ref world.state.ptr->allocator);
            this = default;

        }

    }

    /// <summary>
    /// Stores temp node data for the associated pathfinding API.
    /// </summary>
    public struct TempNodeData {

        /// <summary>
        /// Indicates is closed.
        /// </summary>
        public bool isClosed;
        /// <summary>
        /// Indicates is opened.
        /// </summary>
        public bool isOpened;
        /// <summary>
        /// Start to cur node len used by <c>TempNodeData</c>.
        /// </summary>
        public tfloat startToCurNodeLen;
        /// <summary>
        /// Parent entry in the represented hierarchy.
        /// </summary>
        public uint parent;

    }

}