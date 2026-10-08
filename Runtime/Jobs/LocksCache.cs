namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Burst;
    using ME.BECS.Internal;
    using static Cuts;

    /// <summary>
    /// Provides shared storage for read/write synchronization.
    /// </summary>
    public class ReadWriteSpinnerShared {

        /// <summary>
        /// Stores synchronization locks associated with a world.
        /// </summary>
        public unsafe struct WorldLocks {

            /// <summary>
            /// Block used by <c>ReadWriteSpinnerShared.WorldLocks</c>.
            /// </summary>
            public safe_ptr block;
            /// <summary>
            /// Spinners used by <c>ReadWriteSpinnerShared.WorldLocks</c>.
            /// </summary>
            public safe_ptr<ReadWriteNativeSpinner> spinners;
            /// <summary>
            /// Number of entries tracked by this value.
            /// </summary>
            public uint count;

            /// <summary>
            /// Whether the backing state has been initialized.
            /// </summary>
            public bool IsCreated => this.block.ptr != null;

            /// <summary>
            /// Releases the resources owned by this world locks instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                if (this.block.ptr != null) _free(this.block, Constants.ALLOCATOR_DOMAIN);
                this = default;
            }

        }

        /// <summary>
        /// Caches  data for reuse.
        /// </summary>
        public struct Cache {

            /// <summary>
            /// Category lengths used by <c>ReadWriteSpinnerShared.Cache</c>.
            /// </summary>
            public Array<uint> categoryLengths;
            /// <summary>
            /// Category offsets used by <c>ReadWriteSpinnerShared.Cache</c>.
            /// </summary>
            public Array<uint> categoryOffsets;
            /// <summary>
            /// Worlds used by <c>ReadWriteSpinnerShared.Cache</c>.
            /// </summary>
            public Array<WorldLocks> worlds;
            /// <summary>
            /// Locks count for the associated storage.
            /// </summary>
            public uint locksCount;

        }

        // Each world owns one stable block: [all spinner structs][thread-major read counters].
        /// <summary>
        /// Cached data reused by the associated operation.
        /// </summary>
        public static readonly SharedStatic<Cache> cache = SharedStatic<Cache>.GetOrCreate<ReadWriteSpinnerShared>();

    }
    
    /// <summary>
    /// Caches locks data for reuse.
    /// </summary>
    public static unsafe class LocksCache {

        /// <summary>
        /// Max id used to locate the associated entry.
        /// </summary>
        public const uint MAX_ID = 3u;
        
        /// <summary>
        /// Component storage or descriptors used by this operation.
        /// </summary>
        public const uint COMPONENTS = 1u;
        /// <summary>
        /// Ent groups constant used by <c>LocksCache</c>.
        /// </summary>
        public const uint ENT_GROUPS = 2u;

        [INLINE(256)]
        private static void EnsureLayout() {
            ref var cache = ref ReadWriteSpinnerShared.cache.Data;
            var componentsCount = StaticTypes.counter + 1u;
            var entityGroupsCount = EntityTypes.groupsCount;
            if (cache.categoryLengths.Length >= MAX_ID &&
                cache.categoryLengths.Get(COMPONENTS) == componentsCount &&
                cache.categoryLengths.Get(ENT_GROUPS) == entityGroupsCount) return;

            Initialize(COMPONENTS, componentsCount);
            Initialize(ENT_GROUPS, entityGroupsCount);
        }

        /// <summary>
        /// Initializes locks cache state from the supplied context.
        /// </summary>
        [INLINE(256)]
        public static void Initialize(uint groupId, uint maxIndex) {
            ref var cache = ref ReadWriteSpinnerShared.cache.Data;
            if (cache.categoryLengths.Length < MAX_ID) {
                cache.categoryLengths.Resize(MAX_ID);
                cache.categoryOffsets.Resize(MAX_ID);
            }

            cache.categoryLengths.Get(groupId) = maxIndex;
            cache.locksCount = 0u;
            for (uint i = 0u; i < MAX_ID; ++i) {
                cache.categoryOffsets.Get(i) = cache.locksCount;
                cache.locksCount += cache.categoryLengths.Get(i);
            }
        }

        /// <summary>
        /// Adds world.
        /// </summary>
        [INLINE(256)]
        public static void AddWorld(ushort worldId) {
            EnsureLayout();
            ref var cache = ref ReadWriteSpinnerShared.cache.Data;
            if (worldId >= cache.worlds.Length) cache.worlds.Resize((uint)worldId + 1u);

            ref var world = ref cache.worlds.Get(worldId);
            if (world.IsCreated == true) return;

            var threadsCount = JobUtils.ThreadsCount;
            if (threadsCount == 0u) threadsCount = 1u;
            var spinnersSize = TSize<ReadWriteNativeSpinner>.size * cache.locksCount;
            var countersOffset = Bitwise.AlignUp(spinnersSize, JobUtils.CacheLineSize);
            var countersStride = Bitwise.AlignUp(TSize<int>.size * cache.locksCount, JobUtils.CacheLineSize);
            var totalSize = countersOffset + countersStride * threadsCount;
            var block = _calloc((int)totalSize, (int)JobUtils.CacheLineSize, Constants.ALLOCATOR_DOMAIN);
            var spinners = new safe_ptr<ReadWriteNativeSpinner>((ReadWriteNativeSpinner*)block.ptr, spinnersSize);
            var counters = block + countersOffset;

            for (uint i = 0u; i < cache.locksCount; ++i) {
                spinners[i] = ReadWriteNativeSpinner.Create(counters + TSize<int>.size * i, threadsCount, countersStride);
            }

            world = new ReadWriteSpinnerShared.WorldLocks() {
                block = block,
                spinners = spinners,
                count = cache.locksCount,
            };
        }

        /// <summary>
        /// Releases the resources registered for the specified world.
        /// </summary>
        [INLINE(256)]
        public static void DisposeWorld(ushort worldId) {
            ref var worlds = ref ReadWriteSpinnerShared.cache.Data.worlds;
            if (worldId >= worlds.Length) return;
            worlds.Get(worldId).Dispose();
        }

        /// <summary>
        /// Releases the resources owned by this locks cache instance.
        /// </summary>
        [INLINE(256)]
        public static void Dispose() {
            ref var cache = ref ReadWriteSpinnerShared.cache.Data;
            for (uint i = 0u; i < cache.worlds.Length; ++i) {
                cache.worlds.Get(i).Dispose();
            }
            cache.worlds.Dispose();
            cache.categoryOffsets.Dispose();
            cache.categoryLengths.Dispose();
            cache = default;
        }

        /// <summary>
        /// Returns read write spinner.
        /// </summary>
        [INLINE(256)]
        public static ref ReadWriteNativeSpinner GetReadWriteSpinner(ushort worldId, uint groupId, uint index) {
            ref var cache = ref ReadWriteSpinnerShared.cache.Data;
            E.RANGE(groupId, 0u, cache.categoryLengths.Length);
            E.RANGE(index, 0u, cache.categoryLengths.Get(groupId));
            var flatIndex = cache.categoryOffsets.Get(groupId) + index;
            ref var world = ref cache.worlds.Get(worldId);
            E.RANGE(flatIndex, 0u, world.count);
            return ref world.spinners[flatIndex];
        }

    }

}
