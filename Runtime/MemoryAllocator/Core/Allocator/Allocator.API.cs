namespace ME.BECS.Memory {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(AllocatorDebugProxy))]
    public unsafe partial struct Allocator {

        [INLINE(256)]
        public MemPtr GetSafePtr(byte* ptr, uint zoneId) {
            return new MemPtr(zoneId, (uint)(ptr - this.zones[zoneId].ptr->data.ptr));
        }

        [INLINE(256)]
        public byte* GetPtr(MemPtr ptr) {
            return this.zones[ptr.zoneId].ptr->data.ptr + ptr.offset;
        }

        [INLINE(256)]
        public MemPtr Alloc(uint size) {
            return this.Alloc(size, out _);
        }

        [INLINE(256)]
        public MemPtr Alloc(uint size, out safe_ptr ptr) {
            this.lockSpinner.Lock();
            var memPtr = this.AllocFromFreeBlocks(size, out ptr);
            if (memPtr.IsValid() == true) {
                this.lockSpinner.Unlock();
                return memPtr;
            }

            // create new zone
            var zoneId = this.zonesCount++;
            if (zoneId >= this.zonesCapacity) {
                _resizeArray(this.allocatorLabel, ref this.zones, ref this.zonesCapacity, this.zonesCapacity * 2u);
            }
            this.zones[zoneId] = this.CreateZone(size > this.initialSize ? size : this.initialSize, zoneId);
            memPtr = this.AllocFromFreeBlocks(size, out ptr);
            this.lockSpinner.Unlock();
            return memPtr;
        }

        [INLINE(256)]
        public bool Free(MemPtr ptr) {
            if (ptr.IsValid() == false) return false;
            
            this.lockSpinner.Lock();
            var header = (BlockHeader*)(this.GetPtr(ptr) - sizeof(BlockHeader));
            if (header->freeIndex != uint.MaxValue) {
                this.lockSpinner.Unlock();
                return false;
            }

            var root = this.zones[ptr.zoneId].ptr->data.ptr;
            
            // coalescing with next
            if (header->next != uint.MaxValue) {
                var headerNext = (BlockHeader*)this.GetPtr(new MemPtr(ptr.zoneId, header->next));
                if (headerNext->freeIndex != uint.MaxValue) {
                    this.RemoveFromFree(headerNext);
                    header->size += (uint)sizeof(BlockHeader) + headerNext->size;
                    header->next = headerNext->next;
                }
            }

            // coalescing with prev
            if (header->prev != uint.MaxValue) {
                var prevHeader = (BlockHeader*)this.GetPtr(new MemPtr(ptr.zoneId, header->prev));
                if (prevHeader->freeIndex != uint.MaxValue) {
                    prevHeader->size += header->size + (uint)sizeof(BlockHeader);
                    prevHeader->next = header->next;
                    if (header->next != uint.MaxValue) {
                        var next = (BlockHeader*)(root + header->next);
                        next->prev = header->prev;
                    }
                    this.RemoveFromFree(prevHeader);
                    header = prevHeader;
                }
            }

            {
                // return data to free blocks
                header->freeIndex = (uint)this.freeBlocks.Length;
                this.freeBlocks.Add(this.GetSafePtr((byte*)header, ptr.zoneId));
            }
            
            this.lockSpinner.Unlock();
            return true;
        }

        [INLINE(256)]
        public MemPtr ReAlloc(MemPtr ptr, uint size) {
            var memPtr = this.Alloc(size);
            this.MemMove(ptr, memPtr, size);
            this.Free(ptr);
            return memPtr;
        }

        [INLINE(256)]
        public void MemMove(MemPtr srcPtr, MemPtr dstPtr, uint size) {
            _memmove((safe_ptr)this.GetPtr(srcPtr), (safe_ptr)this.GetPtr(dstPtr), size);
        }

        [INLINE(256)]
        public void MemCpy(MemPtr srcPtr, MemPtr dstPtr, uint size) {
            _memcpy((safe_ptr)this.GetPtr(srcPtr), (safe_ptr)this.GetPtr(dstPtr), size);
        }

        [INLINE(256)]
        public void MemClear(MemPtr ptr, uint size) {
            _memclear((safe_ptr)this.GetPtr(ptr), size);
        }

        [INLINE(256)][NotThreadSafe]
        public void Dispose() {
            if (this.freeBlocks.IsCreated == true) this.freeBlocks.Dispose();
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = this.zones[i];
                if (zone.ptr == null) continue;
                zone.ptr->Dispose();
                _free(zone);
            }
            if (this.zones.ptr != null) _free(this.zones);
        }

    }

}