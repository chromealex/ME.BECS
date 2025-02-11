#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

using static ME.BECS.Cuts;
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
using BURST = Unity.Burst.BurstCompileAttribute;

namespace ME.BECS {

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemoryAllocatorProxy))]
    public unsafe partial struct MemoryAllocator : System.IDisposable {

        public bool IsValid => this.zonesList != null;

        /// 
        /// Constructors
        /// 
        [INLINE(256)]
        public MemoryAllocator Initialize(long initialSize) {

            this.initialSize = (int)math.max(initialSize, MemoryAllocator.MIN_ZONE_SIZE);
            this.AddZone(MemoryAllocator.ZmCreateZone(this.initialSize));
            this.version = 1;

            return this;
        }

        [INLINE(256)]
        public void Dispose() {

            this.FreeZones();
            
            if (this.zonesList != null) {
                _free((safe_ptr)this.zonesList, Constants.ALLOCATOR_PERSISTENT);
                this.zonesList = null;
            }

            this.zonesListCapacity = 0;

        }

        [INLINE(256)]
        internal readonly MemPtr GetSafePtr(void* ptr, uint zoneIndex) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (zoneIndex >= this.zonesListCount || this.zonesList[zoneIndex] == null) {
                throw new System.Exception();
            }
            #endif
            
            //var index = (long)zoneIndex << 32;
            //var offset = ((byte*)ptr - (byte*)this.zonesList[zoneIndex]);

            // index | offset
            return new MemPtr(zoneIndex, (uint)((byte*)ptr - (byte*)this.zonesList[zoneIndex]));
        }

        [INLINE(256)]
        private void FreeZones() {
            if (this.zonesListCount > 0 && this.zonesList != null) {
                for (int i = 0; i < this.zonesListCount; i++) {
                    var zone = this.zonesList[i];
                    if (zone != null) {
                        MemoryAllocator.ZmFreeZone(zone);
                    }
                }
            }

            this.zonesListCount = 0;
        }

        [INLINE(256)]
        internal uint AddZone(MemZone* zone, bool lookUpNull = true) {

            if (lookUpNull == true) {

                for (uint i = 0u; i < this.zonesListCount; ++i) {
                    if (this.zonesList[i] == null) {
                        this.zonesList[i] = zone;
                        return i;
                    }
                }

            }

            if (this.zonesListCapacity <= this.zonesListCount) {
                
                var capacity = math.max(MemoryAllocator.MIN_ZONES_LIST_CAPACITY, this.zonesListCapacity * 2u);
                var list = (MemZone**)_make(capacity * (uint)sizeof(MemZone*), TAlign<System.IntPtr>.alignInt, Constants.ALLOCATOR_PERSISTENT).ptr;

                if (this.zonesList != null) {
                    _memcpy((safe_ptr)this.zonesList, (safe_ptr)list, (uint)sizeof(MemZone*) * this.zonesListCount);
                    _free((safe_ptr)this.zonesList, Constants.ALLOCATOR_PERSISTENT);
                }
                
                this.zonesList = list;
                this.zonesListCapacity = capacity;
                
            }

            this.zonesList[this.zonesListCount++] = zone;

            return this.zonesListCount - 1u;
        }

        [INLINE(256)]
        public static void ZmClearZone(MemZone* zone) {
            
            var block = (MemBlock*)((byte*)zone + sizeof(MemZone));
            var blockOffset = new MemBlockOffset(block, zone);

            // set the entire zone to one free block
            zone->blocklist.next = zone->blocklist.prev = blockOffset;
            
            zone->blocklist.state = MemoryAllocator.BLOCK_STATE_USED;
            zone->rover = blockOffset;

            block->prev = block->next = new MemBlockOffset(&zone->blocklist, zone);
            block->state = MemoryAllocator.BLOCK_STATE_FREE;
            block->size = zone->size - TSize<MemZone>.sizeInt;
            
        }

        [INLINE(256)]
        public static MemZone* ZmCreateZoneEmpty(int size) {
            size = MemoryAllocator.ZmGetMemBlockSize(size) + TSize<MemZone>.sizeInt;
            var zone = ((safe_ptr<MemZone>)_make(size, TAlign<uint>.alignInt, Constants.ALLOCATOR_PERSISTENT)).ptr;
            return zone;
        }

        [INLINE(256)]
        public static MemZone* ZmCreateZone(int size) {
            size = MemoryAllocator.ZmGetMemBlockSize(size) + TSize<MemZone>.sizeInt;
            var zone = ((safe_ptr<MemZone>)_make(size, TAlign<uint>.alignInt, Constants.ALLOCATOR_PERSISTENT)).ptr;
            zone->size = size;
            MemoryAllocator.ZmClearZone(zone);
            return zone;
        }

        [INLINE(256)]
        public static MemZone* ZmReallocZone(MemZone* zone, int newSize) {
            if (zone->size >= newSize) return zone;

            var newZone = MemoryAllocator.ZmCreateZone(newSize);
            var extra = newZone->size - zone->size;

            _memcpy((safe_ptr)zone, (safe_ptr)newZone, zone->size);

            newZone->size = zone->size + extra;

            var top = newZone->rover.Ptr(newZone);

            for (var block = newZone->blocklist.next.Ptr(newZone); block != &newZone->blocklist; block = block->next.Ptr(newZone)) {
                if (block > top) {
                    top = block;
                }
            }

            if (top->state == MemoryAllocator.BLOCK_STATE_FREE) {
                top->size += extra;
            } else {
                var newblock = (MemBlock*)((byte*)top + top->size);
                var newblockOffset = new MemBlockOffset(newblock, newZone);
                newblock->size = extra;

                newblock->state = MemoryAllocator.BLOCK_STATE_FREE;
                #if MEMORY_ALLOCATOR_BOUNDS_CHECK
                newblock->id = MemoryAllocator.ZONE_ID;
                #endif
                newblock->prev = new MemBlockOffset(top, newZone);
                newblock->next = top->next;
                newblock->next.Ptr(newZone)->prev = newblockOffset;

                top->next = newblockOffset;
                newZone->rover = newblockOffset;
            }

            MemoryAllocator.ZmFreeZone(zone);

            return newZone;
        }

        [INLINE(256)]
        public static void ZmFreeZone(MemZone* zone) {
            _free((safe_ptr)zone, Constants.ALLOCATOR_PERSISTENT);
        }

        [INLINE(256)]
        public static bool ZmFree(MemZone* zone, void* ptr, bool freePrev = true) {
            MemoryAllocator.CHECK_PTR(ptr);
            var block = (MemBlock*)((byte*)ptr - TSize<MemBlock>.size);
            var blockOffset = new MemBlockOffset(block, zone);

            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            MemoryAllocator.CHECK_ZONE_ID(block->id);
            #endif

            if (block->state == MemoryAllocator.BLOCK_STATE_FREE) {
                throw new System.Exception("Seems like ptr is free already");
            }
            
            // mark as free
            block->state = MemoryAllocator.BLOCK_STATE_FREE;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            block->id = 0;
            #endif

            MemBlock* other;
            MemBlockOffset otherOffset;
            if (freePrev == true) {
                other = block->prev.Ptr(zone);
                otherOffset = block->prev;
                if (other->state == MemoryAllocator.BLOCK_STATE_FREE) {
                    // merge with previous free block
                    other->size += block->size;
                    other->next = block->next;
                    other->next.Ptr(zone)->prev = otherOffset;

                    if (blockOffset == zone->rover) zone->rover = otherOffset;

                    block = other;
                    blockOffset = otherOffset;
                }
            }

            {
                other = block->next.Ptr(zone);
                otherOffset = block->next;
                if (other->state == MemoryAllocator.BLOCK_STATE_FREE) {
                    // merge the next free block onto the end
                    block->size += other->size;
                    block->next = other->next;
                    block->next.Ptr(zone)->prev = blockOffset;

                    if (otherOffset == zone->rover) zone->rover = blockOffset;
                }
            }

            return true;
        }

        [INLINE(256)]
        private static int ZmGetMemBlockSize(int size) {
            return Align(size) + TSize<MemBlock>.sizeInt;
        }
        
        [INLINE(256)]
        internal static int Align(int size) => ((size + 3) & ~3);
        [INLINE(256)]
        internal static long Align(long size) => ((size + 3) & ~3);

        [INLINE(256)]
        public static void* ZmMalloc(MemZone* zone, int size) {
            
            size = MemoryAllocator.ZmGetMemBlockSize(size);

            // scan through the block list,
            // looking for the first free block
            // of sufficient size,
            // throwing out any purgable blocks along the way.

            // if there is a free block behind the rover,
            //  back up over them
            var @base = zone->rover.Ptr(zone);

            if (@base->prev.Ptr(zone)->state != MemoryAllocator.BLOCK_STATE_FREE) @base = @base->prev.Ptr(zone);

            var rover = @base;
            var start = @base->prev.Ptr(zone);

            do {
                if (rover == start) {
                    // scanned all the way around the list
                    return null;
                    //throw new System.OutOfMemoryException($"Malloc: failed on allocation of {size} bytes");
                }

                if (rover->state != MemoryAllocator.BLOCK_STATE_FREE) {
                    // hit a block that can't be purged,
                    // so move base past it
                    @base = rover = rover->next.Ptr(zone);
                } else {
                    rover = rover->next.Ptr(zone);
                }
            } while (@base->state != MemoryAllocator.BLOCK_STATE_FREE || @base->size < size);
            
            // found a block big enough
            var extra = @base->size - size;
            if (extra > MemoryAllocator.MIN_FRAGMENT) {
                // there will be a free fragment after the allocated block
                var newblock = (MemBlock*)((byte*)@base + size);
                var newblockOffset = new MemBlockOffset(newblock, zone);
                newblock->size = extra;

                // NULL indicates free block.
                newblock->state = MemoryAllocator.BLOCK_STATE_FREE;
                newblock->prev = new MemBlockOffset(@base, zone);
                newblock->next = @base->next;
                newblock->next.Ptr(zone)->prev = newblockOffset;

                @base->next = newblockOffset;
                @base->size = size;

            }

            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            @base->id = MemoryAllocator.ZONE_ID;
            #endif

            @base->state = MemoryAllocator.BLOCK_STATE_USED;
            // next allocation will start looking here
            zone->rover = @base->next;
            
            return (void*)((byte*)@base + TSize<MemBlock>.size);
        }

        [INLINE(256)]
        public static void* ZmAlloc(MemZone* zone, MemBlock* @base, int size) {
            
            // found a block big enough
            var extra = @base->size - size;
            if (extra > MemoryAllocator.MIN_FRAGMENT) {
                // there will be a free fragment after the allocated block
                var newblock = (MemBlock*)((byte*)@base + size);
                var newblockOffset = new MemBlockOffset(newblock, zone);
                newblock->size = extra;

                // NULL indicates free block.
                newblock->state = MemoryAllocator.BLOCK_STATE_FREE;
                newblock->prev = new MemBlockOffset(@base, zone);
                newblock->next = @base->next;
                newblock->next.Ptr(zone)->prev = newblockOffset;

                @base->next = newblockOffset;
                @base->size = size;

            }

            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            @base->id = MemoryAllocator.ZONE_ID;
            #endif

            @base->state = MemoryAllocator.BLOCK_STATE_USED;
            // next allocation will start looking here
            zone->rover = @base->next;

            return (void*)((byte*)@base + TSize<MemBlock>.size);
            
        }
        
        [INLINE(256)]
        public static bool IsEmptyZone(MemZone* zone) {

            for (var block = zone->blocklist.next.Ptr(zone); block != &zone->blocklist; block = block->next.Ptr(zone)) {
                if (block->state != MemoryAllocator.BLOCK_STATE_FREE) return false;
            }

            return true;
        }

        [INLINE(256)]
        public static bool ZmCheckHeap(MemZone* zone, out int blockIndex, out int index) {
            blockIndex = -1;
            index = -1;
            for (var block = zone->blocklist.next.Ptr(zone);; block = block->next.Ptr(zone)) {
                if (block->next.Ptr(zone) == &zone->blocklist) {
                    // all blocks have been hit
                    break;
                }

                ++blockIndex;
                if (MemoryAllocator.ZmCheckBlock(zone, block, out index) == false) return false;
            }

            return true;
        }

        [INLINE(256)]
        private static bool ZmCheckBlock(MemZone* zone, MemBlock* block, out int index) {
            index = -1;
            var next = (byte*)block->next.Ptr(zone);
            if (next == null) {
                index = 0;
                return false;
            }

            if ((byte*)block + block->size != (byte*)block->next.Ptr(zone)) {
                index = 1;
                return false;
            }

            if (block->next.Ptr(zone)->prev.Ptr(zone) != block) {
                index = 2;
                return false;
            }

            if (block->state == MemoryAllocator.BLOCK_STATE_FREE && block->next.Ptr(zone)->state == MemoryAllocator.BLOCK_STATE_FREE) {
                index = 3;
                return false;
            }

            return true;
        }

        [INLINE(256)]
        public static int GetZmFreeMemory(MemZone* zone) {
            var free = 0;

            for (var block = zone->blocklist.next.Ptr(zone); block != &zone->blocklist; block = block->next.Ptr(zone)) {
                if (block->state == MemoryAllocator.BLOCK_STATE_FREE) free += block->size;
            }

            return free;
        }

        [INLINE(256)]
        public static bool ZmHasFreeBlock(MemZone* zone, int size) {
            size = MemoryAllocator.ZmGetMemBlockSize(size);

            for (var block = zone->blocklist.next.Ptr(zone); block != &zone->blocklist; block = block->next.Ptr(zone)) {
                if (block->state == MemoryAllocator.BLOCK_STATE_FREE && block->size > size) {
                    return true;
                }

            }

            return false;
        }

    }

}
