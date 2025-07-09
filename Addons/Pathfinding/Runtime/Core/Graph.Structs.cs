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

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using Unity.Jobs;
    using ME.BECS.Jobs;
    using System.Runtime.InteropServices;

    public enum Direction : uint {

        UpLeft = 0u,
        Up = 1u,
        UpRight = 2u,
        Right = 3u,
        DownRight = 4u,
        Down = 5u,
        DownLeft = 6u,
        Left = 7u,

    }

    public enum PathState : byte {

        NotCalculated,
        Success,
        Failed,

    }

    public enum NodeFlag : uint {

        None = 0,

    }

    [System.Serializable]
    public struct ObstacleChannel : System.IEquatable<ObstacleChannel> {

        public static readonly ObstacleChannel Obstacle = 0u;
        public static readonly ObstacleChannel Building = 1u;
        public static readonly ObstacleChannel Slope = 2u;

        [UnityEngine.SerializeField]
        private uint value;

        private ObstacleChannel(uint value) {
            this.value = value;
        }
        
        public static implicit operator uint(ObstacleChannel c) => c.value;
        public static implicit operator int(ObstacleChannel c) => (int)c.value;
        public static implicit operator ObstacleChannel(uint c) => new ObstacleChannel(c);
        public static bool operator ==(ObstacleChannel a, ObstacleChannel b) {
            return a.value == b.value;
        }

        public static bool operator !=(ObstacleChannel a, ObstacleChannel b) {
            return !(a == b);
        }

        public bool Equals(ObstacleChannel other) {
            return this.value == other.value;
        }

        public override bool Equals(object obj) {
            return obj is ObstacleChannel other && this.Equals(other);
        }

        public override int GetHashCode() {
            return (int)this.value;
        }

    }

    [System.Serializable]
    public struct GraphProperties {

        public float3 position;
        public uint chunkWidth;
        public uint chunkHeight;
        public tfloat nodeSize;
        public uint chunksCountX;
        public uint chunksCountY;

    }

    public interface IFilter {

        bool IsValid(in NodeInfo info, in RootGraphComponent root);

    }

    [System.Serializable]
    public struct Filter : IFilter {

        public bbool ignoreNonWalkable;
        public NodeFlag flags;

        public readonly bool IsValid(in NodeInfo info, in RootGraphComponent root) {
            if (this.ignoreNonWalkable == false && info.node.walkable == false) return false;
            if (info.node.flags == 0) return true;
            return ((uint)this.flags & info.node.flags) != 0;
        }

    }
    
    public struct ChunkCache {

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        public struct PortalPair {

            [FieldOffset(0)]
            public uint fromPortalIdx;
            [FieldOffset(4)]
            public uint toPortalIdx;
            [FieldOffset(0)]
            public ulong pack;

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

        [INLINE(256)]
        public bool TryGetCache(in MemoryAllocator allocator, PortalInfo fromPortalId, PortalInfo toPortalId, out Path.Chunk chunk) {
            this.lockSpinner.Lock();
            var result = this.data.TryGetValue(in allocator, new PortalPair(fromPortalId, toPortalId).pack, out chunk);
            this.lockSpinner.Unlock();
            return result;
        }

        [INLINE(256)]
        public void InvalidateCache(ref MemoryAllocator allocator, PortalInfo fromPortalId, PortalInfo toPortalId) {
            this.lockSpinner.Lock();
            var chunk = this.data.GetValueAndRemove(ref allocator, new PortalPair(fromPortalId, toPortalId).pack);
            if (chunk.flowField.IsCreated == true) chunk.flowField.Dispose(ref allocator);
            this.lockSpinner.Unlock();
        }

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

        [INLINE(256)]
        public static unsafe ChunkCache Create(safe_ptr<State> state, uint capacity) {
            return new ChunkCache() {
                data = new EquatableDictionary<ulong, Path.Chunk>(ref state.ptr->allocator, capacity),
            };
        }

    }

    public unsafe struct Heights {

        private GraphHeights data;

        [INLINE(256)]
        public void Dispose() {
            this.data.Dispose();
        }

        [INLINE(256)]
        public static Heights CreateDefault(World world) {
            return new Heights() {
                data = new GraphHeights() {
                    heightMap = new MemArray<tfloat>(ref world.state.ptr->allocator, 1u),
                },
            };
        }

        [INLINE(256)]
        public static Heights Create(float3 offset, UnityEngine.TerrainData terrain, World world) {
            return new Heights() {
                data = new GraphHeights(offset, terrain, world),
            };
        }

        [INLINE(256)]
        public bool IsValid() {
            return this.data.IsValid;
        }

        [INLINE(256)]
        public readonly tfloat GetHeight(float3 worldPosition) {
            if (this.data.heightMap.Length == 1) return 0f;
            return this.data.SampleHeight(worldPosition);
        }

        [INLINE(256)]
        public readonly tfloat GetHeight(float3 worldPosition, out float3 normal) {
            normal = math.up();
            if (this.data.heightMap.Length == 1) return 0f;
            return this.data.SampleHeight(worldPosition, out normal);
        }

    }
    
    public ref struct NodeInfo {

        public Node node;
        public uint chunkIndex;
        public uint nodeIndex;

        public NodeInfo(Node node, uint chunkIndex, uint nodeIndex) {
            this.node = node;
            this.chunkIndex = chunkIndex;
            this.nodeIndex = nodeIndex;
        }

    }
    
    [System.Serializable]
    public struct Node {

        public bool walkable => this.cost < Graph.UNWALKABLE;
        public uint flags;
        public tfloat height;
        public int cost;
        public ObstacleChannel obstacleChannel;
        #if PATHFINDING_NORMALS
        public float3 normal;
        #endif

    }

    public enum Side : byte {

        None = 0,
        Up,
        Down,
        Left,
        Right,

    }

    public struct Portal {

        public struct Connection {

            public uint length;
            public PortalInfo portalInfo;

        }

        public PortalInfo portalInfo;
        public uint area;
        public uint globalArea;
        public float3 position;
        public uint rangeStart;
        public uint rangeStartNodeIndex;
        public uint size;
        public Side side;
        public uint2 axis;
        public ListAuto<Connection> localNeighbours;
        public ListAuto<Connection> remoteNeighbours;

        public uint rangeEnd => this.rangeStart + this.size;
        public bool IsCreated => this.area != 0u;

    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct PortalInfo {

        [FieldOffset(0)]
        public uint chunkIndex;
        [FieldOffset(4)]
        public uint portalIndex;
        [FieldOffset(0)]
        public ulong pack;

        public static readonly PortalInfo Invalid = new PortalInfo() { chunkIndex = uint.MaxValue, portalIndex = uint.MaxValue };

        public bool IsValid => this.chunkIndex != uint.MaxValue && this.portalIndex != uint.MaxValue;

        public override string ToString() {
            return $"Chunk: {this.chunkIndex}, portal: {this.portalIndex}";
        }

    }

    public struct ChunkPortals {

        public List<Portal> list;

    }

    public struct PathInfo {

        public Unity.Collections.NativeList<PortalInfo> nodes;
        public PathState pathState;
        public float3 to;

        public override int GetHashCode() {
            int hash = 0;
            foreach (var portalInfo in this.nodes) {
                hash += (int)((portalInfo.portalIndex + 17) ^ portalInfo.chunkIndex);
            }
            return hash;
        }

    }

    public unsafe struct Path {

        public struct Chunk {

            public struct Item {

                public byte direction;
                public bbool hasLineOfSight;
                public tfloat bestCost;

            }

            public MemArray<Item> flowField;
            public bbool hasLineOfSight;

            [INLINE(256)]
            public readonly Chunk Clone(ref MemoryAllocator allocator) {
                var chunk = new Chunk {
                    flowField = new MemArray<Item>(ref allocator, this.flowField),
                };
                return chunk;
            }

        }

        public struct Target {

            public enum TargetType : byte {
                Point  = 0,
                Rect   = 1,
                Radius = 2,
            }
            
            public TargetType type;
            public float3 center;
            public float2 size;

            public tfloat radius {
                [INLINE(256)] get => this.size.x;
                [INLINE(256)] set => this.size.x = value;
            }

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

            [INLINE(256)]
            public Target(Target other) {
                this.type = other.type;
                this.center = other.center;
                this.size = other.size;
            }

            [INLINE(256)]
            public void FillNodes(in RootGraphComponent root, safe_ptr<State> state, ref Unity.Collections.NativeHashSet<Graph.TempNode> set, tfloat agentRadius) {
                
                switch (this.type) {
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

            [INLINE(256)]
            public void FillChunks(in RootGraphComponent root, safe_ptr<State> state, ref Unity.Collections.NativeHashSet<uint> set) {
                
                switch (this.type) {
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

            [INLINE(256)]
            public static Target Create(in float3 position) {
                return new Target() {
                    type = TargetType.Point,
                    center = position,
                };
            }
            
            [INLINE(256)]
            public static Target Create(in Bounds rect) {
                return new Target() {
                    type = TargetType.Rect,
                    center = rect.center,
                    size = new float2(rect.size.x, rect.size.z),
                };
            }
            
            [INLINE(256)]
            public static Target Create(in float3 position, tfloat radius) {
                return new Target() {
                    type = TargetType.Radius,
                    center = position,
                    radius = radius,
                };
            }

        }
        
        public Ent graph;
        public MemArray<Chunk> chunks;
        public MemAllocatorPtr<List<float3>> from;
        public Target to;
        public MemAllocatorPtr<int> hierarchyPathHash;
        public Filter filter;
        public byte isRecalculationRequired;

        public bool IsCreated => this.graph.IsAlive() == true && this.from.IsValid() == true && this.chunks.IsCreated == true;

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

    public struct TempNodeData {

        public bool isClosed;
        public bool isOpened;
        public tfloat startToCurNodeLen;
        public uint parent;

    }

}