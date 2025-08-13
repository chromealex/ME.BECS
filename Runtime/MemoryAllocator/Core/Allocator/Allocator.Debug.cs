namespace ME.BECS {

    public unsafe class AllocatorDebugProxy {

        public struct ZoneInfo {

            public MemoryAllocator.Zone zone;
            public string[] blocks;

        }

        private readonly MemoryAllocator allocator;
        public AllocatorDebugProxy(MemoryAllocator allocator) {
            this.allocator = allocator;
        }

        public string[] errors {
            get {
                var errors = new System.Collections.Generic.List<string>(10);
                for (uint i = 0u; i < this.allocator.zonesCount; ++i) {
                    var zone = this.allocator.zones[i].ptr;
                    var size = zone->size;
                    var curSize = MemoryAllocator.ZONE_HEADER_OFFSET;
                    var node = (MemoryAllocator.BlockHeader*)zone->firstBlock.ptr;
                    var prevId = MemoryAllocator.ZONE_HEADER_OFFSET;
                    do {
                        curSize += node->size + (uint)sizeof(MemoryAllocator.BlockHeader);
                        var check = true;
                        if (check == true && node->prev != uint.MaxValue) {
                            check = (((MemoryAllocator.BlockHeader*)(zone->root.ptr + node->prev))->next == prevId);
                        }

                        if (check == true && node->next != uint.MaxValue) {
                            check = (((MemoryAllocator.BlockHeader*)(zone->root.ptr + node->next))->prev == prevId);
                        }
                        
                        if (check == true && node->prev != uint.MaxValue) {
                            var h = (MemoryAllocator.BlockHeader*)(zone->root.ptr + node->prev);
                            var idx = h->freeIndex;
                            if (idx != uint.MaxValue) {
                                check = (this.allocator.freeBlocks.GetPtr(in *h).zoneId == i);
                            }
                        }

                        if (check == true && node->next != uint.MaxValue) {
                            var h = (MemoryAllocator.BlockHeader*)(zone->root.ptr + node->next);
                            var idx = h->freeIndex;
                            if (idx != uint.MaxValue) {
                                check = (this.allocator.freeBlocks.GetPtr(in *h).zoneId == i);
                            }
                        }

                        if (check == false) {
                            errors.Add($"{prevId}: ZoneId: {i}, node: {node->ToString()}");
                        }

                        prevId = node->next;
                        if (node->next != uint.MaxValue) {
                            node = (MemoryAllocator.BlockHeader*)(zone->root.ptr + node->next);
                        } else {
                            break;
                        }
                    } while (true);

                    if (curSize != size) {
                        errors.Add($"Zone {i} Size: {size}, but Current Size: {curSize}");
                    }
                }
                return errors.ToArray();
            }
        }
        
        public ZoneInfo[] zones {
            get {
                var zones = new ZoneInfo[this.allocator.zonesCount];
                for (uint i = 0u; i < this.allocator.zonesCount; ++i) {
                    var zone = this.allocator.zones[i].ptr;
                    var blocks = new System.Collections.Generic.List<string>();
                    var node = (MemoryAllocator.BlockHeader*)zone->firstBlock.ptr;
                    var prevId = MemoryAllocator.ZONE_HEADER_OFFSET;
                    do {
                        var check = true;
                        if (check == true && node->prev != uint.MaxValue) {
                            check = (((MemoryAllocator.BlockHeader*)(zone->root.ptr + node->prev))->next == prevId);
                        }

                        if (check == true && node->next != uint.MaxValue) {
                            check = (((MemoryAllocator.BlockHeader*)(zone->root.ptr + node->next))->prev == prevId);
                        }
                        
                        if (check == true && node->prev != uint.MaxValue) {
                            var h = (MemoryAllocator.BlockHeader*)(zone->root.ptr + node->prev);
                            var idx = h->freeIndex;
                            if (idx != uint.MaxValue) {
                                check = (this.allocator.freeBlocks.GetPtr(in *h).zoneId == i);
                            }
                        }

                        if (check == true && node->next != uint.MaxValue) {
                            var h = (MemoryAllocator.BlockHeader*)(zone->root.ptr + node->next);
                            var idx = h->freeIndex;
                            if (idx != uint.MaxValue) {
                                check = (this.allocator.freeBlocks.GetPtr(in *h).zoneId == i);
                            }
                        }
                        
                        blocks.Add($"{prevId}: {node->ToString()}, CHECK: {check}");
                    
                        prevId = node->next;
                        if (node->next != uint.MaxValue) {
                            node = (MemoryAllocator.BlockHeader*)(zone->root.ptr + node->next);
                        } else {
                            break;
                        }
                    } while (true);
                    zones[i] = new ZoneInfo() {
                        zone = *zone,
                        blocks = blocks.ToArray(),
                    };
                }
                return zones;
            }
        }

        public FreeBlocks freeBlocks => this.allocator.freeBlocks;

    }

    public unsafe partial struct MemoryAllocator {
        
        public void CheckConsistency() {
            this.lockSpinner.Lock();
            for (uint i = 0u; i < this.zonesCount; ++i) {
                var zone = this.zones[i].ptr;
                var size = zone->size;
                var curSize = ZONE_HEADER_OFFSET;
                var node = (MemoryAllocator.BlockHeader*)zone->firstBlock.ptr;
                var prevId = ZONE_HEADER_OFFSET;
                do {
                    curSize += node->size + (uint)sizeof(BlockHeader);
                    node->CheckConsistency(this, prevId, i);
                    prevId = node->next;
                    if (node->next != uint.MaxValue) {
                        node = (MemoryAllocator.BlockHeader*)(zone->root.ptr + node->next);
                    } else {
                        break;
                    }
                } while (true);
                UnityEngine.Assertions.Assert.IsTrue(curSize == size);
            }
            this.lockSpinner.Unlock();
        }

        [System.Diagnostics.ConditionalAttribute(COND.ALLOCATOR_VALIDATION)]
        public static void CheckPtr(in MemoryAllocator allocator, MemPtr ptr) {
            var root = allocator.zones[ptr.zoneId].ptr->root.ptr;
            var header = (BlockHeader*)(root + ptr.offset - sizeof(BlockHeader));
            var memPtrOffset = ptr.offset - sizeof(BlockHeader);
            if (header->next != uint.MaxValue) {
                var next = (BlockHeader*)(root + header->next);
                UnityEngine.Assertions.Assert.IsTrue(next->prev == memPtrOffset);
            }
            if (header->prev != uint.MaxValue) {
                var next = (BlockHeader*)(root + header->prev);
                UnityEngine.Assertions.Assert.IsTrue(next->next == memPtrOffset);
            }
        }

        [System.Diagnostics.ConditionalAttribute(COND.ALLOCATOR_VALIDATION)]
        public static void CheckConsistency(ref MemoryAllocator allocator) {
            allocator.CheckConsistency();
        }
        
        [System.Diagnostics.ConditionalAttribute(COND.ALLOCATOR_VALIDATION)]
        public static void ValidateConsistency(ref MemoryAllocator allocator) {
            allocator.CheckConsistency();
        }

    }

}