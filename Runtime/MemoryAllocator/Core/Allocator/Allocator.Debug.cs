namespace ME.BECS.Memory {

    public unsafe class AllocatorDebugProxy {

        public struct ZoneInfo {

            public Allocator.Zone zone;
            public string[] blocks;

        }

        private readonly Allocator allocator;
        public AllocatorDebugProxy(Allocator allocator) {
            this.allocator = allocator;
        }

        public ZoneInfo[] zones {
            get {
                var zones = new ZoneInfo[this.allocator.zonesCount];
                for (uint i = 0u; i < this.allocator.zonesCount; ++i) {
                    var zone = this.allocator.zones[i].ptr;
                    var blocks = new System.Collections.Generic.List<string>();
                    var node = (Allocator.BlockHeader*)zone->data.ptr;
                    blocks.Add(node->ToString());
                    while (node->next != uint.MaxValue) {
                        var nodeId = node->next;
                        node = (Allocator.BlockHeader*)(zone->data.ptr + node->next);
                        blocks.Add($"{nodeId}: {node->ToString()}");
                    }
                    zones[i] = new ZoneInfo() {
                        zone = *zone,
                        blocks = blocks.ToArray(),
                    };
                }
                return zones;
            }
        }

        public Unity.Collections.LowLevel.Unsafe.UnsafeList<MemPtr> freeBlocks => this.allocator.freeBlocks;

    }

    public unsafe partial struct Allocator {
        
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