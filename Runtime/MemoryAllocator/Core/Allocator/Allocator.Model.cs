namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// Allocates relocatable native blocks addressed by zone and offset.
    /// </summary>
    public partial struct MemoryAllocator {

        /// <summary>
        /// Zones used by <c>MemoryAllocator</c>.
        /// </summary>
        public safe_ptr<safe_ptr<Zone>> zones;
        /// <summary>
        /// Zones capacity for the associated storage.
        /// </summary>
        public uint zonesCapacity;
        /// <summary>
        /// Zones count for the associated storage.
        /// </summary>
        public uint zonesCount;
        /// <summary>
        /// Initial size used by <c>MemoryAllocator</c>.
        /// </summary>
        public uint initialSize;
        /// <summary>
        /// Free allocator blocks available for reuse.
        /// </summary>
        public FreeBlocks freeBlocks;
        private Unity.Collections.Allocator allocatorLabel;
        /// <summary>
        /// Change version used to detect stale state.
        /// </summary>
        public ushort version;
        /// <summary>
        /// Spin lock protecting concurrent access to this state.
        /// </summary>
        public LockSpinner lockSpinner;

    }

}