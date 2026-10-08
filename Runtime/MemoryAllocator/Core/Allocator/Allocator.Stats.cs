namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// Allocates relocatable native blocks addressed by zone and offset.
    /// </summary>
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(AllocatorDebugProxy))]
    public unsafe partial struct MemoryAllocator {

        /// <summary>
        /// Returns size.
        /// </summary>
        public readonly uint GetSize(in MemPtr memPtr) {
            if (memPtr.IsValid() == false) return TSize<MemPtr>.size;
            var header = (BlockHeader*)(this.GetPtr(memPtr) - sizeof(BlockHeader));
            return header->size + TSize<MemPtr>.size;
        }

        /// <summary>
        /// Returns size.
        /// </summary>
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

        /// <summary>
        /// Returns reserved size.
        /// </summary>
        public readonly uint GetReservedSize() {
            this.GetSize(out uint reservedSize, out uint usedSize, out uint freeSize);
            return reservedSize;
        }

        /// <summary>
        /// Returns used size.
        /// </summary>
        public readonly uint GetUsedSize() {
            this.GetSize(out uint reservedSize, out uint usedSize, out uint freeSize);
            return usedSize;
        }

        /// <summary>
        /// Returns free size.
        /// </summary>
        public readonly uint GetFreeSize() {
            this.GetSize(out uint reservedSize, out uint usedSize, out uint freeSize);
            return freeSize;
        }

    }

}