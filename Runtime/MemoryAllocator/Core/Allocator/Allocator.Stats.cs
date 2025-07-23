namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(AllocatorDebugProxy))]
    public unsafe partial struct MemoryAllocator {

        public readonly uint GetSize(in MemPtr memPtr) {
            if (memPtr.IsValid() == false) return TSize<MemPtr>.size;
            var header = (BlockHeader*)(this.GetPtr(memPtr) - sizeof(BlockHeader));
            return header->size + TSize<MemPtr>.size;
        }

        public readonly void GetSize(out uint reservedSize, out uint usedSize, out uint freeSize) {
            freeSize = 0u;
            reservedSize = 0u;
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = this.zones[i];
                reservedSize += zone.ptr->size;
            }

            freeSize += this.freeBlocks.GetSize(in this);
            
            usedSize = reservedSize - freeSize;
        }

        public readonly uint GetReservedSize() {
            this.GetSize(out uint reservedSize, out uint usedSize, out uint freeSize);
            return reservedSize;
        }

        public readonly uint GetUsedSize() {
            this.GetSize(out uint reservedSize, out uint usedSize, out uint freeSize);
            return usedSize;
        }

        public readonly uint GetFreeSize() {
            this.GetSize(out uint reservedSize, out uint usedSize, out uint freeSize);
            return freeSize;
        }

    }

}