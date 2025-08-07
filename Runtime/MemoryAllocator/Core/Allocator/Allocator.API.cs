namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(AllocatorDebugProxy))]
    public unsafe partial struct MemoryAllocator {

        [INLINE(256)]
        public readonly MemPtr GetSafePtr(byte* ptr, uint zoneId) {
            return new MemPtr(zoneId, (uint)(ptr - this.zones[zoneId].ptr->root.ptr));
        }

        [INLINE(256)]
        public readonly byte* GetPtr(in MemPtr ptr) {
            return this.zones[ptr.zoneId].ptr->root.ptr + ptr.offset;
        }

        [INLINE(256)]
        public readonly byte* GetPtr(in MemPtr ptr, uint offset) {
            return this.zones[ptr.zoneId].ptr->root.ptr + ptr.offset + offset;
        }

        [INLINE(256)]
        public readonly byte* GetPtr(in MemPtr ptr, ulong offset) {
            return this.zones[ptr.zoneId].ptr->root.ptr + ptr.offset + offset;
        }

        [INLINE(256)]
        public readonly byte* GetPtr(in MemPtr ptr, int offset) {
            return this.zones[ptr.zoneId].ptr->root.ptr + ptr.offset + offset;
        }

        [INLINE(256)]
        public MemPtr Alloc(uint size) {
            return this.Alloc(size, out _);
        }

        [INLINE(256)]
        public MemPtr Alloc(uint size, out safe_ptr ptr) {
            size = Align(size);
            this.lockSpinner.Lock();
            var memPtr = this.AllocFromFreeBlocks(size, out ptr);
            if (memPtr.IsValid() == true) {
                this.lockSpinner.Unlock();
                LeakDetector.Track(ptr);
                return memPtr;
            }

            // create new zone
            var zoneId = this.zonesCount++;
            if (zoneId >= this.zonesCapacity) {
                _resizeArray(this.allocatorLabel, ref this.zones, ref this.zonesCapacity, this.zonesCapacity * 2u);
            }
            this.zones[zoneId] = this.CreateZone(size, zoneId);
            memPtr = this.AllocFromFreeBlocks(size, out ptr);
            this.lockSpinner.Unlock();
            LeakDetector.Track(ptr);
            MemoryAllocator.CheckConsistency(ref this);
            return memPtr;
        }

        [INLINE(256)]
        public bool Free(in MemPtr ptr) {
            if (ptr.IsValid() == false) return false;
            CheckPtr(in this, ptr);
            
            var header = (BlockHeader*)(this.GetPtr(ptr) - sizeof(BlockHeader));
            if (header->freeIndex != uint.MaxValue) {
                return false;
            }

            LeakDetector.Free(this.GetPtr(ptr));
            
            var root = this.zones[ptr.zoneId].ptr->root.ptr;
            
            this.lockSpinner.Lock();
            // coalescing with next
            if (header->next != uint.MaxValue) {
                var headerNext = (BlockHeader*)this.GetPtr(new MemPtr(ptr.zoneId, header->next));
                if (headerNext->freeIndex != uint.MaxValue) {
                    this.RemoveFromFree(headerNext);
                    header->size += (uint)sizeof(BlockHeader) + headerNext->size;
                    header->next = headerNext->next;
                    if (header->next != uint.MaxValue) {
                        var next = (BlockHeader*)(root + header->next);
                        next->prev = ptr.offset - (uint)sizeof(BlockHeader);
                    }
                }
            }

            // coalescing with prev
            if (header->prev != uint.MaxValue) {
                var prevHeader = (BlockHeader*)this.GetPtr(new MemPtr(ptr.zoneId, header->prev));
                if (prevHeader->freeIndex != uint.MaxValue) {
                    this.RemoveFromFree(prevHeader);
                    prevHeader->size += header->size + (uint)sizeof(BlockHeader);
                    prevHeader->next = header->next;
                    if (header->next != uint.MaxValue) {
                        var next = (BlockHeader*)(root + header->next);
                        next->prev = header->prev;
                    }
                    header = prevHeader;
                }
            }
            
            // add to free blocks
            {
                this.freeBlocks.Add(in this, header, ptr.zoneId);
            }
            this.lockSpinner.Unlock();
            
            MemoryAllocator.CheckConsistency(ref this);
            return true;
        }

        [INLINE(256)]
        public MemPtr ReAlloc(MemPtr ptr, uint size) {
            return this.ReAlloc(ptr, size, out _);
        }

        [INLINE(256)]
        public MemPtr ReAlloc(MemPtr memPtr, uint size, out safe_ptr ptr) {
            CheckPtr(in this, memPtr);
            if (memPtr.IsValid() == false) {
                return this.Alloc(size, out ptr);
            }
            var header = (BlockHeader*)(this.GetPtr(memPtr) - sizeof(BlockHeader));
            this.lockSpinner.Lock();
            if (size <= header->size) {
                // if current block has valid space size - use it without re-alloc
                this.lockSpinner.Unlock();
                ptr = (safe_ptr)this.GetPtr(memPtr);
                return memPtr;
            }

            if (header->next != uint.MaxValue) {
                // if next block is free, and sum is enough to fit the object
                var root = this.zones[memPtr.zoneId].ptr->root.ptr;
                var requiredSize = size - header->size;
                var nextHeader = (BlockHeader*)(this.zones[memPtr.zoneId].ptr->root.ptr + header->next);
                var blockSize = nextHeader->size + (uint)sizeof(BlockHeader);
                if (nextHeader->freeIndex != uint.MaxValue && requiredSize <= blockSize) {
                    // remove next block from free list
                    this.RemoveFromFree(nextHeader);
                    var tailSize = nextHeader->size > requiredSize + sizeof(BlockHeader) + sizeof(BlockHeader) ? nextHeader->size - requiredSize : 0u;
                    if (tailSize == 0u) {
                        // use full block
                        // resize current header and eliminate the next block
                        header->size += nextHeader->size + (uint)sizeof(BlockHeader); 
                        header->next = nextHeader->next;
                        if (nextHeader->next != uint.MaxValue) {
                            var nextNextHeader = (BlockHeader*)(root + nextHeader->next);
                            nextNextHeader->prev = nextHeader->prev;
                        }
                    } else {
                        // split blocks
                        var next = nextHeader->next;
                        var nextSize = nextHeader->size - requiredSize;
                        
                        // move header
                        var newHeader = (BlockHeader*)((byte*)header + (uint)sizeof(BlockHeader) + header->size + requiredSize);
                        *newHeader = *nextHeader;
                        newHeader->prev = memPtr.offset - (uint)sizeof(BlockHeader);
                        newHeader->next = next;
                        newHeader->size = nextSize;
                        
                        header->size += requiredSize;
                        header->next += requiredSize;
                        if (newHeader->next != uint.MaxValue) {
                            var nextNextHeader = (BlockHeader*)(root + newHeader->next);
                            nextNextHeader->prev += requiredSize;
                        }
                        
                        // add to free blocks
                        this.freeBlocks.Add(in this, newHeader, memPtr.zoneId);
                    }

                    this.lockSpinner.Unlock();
                    ptr = (safe_ptr)this.GetPtr(memPtr);
                    return memPtr;
                }
            }
            this.lockSpinner.Unlock();
            
            var newMemPtr = this.Alloc(size, out ptr);
            this.MemMove(newMemPtr, memPtr, header->size);
            this.Free(memPtr);
            return newMemPtr;
        }

        [INLINE(256)]
        public readonly void MemMove(MemPtr dstPtr, MemPtr srcPtr, uint size) {
            CheckPtr(in this, dstPtr);
            CheckPtr(in this, srcPtr);
            _memmove((safe_ptr)this.GetPtr(srcPtr), (safe_ptr)this.GetPtr(dstPtr), size);
        }

        [INLINE(256)]
        public readonly void MemMove(MemPtr dstPtr, uint dstIndex, MemPtr srcPtr, uint srcIndex, uint size) {
            CheckPtr(in this, dstPtr);
            CheckPtr(in this, srcPtr);
            _memmove((safe_ptr)this.GetPtr(srcPtr, srcIndex), (safe_ptr)this.GetPtr(dstPtr, dstIndex), size);
        }

        [INLINE(256)]
        public readonly void MemMove(MemPtr dstPtr, int dstIndex, MemPtr srcPtr, int srcIndex, int size) {
            CheckPtr(in this, dstPtr);
            CheckPtr(in this, srcPtr);
            _memmove((safe_ptr)this.GetPtr(srcPtr, srcIndex), (safe_ptr)this.GetPtr(dstPtr, dstIndex), size);
        }

        [INLINE(256)]
        public readonly void MemCopy(MemPtr dstPtr, MemPtr srcPtr, uint size) {
            CheckPtr(in this, dstPtr);
            CheckPtr(in this, srcPtr);
            _memcpy((safe_ptr)this.GetPtr(srcPtr), (safe_ptr)this.GetPtr(dstPtr), size);
        }

        [INLINE(256)]
        public readonly void MemCopy(MemPtr dstPtr, uint dstIndex, MemPtr srcPtr, uint srcIndex, uint size) {
            CheckPtr(in this, dstPtr);
            CheckPtr(in this, srcPtr);
            _memcpy((safe_ptr)this.GetPtr(srcPtr, srcIndex), (safe_ptr)this.GetPtr(dstPtr, dstIndex), size);
        }

        [INLINE(256)]
        public readonly void MemClear(MemPtr ptr, uint size) {
            CheckPtr(in this, ptr);
            _memclear((safe_ptr)this.GetPtr(ptr), size);
        }

        [INLINE(256)]
        public readonly void MemClear(MemPtr ptr, uint offset, uint size) {
            CheckPtr(in this, ptr);
            _memclear((safe_ptr)this.GetPtr(ptr, offset), size);
        }

        [INLINE(256)]
        public readonly void MemClear(MemPtr ptr, uint offset, int size) {
            CheckPtr(in this, ptr);
            _memclear((safe_ptr)this.GetPtr(ptr, offset), size);
        }

        [INLINE(256)]
        public readonly void MemClear(MemPtr ptr, uint offset, long size) {
            CheckPtr(in this, ptr);
            _memclear((safe_ptr)this.GetPtr(ptr, offset), size);
        }

        [INLINE(256)]
        public readonly void MemClear(MemPtr ptr, ulong offset, uint size) {
            CheckPtr(in this, ptr);
            _memclear((safe_ptr)this.GetPtr(ptr, offset), size);
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
            this = default;
        }

    }

}