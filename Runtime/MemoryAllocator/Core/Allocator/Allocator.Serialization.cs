namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct MemoryAllocator {

        [INLINE(256)]
        public void Serialize(ref StreamBufferWriter writer) {
            writer.Write(this.zonesCapacity);
            writer.Write(this.zonesCount);
            writer.Write(this.initialSize);
            writer.Write(this.version);
            {
                this.freeBlocks.Serialize(ref writer);
            }
            
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = this.zones[i];
                if (zone.ptr == null) {
                    writer.Write(0u);
                    continue;
                }
                writer.Write(zone.ptr->size);
                writer.Write(zone.ptr->root.ptr, zone.ptr->size);
            }
        }

        [INLINE(256)]
        public void Deserialize(ref StreamBufferReader reader) {
            reader.Read(ref this.zonesCapacity);
            reader.Read(ref this.zonesCount);
            reader.Read(ref this.initialSize);
            reader.Read(ref this.version);
            {
                this.freeBlocks.Deserialize(ref reader, this.allocatorLabel);
            }

            this.zones = MakeZones(this.zonesCapacity, this.allocatorLabel);
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var size = 0u;
                reader.Read(ref size);
                if (size == 0u) continue;
                var zone = this.CreateZoneRaw(size);
                zone.ptr->size = size;
                var bytes = zone.ptr->root.ptr;
                reader.Read(ref bytes, size);
                *(zone.ptr->root.ptr) = *bytes;
                this.zones[i] = zone;
            }

            ++this.version;
        }

    }

}