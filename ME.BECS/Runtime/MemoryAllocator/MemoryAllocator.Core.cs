//#define MEMORY_ALLOCATOR_BOUNDS_CHECK
//#define BURST

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
using BURST = Unity.Burst.BurstCompileAttribute;

namespace ME.BECS {

    public unsafe class MemoryAllocatorProxy {

        public struct Dump {

            public string[] blocks;

        }
        
        private readonly MemoryAllocator allocator;
        
        public MemoryAllocatorProxy(MemoryAllocator allocator) {

            this.allocator = allocator;

        }

        public Dump[] dump {
            get {
                var list = new System.Collections.Generic.List<Dump>();
                for (int i = 0; i < this.allocator.zonesListCount; ++i) {
                    var zone = this.allocator.zonesList[i];

                    if (zone == null) {
                        list.Add(default);
                        continue;
                    }

                    var blocks = new System.Collections.Generic.List<string>();
                    MemoryAllocator.ZmDumpHeap(zone, blocks);
                    var item = new Dump() {
                        blocks = blocks.ToArray(),
                    };
                    list.Add(item);
                }
                
                return list.ToArray();
            }
        }

        public Dump[] checks {
            get {
                var list = new System.Collections.Generic.List<Dump>();
                for (int i = 0; i < this.allocator.zonesListCount; ++i) {
                    var zone = this.allocator.zonesList[i];

                    if (zone == null) {
                        list.Add(default);
                        continue;
                    }
                    
                    var blocks = new System.Collections.Generic.List<string>();
                    MemoryAllocator.ZmCheckHeap(zone, blocks);
                    var item = new Dump() {
                        blocks = blocks.ToArray(),
                    };
                    list.Add(item);
                }
                
                return list.ToArray();
            }
        }

    }

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemoryAllocatorProxy))]
    #if BURST
    [BURST(CompileSynchronously = true)]
    #endif
    public unsafe partial struct MemoryAllocator {

        private const int ZONE_ID = 0x1d4a11;

        private const int MIN_FRAGMENT = 64;
        
        public const byte BLOCK_STATE_FREE = 0;
        public const byte BLOCK_STATE_USED = 1;
        
        public struct MemZone {

            public int size;           // total bytes malloced, including header
            public MemBlock blocklist; // start / end cap for linked list
            public MemBlockOffset rover;

        }

        public struct MemBlock {

            public int size;    // including the header and possibly tiny fragments
            public byte state;
            // to align block
            public byte b1;
            public byte b2;
            public byte b3;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            public int id;      // should be ZONE_ID
            #endif
            public MemBlockOffset next;
            public MemBlockOffset prev;

        };

        public readonly struct MemBlockOffset {

            public readonly long value;

            [INLINE(256)]
            public MemBlockOffset(void* block, MemZone* zone) {
                this.value = (byte*)block - (byte*)zone;
            }

            [INLINE(256)]
            public MemBlock* Ptr(void* zone) {
                return (MemBlock*)((byte*)zone + this.value);
            }

            [INLINE(256)]
            public static bool operator ==(MemBlockOffset a, MemBlockOffset b) => a.value == b.value;

            [INLINE(256)]
            public static bool operator !=(MemBlockOffset a, MemBlockOffset b) => a.value != b.value;

            [INLINE(256)]
            public bool Equals(MemBlockOffset other) {
                return this.value == other.value;
            }

            [INLINE(256)]
            public override bool Equals(object obj) {
                return obj is MemBlockOffset other && this.Equals(other);
            }

            [INLINE(256)]
            public override int GetHashCode() {
                return this.value.GetHashCode();
            }

        }
        
        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
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

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static MemZone* ZmCreateZoneEmpty(int size) {

            size = MemoryAllocator.ZmGetMemBlockSize(size) + TSize<MemZone>.sizeInt;
            var zone = (MemZone*)UnsafeUtility.Malloc(size, TAlign<byte>.alignInt, Allocator.Persistent);
            return zone;
            
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static MemZone* ZmCreateZone(int size) {

            size = MemoryAllocator.ZmGetMemBlockSize(size) + TSize<MemZone>.sizeInt;
            var zone = (MemZone*)UnsafeUtility.Malloc(size, TAlign<byte>.alignInt, Allocator.Persistent);
            
            //UnsafeUtility.MemClear(zone, size);
            
            zone->size = size;
            MemoryAllocator.ZmClearZone(zone);
            
            return zone;
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static MemZone* ZmReallocZone(MemZone* zone, int newSize) {
            if (zone->size >= newSize) return zone;

            var newZone = MemoryAllocator.ZmCreateZone(newSize);
            var extra = newZone->size - zone->size;

            UnsafeUtility.MemCpy(newZone, zone, zone->size);

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
                newblock->prev = new MemBlockOffset(top, newZone);
                newblock->next = top->next;
                newblock->next.Ptr(newZone)->prev = newblockOffset;

                top->next = newblockOffset;
                newZone->rover = newblockOffset;
            }

            MemoryAllocator.ZmFreeZone(zone);

            return newZone;
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static void ZmFreeZone(MemZone* zone) {
            UnsafeUtility.Free(zone, Allocator.Persistent);
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static bool ZmFree(MemZone* zone, void* ptr, bool freePrev = true) {
            var block = (MemBlock*)((byte*)ptr - TSize<MemBlock>.size);
            var blockOffset = new MemBlockOffset(block, zone);

            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (block->id != MemoryAllocator.ZONE_ID) {
                //return false;
                throw new System.ArgumentException("ZmFree: freed a pointer without ZONEID");
            }
            #endif

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

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        private static int ZmGetMemBlockSize(int size) {
            return ((size + 3) & ~3) + TSize<MemBlock>.sizeInt;
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
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
        
        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static bool IsEmptyZone(MemZone* zone) {

            for (var block = zone->blocklist.next.Ptr(zone); block != &zone->blocklist; block = block->next.Ptr(zone)) {
                if (block->state != MemoryAllocator.BLOCK_STATE_FREE) return false;
            }

            return true;
        }

        [INLINE(256)]
        public static void ZmDumpHeap(MemZone* zone, System.Collections.Generic.List<string> results) {
            results.Add($"zone size: {zone->size}; location: {new IntPtr(zone)}; rover block offset: {zone->rover.value}");

            for (var block = zone->blocklist.next.Ptr(zone);; block = block->next.Ptr(zone)) {

                results.Add($"block offset: {(byte*)block - (byte*)@zone}; size: {block->size}; state: {block->state}");

                if (block->next.Ptr(zone) == &zone->blocklist) break;

                MemoryAllocator.ZmCheckBlock(zone, block, results);
            }
        }

        [INLINE(256)]
        public static void ZmCheckHeap(MemZone* zone, System.Collections.Generic.List<string> results) {
            for (var block = zone->blocklist.next.Ptr(zone);; block = block->next.Ptr(zone)) {
                if (block->next.Ptr(zone) == &zone->blocklist) {
                    // all blocks have been hit
                    break;
                }

                MemoryAllocator.ZmCheckBlock(zone, block, results);
            }
        }

        [INLINE(256)]
        private static void ZmCheckBlock(MemZone* zone, MemBlock* block, System.Collections.Generic.List<string> results) {
            var next = (byte*)block->next.Ptr(zone);
            if (next == null) {
                results.Add("CheckHeap: next block is null\n");
                return;
            }

            if ((byte*)block + block->size != (byte*)block->next.Ptr(zone)) {
                results.Add("CheckHeap: block size does not touch the next block\n");
            }

            if (block->next.Ptr(zone)->prev.Ptr(zone) != block) {
                results.Add("CheckHeap: next block doesn't have proper back link\n");
            }

            if (block->state == MemoryAllocator.BLOCK_STATE_FREE && block->next.Ptr(zone)->state == MemoryAllocator.BLOCK_STATE_FREE) {
                results.Add("CheckHeap: two consecutive free blocks\n");
            }
        }
        
        [INLINE(256)]
        public static bool ZmCheckHeap(MemZone* zone) {
            for (var block = zone->blocklist.next.Ptr(zone);; block = block->next.Ptr(zone)) {
                if (block->next.Ptr(zone) == &zone->blocklist) {
                    // all blocks have been hit
                    break;
                }

                if (MemoryAllocator.ZmCheckBlock(zone, block) == false) return false;
            }

            return true;
        }

        [INLINE(256)]
        private static bool ZmCheckBlock(MemZone* zone, MemBlock* block) {
            var next = (byte*)block->next.Ptr(zone);
            if (next == null) {
                return false;
            }

            if ((byte*)block + block->size != (byte*)block->next.Ptr(zone)) {
                return false;
            }

            if (block->next.Ptr(zone)->prev.Ptr(zone) != block) {
                return false;
            }

            if (block->state == MemoryAllocator.BLOCK_STATE_FREE && block->next.Ptr(zone)->state == MemoryAllocator.BLOCK_STATE_FREE) {
                return false;
            }

            return true;
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static int GetZmFreeMemory(MemZone* zone) {
            var free = 0;

            for (var block = zone->blocklist.next.Ptr(zone); block != &zone->blocklist; block = block->next.Ptr(zone)) {
                if (block->state == MemoryAllocator.BLOCK_STATE_FREE) free += block->size;
            }

            return free;
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
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
