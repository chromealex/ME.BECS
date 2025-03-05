//#define MEMORY_ALLOCATOR_BOUNDS_CHECK
//#define LOGS_ENABLED
//#define ALLOCATOR_VALIDATION

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using math = Unity.Mathematics.math;
    using static Cuts;

    public unsafe partial struct MemoryAllocator {

        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr(in MemPtr ptr, uint offset = 0U) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (ptr.zoneId < this.zonesListCount && this.zonesList[ptr.zoneId] != null && this.zonesList[ptr.zoneId]->size < ptr.offset) {
                throw new System.Exception();
            }
            #endif
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            return new safe_ptr((byte*)this.zonesList[ptr.zoneId] + ptr.offset + offset, ((MemBlock*)((byte*)this.zonesList[ptr.zoneId] + ptr.offset - TSize<MemBlock>.size))->size);
            #else
            return new safe_ptr((byte*)this.zonesList[ptr.zoneId] + ptr.offset + offset);
            #endif
        }

        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr(in MemPtr ptr, long offset) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            return new safe_ptr((byte*)this.zonesList[ptr.zoneId] + ptr.offset + offset, ((MemBlock*)((byte*)this.zonesList[ptr.zoneId] + ptr.offset - TSize<MemBlock>.size))->size);
            #else
            return new safe_ptr((byte*)this.zonesList[ptr.zoneId] + ptr.offset + offset);
            #endif
        }

        [INLINE(256)]
        public MemPtr ReAlloc(in MemPtr ptr, int size) {

            return this.ReAlloc(ptr, size, out _);

        }

        [INLINE(256)]
        public MemPtr ReAlloc(in MemPtr ptr, int size, out safe_ptr voidPtr) {

            size = MemoryAllocator.Align(size);
            
            if (ptr.IsValid() == false) return this.Alloc(size, out voidPtr);

            MemoryAllocator.ValidateConsistency(ref this);
            JobUtils.Lock(ref this.lockIndex);

            voidPtr = this.GetUnsafePtr(ptr);
            var block = (MemoryAllocator.MemBlock*)((byte*)voidPtr.ptr - TSize<MemoryAllocator.MemBlock>.size);
            var blockSize = block->size;
            var blockDataSize = blockSize - TSize<MemoryAllocator.MemBlock>.sizeInt;
            if (blockDataSize > size) {
                JobUtils.Unlock(ref this.lockIndex);
                MemoryAllocator.ValidateConsistency(ref this);
                return ptr;
            }

            if (blockDataSize < 0) {
                JobUtils.Unlock(ref this.lockIndex);
                MemoryAllocator.ValidateConsistency(ref this);
                throw new System.Exception();
            }

            {
                var zone = this.zonesList[ptr.zoneId];
                var nextBlock = block->next.Ptr(zone);
                var requiredSize = size - blockDataSize;
                // next block is free and its size is enough for current size
                if (nextBlock != null &&
                    nextBlock->state == MemoryAllocator.BLOCK_STATE_FREE &&
                    nextBlock->size - TSize<MemoryAllocator.MemBlock>.sizeInt > requiredSize) {
                    // mark current block as free
                    // freePrev is false because it must not collapse block with previous one
                    // [!] may be we need to add case, which move data on collapse
                    if (MemoryAllocator.ZmFree(zone, (byte*)block + TSize<MemoryAllocator.MemBlock>.size, freePrev: false) == false) {
                        // Something went wrong
                        JobUtils.Unlock(ref this.lockIndex);
                        MemoryAllocator.ValidateConsistency(ref this);
                        throw new System.Exception();
                    }
                    // alloc block again
                    var newPtr = MemoryAllocator.ZmAlloc(zone, block, size + TSize<MemoryAllocator.MemBlock>.sizeInt);
                    #if MEMORY_ALLOCATOR_BOUNDS_CHECK
                    {
                        var memPtr = this.GetSafePtr(newPtr, ptr.zoneId);
                        if (memPtr != ptr) {
                            // Something went wrong
                            JobUtils.Unlock(ref this.lockIndex);
                            throw new System.Exception();
                        }
                    }
                    #endif
                    voidPtr = new safe_ptr(newPtr, size + TSize<MemoryAllocator.MemBlock>.sizeInt);
                    JobUtils.Unlock(ref this.lockIndex);
                    MemoryAllocator.ValidateConsistency(ref this);
                    return ptr;
                }
            }
            
            JobUtils.Unlock(ref this.lockIndex);
            MemoryAllocator.ValidateConsistency(ref this);

            {
                var newPtr = this.Alloc(size, out voidPtr);
                this.MemMove(newPtr, 0, ptr, 0, blockDataSize);
                this.Free(ptr);

                return newPtr;
            }

        }

        [INLINE(256)]
        public MemPtr Alloc(long size) {

            return this.Alloc(size, out _);

        }

        [INLINE(256)]
        public MemPtr Alloc(long size, out safe_ptr ptr) {

            size = MemoryAllocator.Align(size);
            
            MemoryAllocator.ValidateConsistency(ref this);

            JobUtils.Lock(ref this.lockIndex);
            
            for (uint i = 0u, cnt = this.zonesListCount; i < cnt; ++i) {
                var zone = this.zonesList[i];
                if (zone == null) continue;

                ptr = new safe_ptr(MemoryAllocator.ZmMalloc(zone, (int)size), (uint)size);
                if (ptr.ptr != null) {
                    var memPtr = this.GetSafePtr(ptr.ptr, i);
                    #if LOGS_ENABLED
                    MemoryAllocator.LogAdd(memPtr, size);
                    #endif
                    JobUtils.Unlock(ref this.lockIndex);
                    MemoryAllocator.ValidateConsistency(ref this);

                    return memPtr;
                }
                
            }

            {

                var zone = MemoryAllocator.ZmCreateZone((int)math.max(size, this.initialSize));
                var zoneIndex = this.AddZone(zone);
                ptr = new safe_ptr(MemoryAllocator.ZmMalloc(zone, (int)size), (uint)size);
                var memPtr = this.GetSafePtr(ptr.ptr, zoneIndex);
                #if LOGS_ENABLED
                MemoryAllocator.LogAdd(memPtr, size);
                #endif
                
                JobUtils.Unlock(ref this.lockIndex);
                MemoryAllocator.ValidateConsistency(ref this);

                return memPtr;
            }

        }

        [INLINE(256)]
        public bool Free(in MemPtr ptr) {

            if (ptr.IsValid() == false) return false;

            MemoryAllocator.ValidateConsistency(ref this);

            JobUtils.Lock(ref this.lockIndex);

            var zoneIndex = ptr.zoneId; //ptr >> 32;

            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (zoneIndex >= this.zonesListCount || this.zonesList[zoneIndex] == null) {
                throw new System.Exception();
            }
            #endif

            var zone = this.zonesList[zoneIndex];

            #if LOGS_ENABLED
            if (startLog == true) {
                MemoryAllocator.LogRemove(ptr);
            }
            #endif

            var success = false;
            if (zone != null) {

                var srcPtr = this.GetUnsafePtr(ptr).ptr;
                success = MemoryAllocator.ZmFree(zone, srcPtr);

                if (MemoryAllocator.IsEmptyZone(zone) == true) {
                    MemoryAllocator.ZmFreeZone(zone);
                    this.zonesList[zoneIndex] = null;
                }
            }

            JobUtils.Unlock(ref this.lockIndex);

            MemoryAllocator.ValidateConsistency(ref this);

            return success;
        }

        public uint GetSize(in MemPtr ptr) {

            var block = (MemBlock*)((byte*)this.zonesList[ptr.zoneId] + ptr.offset - sizeof(MemBlock));
            return (uint)block->size;

        }
        
        [INLINE(256)]
        public readonly void GetSize(out int reservedSize, out int usedSize, out int freeSize) {

            usedSize = 0;
            reservedSize = 0;
            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                if (zone != null) {
                    reservedSize += zone->size;
                    usedSize = reservedSize;
                    usedSize -= MemoryAllocator.GetZmFreeMemory(zone);
                }
            }

            freeSize = reservedSize - usedSize;

        }

        [INLINE(256)]
        public readonly int GetReservedSize() {

            var size = 0;
            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                if (zone != null) {
                    size += zone->size;
                }
            }

            return size;

        }

        [INLINE(256)]
        public readonly int GetUsedSize() {

            var size = 0;
            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                if (zone != null) {
                    size += zone->size;
                    size -= MemoryAllocator.GetZmFreeMemory(zone);
                }
            }

            return size;

        }

        [INLINE(256)]
        public readonly int GetFreeSize() {

            var size = 0;
            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                if (zone != null) {
                    size += MemoryAllocator.GetZmFreeMemory(zone);
                }
            }

            return size;

        }

        /// 
        /// Base
        ///
        
        [INLINE(256)]
        public readonly ref T Ref<T>(in MemPtr ptr) where T : unmanaged {
            return ref *((safe_ptr<T>)this.GetUnsafePtr(ptr)).ptr;
        }

        [INLINE(256)]
        public readonly ref T Ref<T>(MemPtr ptr) where T : unmanaged {
            return ref *((safe_ptr<T>)this.GetUnsafePtr(ptr)).ptr;
        }

        [INLINE(256)]
        public MemPtr AllocData<T>(T data) where T : unmanaged {
            var ptr = this.Alloc<T>();
            this.Ref<T>(ptr) = data;
            return ptr;
        }

        [INLINE(256)]
        public MemPtr Alloc<T>() where T : struct {
            var size = TSize<T>.size;
            var alignOf = TAlign<T>.align;
            return this.Alloc(size + alignOf);
        }

        [INLINE(256)]
        public readonly void MemCopy(in MemPtr dest, long destOffset, in MemPtr source, long sourceOffset, long length) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            var destZoneIndex = dest.zoneId;
            var sourceZoneIndex = source.zoneId;
            var destMaxOffset = dest.offset + destOffset + length;
            var sourceMaxOffset = source.offset + sourceOffset + length;
            
            if (destZoneIndex >= this.zonesListCount || sourceZoneIndex >= this.zonesListCount) {
                throw new System.Exception();
            }
            
            if (this.zonesList[destZoneIndex]->size < destMaxOffset || this.zonesList[sourceZoneIndex]->size < sourceMaxOffset) {
                throw new System.Exception();
            }
            #endif
            
            _memcpy(this.GetUnsafePtr(source, sourceOffset), this.GetUnsafePtr(dest, destOffset), length);
            
        }

        [INLINE(256)]
        public readonly void MemMove(in MemPtr dest, long destOffset, in MemPtr source, long sourceOffset, long length) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            var destZoneIndex = dest.zoneId;
            var sourceZoneIndex = source.zoneId;
            var destMaxOffset = dest.offset + destOffset + length;
            var sourceMaxOffset = source.offset + sourceOffset + length;
            
            if (destZoneIndex >= this.zonesListCount || sourceZoneIndex >= this.zonesListCount) {
                throw new System.Exception();
            }
            
            if (this.zonesList[destZoneIndex]->size < destMaxOffset || this.zonesList[sourceZoneIndex]->size < sourceMaxOffset) {
                throw new System.Exception();
            }
            #endif
            
            _memmove(this.GetUnsafePtr(source, sourceOffset), this.GetUnsafePtr(dest, destOffset), length);
            
        }

        [INLINE(256)]
        public readonly void MemClear(in MemPtr dest, long destOffset, long length) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            var zoneIndex = dest.zoneId;
            
            if (zoneIndex >= this.zonesListCount || this.zonesList[zoneIndex]->size < (dest.offset + destOffset + length)) {
                throw new System.Exception();
            }
            #endif

            _memclear(this.GetUnsafePtr(dest, destOffset), length);
            
        }

        [INLINE(256)]
        public void Prepare(long size) {

            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                
                if (zone == null) continue;

                if (MemoryAllocator.ZmHasFreeBlock(zone, (int)size) == true) {
                    return;
                }
            }
 
            this.AddZone(MemoryAllocator.ZmCreateZone((int)math.max(size, this.initialSize)));
            
        }

        /// 
        /// Arrays
        /// 
        [INLINE(256)]
        public readonly MemPtr RefArrayPtr<T>(in MemPtr ptr, int index) where T : unmanaged {
            var size = TSize<T>.size;
            return new MemPtr(ptr.zoneId, ptr.offset + (uint)index * size);
        }
        
        [INLINE(256)]
        public readonly MemPtr RefArrayPtr<T>(in MemPtr ptr, uint index) where T : unmanaged {
            var size = TSize<T>.size;
            return new MemPtr(ptr.zoneId, ptr.offset + index * size);
        }
        
        [INLINE(256)]
        public readonly ref T RefArray<T>(in MemPtr ptr, int index) where T : unmanaged {
            var size = TSize<T>.size;
            return ref *((safe_ptr<T>)this.GetUnsafePtr(in ptr, index * size)).ptr;
        }

        [INLINE(256)]
        public readonly ref T RefArray<T>(MemPtr ptr, int index) where T : unmanaged {
            var size = TSize<T>.size;
            return ref *((safe_ptr<T>)this.GetUnsafePtr(in ptr, index * size)).ptr;
        }

        [INLINE(256)]
        public readonly ref T RefArray<T>(in MemPtr ptr, uint index) where T : unmanaged {
            var size = TSize<T>.size;
            return ref *((safe_ptr<T>)this.GetUnsafePtr(in ptr, index * size)).ptr;
        }

        [INLINE(256)]
        public readonly ref T RefArray<T>(MemPtr ptr, uint index) where T : unmanaged {
            var size = TSize<T>.size;
            return ref *((safe_ptr<T>)this.GetUnsafePtr(in ptr, index * size)).ptr;
        }

        [INLINE(256)]
        public MemPtr ReAllocArray<T>(in MemPtr ptr, int newLength) where T : unmanaged {
            var size = TSize<T>.size;
            return this.ReAlloc(in ptr, (int)(size * newLength));
        }

        [INLINE(256)]
        public MemPtr ReAllocArray<T>(in MemPtr ptr, uint newLength) where T : unmanaged {
            var size = TSize<T>.size;
            return this.ReAlloc(in ptr, (int)(size * newLength));
        }

        [INLINE(256)]
        public MemPtr ReAllocArray<T>(in MemPtr memPtr, uint newLength, out safe_ptr<T> ptr) where T : unmanaged {
            var size = TSize<T>.size;
            var newPtr = this.ReAlloc(in memPtr, (int)(size * newLength), out var voidPtr);
            ptr = (safe_ptr<T>)voidPtr;
            return newPtr;
        }

        [INLINE(256)]
        public MemPtr ReAllocArray(uint elementSizeOf, in MemPtr ptr, uint newLength) {
            return this.ReAlloc(ptr, (int)(elementSizeOf * newLength));
        }

        [INLINE(256)]
        public MemPtr ReAllocArray(uint elementSizeOf, in MemPtr ptr, uint newLength, out safe_ptr voidPtr) {
            return this.ReAlloc(in ptr, (int)(elementSizeOf * newLength), out voidPtr);
        }

        [INLINE(256)]
        public MemPtr AllocArray<T>(int length) where T : struct {
            var size = TSize<T>.size;
            return this.Alloc(size * length);
        }

        [INLINE(256)]
        public MemPtr AllocArray<T>(uint length) where T : struct {
            var size = TSize<T>.size;
            return this.Alloc(size * length);
        }

        [INLINE(256)]
        public MemPtr AllocArray(int length, int sizeOf) {
            return this.Alloc(sizeOf * length);
        }

        [INLINE(256)]
        public MemPtr AllocArray(uint length, uint sizeOf) {
            return this.Alloc(sizeOf * length);
        }

        [INLINE(256)]
        public MemPtr AllocArray<T>(uint length, out safe_ptr<T> ptr) where T : unmanaged {
            var size = TSize<T>.size;
            var memPtr = this.Alloc(size * length, out var voidPtr);
            ptr = voidPtr;
            return memPtr;
        }

    }

}
