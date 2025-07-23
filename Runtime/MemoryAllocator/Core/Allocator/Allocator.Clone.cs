namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct MemoryAllocator {
        
        [INLINE(256)]
        public void CopyFrom(in MemoryAllocator other) {
            var allocator = this.allocatorLabel;
            if (allocator == Unity.Collections.Allocator.Invalid) allocator = other.allocatorLabel;
            this.Dispose();
            this.initialSize = other.initialSize;
            this.freeBlocks.Allocator = allocator;
            this.freeBlocks.CopyFrom(other.freeBlocks);
            this.allocatorLabel = other.allocatorLabel;

            this.zonesCapacity = other.zonesCapacity;
            this.zonesCount = other.zonesCount;
            this.version = other.version;

            var newZones = MakeZones(other.zonesCapacity, other.allocatorLabel);
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = other.zones[i];
                var newZone = this.CreateZoneRaw(zone.ptr->size - (uint)sizeof(BlockHeader) - ZONE_HEADER_OFFSET);
                _memmove((safe_ptr)zone.ptr->root.ptr, (safe_ptr)newZone.ptr->root.ptr, zone.ptr->size);
                newZones[i] = newZone;
            }
            this.zones = newZones;

            ++this.version;
        }

        [INLINE(256)]
        public void CopyFromPrepare(in MemoryAllocator other) {
            var allocator = this.allocatorLabel;
            if (allocator == Unity.Collections.Allocator.Invalid) allocator = other.allocatorLabel;
            this.Dispose();
            this.initialSize = other.initialSize;
            this.freeBlocks.Allocator = allocator;
            this.freeBlocks.CopyFrom(other.freeBlocks);
            this.allocatorLabel = other.allocatorLabel;

            this.zonesCapacity = other.zonesCapacity;
            this.zonesCount = other.zonesCount;
            this.version = other.version;
            
            var newZones = MakeZones(other.zonesCapacity, other.allocatorLabel);
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = other.zones[i];
                newZones[i] = this.CreateZoneRaw(zone.ptr->size - (uint)sizeof(BlockHeader) - ZONE_HEADER_OFFSET);
            }
            this.zones = newZones;
            
            ++this.version;
        }

        [INLINE(256)]
        public void CopyFromComplete(in MemoryAllocator other, int index) {
            var zone = other.zones[index];
            var newZone = this.zones[index];
            _memmove((safe_ptr)zone.ptr->root.ptr, (safe_ptr)newZone.ptr->root.ptr, zone.ptr->size);
        }

    }

}