namespace ME.BECS.Memory {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct Allocator {
        
        [INLINE(256)]
        public void CopyFrom(in Allocator other) {
            this.Dispose();
            this.initialSize = other.initialSize;
            this.freeBlocks.CopyFrom(other.freeBlocks);
            this.allocatorLabel = other.allocatorLabel;

            this.zonesCapacity = other.zonesCapacity;
            this.zonesCount = other.zonesCount;

            var newZones = MakeZones(other.zonesCapacity, other.allocatorLabel);
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = other.zones[i];
                var newZone = this.CreateZoneRaw(zone.ptr->size);
                _memmove((safe_ptr)zone.ptr->data.ptr, (safe_ptr)newZone.ptr->data.ptr, zone.ptr->size);
                newZones[i] = newZone;
            }
            this.zones = newZones;
        }

        [INLINE(256)]
        public void CopyFromPrepare(in Allocator other) {
            this.Dispose();
            this.initialSize = other.initialSize;
            this.freeBlocks.CopyFrom(other.freeBlocks);
            this.allocatorLabel = other.allocatorLabel;

            this.zonesCapacity = other.zonesCapacity;
            this.zonesCount = other.zonesCount;
            
            var newZones = MakeZones(other.zonesCapacity, other.allocatorLabel);
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = other.zones[i];
                newZones[i] = this.CreateZoneRaw(zone.ptr->size);
            }
            this.zones = newZones;
        }

        [INLINE(256)]
        public void CopyFromComplete(in Allocator other, int index) {
            var zone = other.zones[index];
            var newZone = this.zones[index];
            _memmove((safe_ptr)zone.ptr->data.ptr, (safe_ptr)newZone.ptr->data.ptr, zone.ptr->size);
        }

    }

}