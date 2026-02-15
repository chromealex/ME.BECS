namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(AllocatorDebugProxy))]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe partial struct MemoryAllocator {

        public const uint ZONE_HEADER_OFFSET = 4u;
        public const uint MIN_ZONE_SIZE = 512u * 1024u;
        public const uint MIN_ZONE_SIZE_IN_KB = MIN_ZONE_SIZE / 1024u;
        public const uint DEFAULT_ZONES_CAPACITY = 10u;

        [IgnoreProfiler]
        #if !BECS_IL2CPP_OPTIONS_DISABLE
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
        #endif
        public struct BlockHeader {

            // Header
            public uint freeIndex; // Index in free blocks, uint.MaxValue - USED, >= 0 - FREE
            public uint prev;  // Prev offset in current zone, uint.MaxValue = null
            public uint next;  // Next offset in current zone, uint.MaxValue = null
            
            // Data
            public uint size;  // Data size
            // Data placed here

            public bool CheckConsistency(in MemoryAllocator allocator, uint nodeId, uint zoneId, bool checkMove = true) {
                var zone = allocator.zones[zoneId];
                UnityEngine.Assertions.Assert.IsTrue(this.freeIndex == uint.MaxValue || allocator.freeBlocks.GetPtr(in this).zoneId == zoneId);
                if (checkMove == true) {
                    if (this.prev != uint.MaxValue) UnityEngine.Assertions.Assert.IsTrue(((BlockHeader*)(zone.ptr->root.ptr + this.prev))->next == nodeId);
                    if (this.next != uint.MaxValue) UnityEngine.Assertions.Assert.IsTrue(((BlockHeader*)(zone.ptr->root.ptr + this.next))->prev == nodeId);
                    UnityEngine.Assertions.Assert.IsTrue(this.prev == uint.MaxValue || ((BlockHeader*)(zone.ptr->root.ptr + this.prev))->CheckConsistency(in allocator, 0u, zoneId, false));
                    UnityEngine.Assertions.Assert.IsTrue(this.next == uint.MaxValue || ((BlockHeader*)(zone.ptr->root.ptr + this.next))->CheckConsistency(in allocator, 0u, zoneId, false));
                }
                return true;
            }

            public override string ToString() {
                return $"FreeIndex: {this.freeIndex}, prev: {this.prev}, next: {this.next}, size: {this.size}";
            }

        }

        [IgnoreProfiler]
        #if !BECS_IL2CPP_OPTIONS_DISABLE
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
        #endif
        public struct Zone {

            public safe_ptr data;
            public safe_ptr root => this.data;
            public safe_ptr firstBlock => this.data + ZONE_HEADER_OFFSET;
            public uint size;

            public void Dispose(Unity.Collections.Allocator allocator) {
                _free(this.data, allocator);
            }

        }

        public MemoryAllocator(Unity.Collections.Allocator allocator) {
            this = default;
            this.allocatorLabel = allocator;
        }

        [NotThreadSafe][IgnoreProfiler]
        public MemoryAllocator Initialize(uint initialSize) {
            return this.Initialize(DEFAULT_ZONES_CAPACITY, initialSize, Constants.ALLOCATOR_PERSISTENT);
        }

        [NotThreadSafe][IgnoreProfiler]
        public MemoryAllocator Initialize(uint zonesCapacity, uint initialSize, Unity.Collections.Allocator allocator, bool ignoreSizeRestrictions = false) {
            if (ignoreSizeRestrictions == false && zonesCapacity < 1u) zonesCapacity = 1u;
            if (ignoreSizeRestrictions == false && initialSize < MIN_ZONE_SIZE) initialSize = MIN_ZONE_SIZE;
            this.allocatorLabel = allocator;
            this.zonesCount = 0u;
            this.zonesCapacity = zonesCapacity;
            this.initialSize = initialSize;
            this.freeBlocks.Initialize(10, allocator);
            this.zones = MakeZones(zonesCapacity, allocator);
            this.AddZone(initialSize);
            return this;
        }

        [INLINE(256)][IgnoreProfiler]
        private static safe_ptr<safe_ptr<Zone>> MakeZones(uint zonesCapacity, Unity.Collections.Allocator allocator) {
            return _make(zonesCapacity * (uint)sizeof(safe_ptr<Zone>), 8, allocator);
        }

        [INLINE(256)][IgnoreProfiler]
        private void AddZone(uint initialSize) {
            var zoneId = this.zonesCount++;
            this.zones[zoneId] = this.CreateZone(initialSize, zoneId);
        }

        [INLINE(256)][IgnoreProfiler]
        private safe_ptr<Zone> CreateZone(uint size, uint zoneId) {
            if (size < this.initialSize) size = this.initialSize;
            var zone = this.CreateZoneRaw(ref size);
            var first = (BlockHeader*)(zone.ptr->firstBlock.ptr);
            first->size = size;
            first->prev = uint.MaxValue;
            first->next = uint.MaxValue;
            first->freeIndex = uint.MaxValue;
            this.freeBlocks.Add(in this, first, new MemPtr(zoneId, ZONE_HEADER_OFFSET));
            return zone;
        }

        [INLINE(256)][IgnoreProfiler]
        private safe_ptr<Zone> CreateZoneRaw(uint size) {
            return this.CreateZoneRaw(ref size);
        }

        [INLINE(256)][IgnoreProfiler]
        private safe_ptr<Zone> CreateZoneRaw(ref uint size) {
            var s = TSize<BlockHeader>.size + size + ZONE_HEADER_OFFSET;
            var zone = (safe_ptr<Zone>)_make(sizeof(Zone), TAlign<Zone>.alignInt, this.allocatorLabel);
            zone.ptr->data = _make(s, 4, this.allocatorLabel);
            zone.ptr->size = s;
            _memclear(zone.ptr->data, s);
            return zone;
        }

        [INLINE(256)][IgnoreProfiler]
        private MemPtr AllocFromFreeBlocks(uint size, out safe_ptr ptr) {
            // look up through free blocks
            var memPtr = this.freeBlocks.Pop(in this, size);
            if (memPtr.IsValid() == true) {
                var blockPtr = this.GetPtr(memPtr);
                var header = (BlockHeader*)blockPtr;
                // calc tail size
                var tailSize = header->size > size + sizeof(BlockHeader) + sizeof(BlockHeader) ? header->size - size : 0u;
                if (tailSize == 0u) {
                    // use full block
                    header->freeIndex = uint.MaxValue;
                } else {
                    // split blocks
                    var pNext = header->next;
                    var pSize = header->size;
                    header->size = size;
                    header->next = memPtr.offset + (uint)sizeof(BlockHeader) + header->size;
                    header->freeIndex = uint.MaxValue;
                    var newBlock = blockPtr + sizeof(BlockHeader) + header->size;
                    var newBlockHeader = (BlockHeader*)newBlock;
                    newBlockHeader->prev = memPtr.offset;
                    newBlockHeader->next = pNext;
                    newBlockHeader->size = pSize - size - (uint)sizeof(BlockHeader);
                    if (pNext != uint.MaxValue) {
                        var nextHeader = (BlockHeader*)(this.zones[memPtr.zoneId].ptr->root.ptr + newBlockHeader->next);
                        nextHeader->prev = header->next;
                    }
                    this.freeBlocks.Add(in this, newBlockHeader, memPtr.zoneId);
                }
                ptr = (safe_ptr)(blockPtr + sizeof(BlockHeader));
                return this.GetSafePtr(ptr.ptr, memPtr.zoneId);
            }

            ptr = default;
            return MemPtr.Invalid;
        }

        [INLINE(256)][IgnoreProfiler]
        private void RemoveFromFree(BlockHeader* header) {
            this.freeBlocks.Remove(in this, header);
            header->freeIndex = uint.MaxValue;
        }

        [INLINE(256)][IgnoreProfiler]
        private static uint Align(uint size) {
            return ((size + 3u) & ~3u);
        }

    }

}