namespace ME.BECS {

    public partial struct MemoryAllocator {

        private const int ZONE_ID = 0x1d4a11;

        private const int MIN_FRAGMENT = 64;
        
        public const byte BLOCK_STATE_FREE = 0;
        public const byte BLOCK_STATE_USED = 1;
        
        public const long OFFSET_MASK = 0xFFFFFFFF;
        public const long MIN_ZONE_SIZE = 512 * 1024;//128 * 1024;
        public const int MIN_ZONE_SIZE_IN_KB = (int)(MemoryAllocator.MIN_ZONE_SIZE / 1024);
        private const uint MIN_ZONES_LIST_CAPACITY = 1000u;

    }

}
