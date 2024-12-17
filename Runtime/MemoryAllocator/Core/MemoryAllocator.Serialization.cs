namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    public unsafe partial struct MemoryAllocator {

        public void Deserialize(ref StreamBufferReader stream) {

            var allocator = new MemoryAllocator();
            stream.Read(ref allocator.version);
            stream.Read(ref allocator.zonesListCount);
            stream.Read(ref allocator.zonesListCapacity);
            stream.Read(ref allocator.initialSize);
            
            allocator.zonesList = (MemoryAllocator.MemZone**)_make(allocator.zonesListCapacity * (uint)sizeof(MemoryAllocator.MemZone*), TAlign<System.IntPtr>.alignInt, Constants.ALLOCATOR_PERSISTENT).ptr;
            _memclear((safe_ptr)allocator.zonesList, sizeof(MemoryAllocator.MemZone*) * allocator.zonesListCapacity);

            for (int i = 0; i < allocator.zonesListCount; ++i) {
                var length = 0;
                stream.Read(ref length);
                if (length == 0) continue;
                var zone = MemoryAllocator.ZmCreateZone(length);
                allocator.zonesList[i] = zone;
                var readSize = length;
                var zn = (byte*)zone;
                stream.Read(ref zn, (uint)readSize);
            }

            this = allocator;

        }

        public readonly void Serialize(ref StreamBufferWriter stream) {
            
            stream.Write(this.version);
            stream.Write(this.zonesListCount);
            stream.Write(this.zonesListCapacity);
            stream.Write(this.initialSize);

            for (int i = 0; i < this.zonesListCount; ++i) {
                var zone = this.zonesList[i];
                if (zone == null) {
                    stream.Write(0);
                    continue;
                }
                stream.Write(zone->size);
                if (zone->size == 0) continue;
                var writeSize = zone->size;
                stream.Write((byte*)zone, (uint)writeSize);
                //System.Runtime.InteropServices.Marshal.Copy((System.IntPtr)zone, buffer, pos, writeSize);
            }
        }

    }

}
