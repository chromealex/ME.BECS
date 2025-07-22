namespace ME.BECS.Memory {
    
    using Unity.Collections.LowLevel.Unsafe;

    public partial struct Allocator {

        public safe_ptr<safe_ptr<Zone>> zones;
        public uint zonesCapacity;
        public uint zonesCount;
        public uint initialSize;
        public UnsafeList<MemPtr> freeBlocks;
        private Unity.Collections.Allocator allocatorLabel;
        public LockSpinner lockSpinner;

    }

}