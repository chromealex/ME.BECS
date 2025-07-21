namespace ME.BECS.Memory {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(AllocatorDebugProxy))]
    public unsafe partial struct Allocator {

        public struct BlockHeader {

            // Header
            public uint freeIndex; // Index in free blocks, uint.MaxValue - USED, >= 0 - FREE
            public uint prev;  // Prev offset in current zone, uint.MaxValue = null
            public uint next;  // Next offset in current zone, uint.MaxValue = null
            
            // Data
            public uint size;  // Data size
            // Data placed here

            public bool CheckConsistency(in Allocator allocator, uint zoneId, bool checkMove = true) {
                var zone = allocator.zones[zoneId];
                NUnit.Framework.Assert.IsTrue(this.freeIndex == uint.MaxValue || allocator.freeBlocks[(int)this.freeIndex].zoneId == zoneId);
                if (checkMove == true) {
                    NUnit.Framework.Assert.IsTrue(this.prev == uint.MaxValue || ((BlockHeader*)(zone.ptr->data.ptr + this.prev))->CheckConsistency(in allocator, zoneId, false));
                    NUnit.Framework.Assert.IsTrue(this.next == uint.MaxValue || ((BlockHeader*)(zone.ptr->data.ptr + this.next))->CheckConsistency(in allocator, zoneId, false));
                }
                return true;
            }

            public override string ToString() {
                return $"FreeIndex: {this.freeIndex}, prev: {this.prev}, next: {this.next}, size: {this.size}";
            }

        }
        
        public struct Zone {

            public safe_ptr data;
            public uint size;

            public void Dispose() {
                _free(this.data);
            }

        }

        public Allocator(Unity.Collections.Allocator allocator) {
            this = default;
            this.allocatorLabel = allocator;
        }

        public void Initialize(uint zonesCapacity, uint initialSize, Unity.Collections.Allocator allocator) {
            this.allocatorLabel = allocator;
            this.zonesCount = 0u;
            this.zonesCapacity = zonesCapacity;
            this.initialSize = initialSize;
            this.freeBlocks = new UnsafeList<MemPtr>(100, allocator);
            this.zones = this.MakeZones(zonesCapacity, allocator);
            this.AddZone(initialSize);
        }

        private safe_ptr<safe_ptr<Zone>> MakeZones(uint zonesCapacity, Unity.Collections.Allocator allocator) {
            return _make(zonesCapacity * (uint)sizeof(safe_ptr<Zone>), 8, allocator);
        }

        [INLINE(256)]
        private void AddZone(uint initialSize) {
            var zoneId = this.zonesCount++;
            this.zones[zoneId] = this.CreateZone(initialSize, zoneId);
        }

        [INLINE(256)]
        private safe_ptr<Zone> CreateZone(uint size, uint zoneId) {
            var zone = this.CreateZoneRaw(size);
            var first = (BlockHeader*)zone.ptr->data.ptr;
            first->size = size;
            first->prev = uint.MaxValue;
            first->next = uint.MaxValue;
            first->freeIndex = (uint)this.freeBlocks.Length;
            this.freeBlocks.Add(new MemPtr(zoneId, 0u));
            return zone;
        }

        [INLINE(256)]
        private safe_ptr<Zone> CreateZoneRaw(uint size) {
            var s = TSize<BlockHeader>.size + size;
            var zone = (safe_ptr<Zone>)_make(sizeof(Zone), TAlign<Zone>.alignInt, this.allocatorLabel);
            zone.ptr->data = _make(s, 4, this.allocatorLabel);
            zone.ptr->size = s;
            return zone;
        }

        [INLINE(256)]
        private MemPtr AllocFromFreeBlocks(uint size, out safe_ptr ptr) {
            // look up through free blocks
            for (var index = 0; index < this.freeBlocks.Length; ++index) {
                var memPtr = this.freeBlocks[index];
                var blockPtr = this.GetPtr(memPtr);
                var header = (BlockHeader*)blockPtr;
                if (size > header->size) continue;
                // calc tail size
                var tailSize = header->size > size + sizeof(BlockHeader) ? header->size - size : 0u;
                if (tailSize == 0u) {
                    // use full block
                    this.RemoveFromFree(header);
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
                    newBlockHeader->freeIndex = (uint)index;
                    newBlockHeader->size = pSize - size - (uint)sizeof(BlockHeader);
                    this.freeBlocks[index] = this.GetSafePtr(newBlock, memPtr.zoneId);
                }
                ptr = (safe_ptr)(blockPtr + sizeof(BlockHeader));
                return this.GetSafePtr(ptr.ptr, memPtr.zoneId);
            }

            ptr = default;
            return MemPtr.Invalid;
        }

        [INLINE(256)]
        private void RemoveFromFree(BlockHeader* header) {
            var last = this.freeBlocks[this.freeBlocks.Length - 1];
            var lastHeader = (BlockHeader*)this.GetPtr(last);
            lastHeader->freeIndex = header->freeIndex;
            this.freeBlocks.RemoveAtSwapBack((int)header->freeIndex);
        }

        public void CheckConsistency() {
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = this.zones[i].ptr;
                var node = (Allocator.BlockHeader*)zone->data.ptr;
                node->CheckConsistency(this, i);
                while (node->next != uint.MaxValue) {
                    node = (Allocator.BlockHeader*)(zone->data.ptr + node->next);
                    node->CheckConsistency(this, i);
                }
            }
        }

    }

}