#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
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

    public enum ObstacleChannel : uint {

        Obstacle = 0u,
        Building = 1u,
        Slope = 2u,

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

    [System.Serializable]
    public struct Filter {

        public byte ignoreNonWalkable;
        public NodeFlag flags;

        public readonly bool IsValid(in Node node) {
            if (this.ignoreNonWalkable == 0 && node.walkable == false) return false;
            if (node.flags == 0) return true;
            return ((uint)this.flags & node.flags) != 0;
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

    [System.Serializable]
    public struct Node {

        public bool walkable => this.cost < Graph.UNWALKABLE;
        public uint flags;
        public int cost;
        public ObstacleChannel obstacleChannel;
        public tfloat height;
        public float3 normal;

    }

    public enum Side : byte {

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

    public struct PortalInfo {

        public uint chunkIndex;
        public uint portalIndex;

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

        public World world;
        public Unity.Collections.NativeList<PortalInfo> nodes;
        public PathState pathState;

    }

    public unsafe struct Path {

        public struct Chunk {

            public struct Item {

                public byte direction;
                public byte hasLineOfSight;
                public tfloat bestCost;

            }

            public uint index;
            public MemArray<Item> flowField;
            public byte hasLineOfSight;

            [INLINE(256)]
            public readonly Chunk Clone(ref MemoryAllocator allocator) {
                var chunk = new Chunk {
                    index = this.index,
                    flowField = new MemArray<Item>(ref allocator, this.flowField),
                };
                return chunk;
            }

        }

        public Ent graph;
        public MemArray<Chunk> chunks;
        public MemAllocatorPtr<List<float3>> from;
        public float3 to;
        public Filter filter;

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