namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;

    public partial struct MemoryAllocator {

        public safe_ptr<safe_ptr<Zone>> zones;
        public uint zonesCapacity;
        public uint zonesCount;
        public uint initialSize;
        public FreeBlocks freeBlocks;
        private Unity.Collections.Allocator allocatorLabel;
        public ushort version;
        public LockSpinner lockSpinner;

    }

}