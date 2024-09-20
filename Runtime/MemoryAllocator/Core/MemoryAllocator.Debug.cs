namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

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

    public unsafe partial struct MemoryAllocator {

        public static void CheckConsistency(ref MemoryAllocator allocator) {

            allocator.lockIndex.Lock();
            for (int i = 0; i < allocator.zonesListCount; ++i) {
                var zone = allocator.zonesList[i];
                if (zone == null) {
                    continue;
                }

                if (MemoryAllocator.ZmCheckHeap(zone, out var blockIndex, out var index) == false) {
                    UnityEngine.Debug.LogError($"zone {i}, block {blockIndex}, index {index}, thread {Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex}");
                }
            }
            allocator.lockIndex.Unlock();
            
        }

        [System.Diagnostics.ConditionalAttribute(COND.ALLOCATOR_VALIDATION)]
        public static void ValidateConsistency(ref MemoryAllocator allocator) {

            CheckConsistency(ref allocator);

        }

        [System.Diagnostics.ConditionalAttribute("MEMORY_ALLOCATOR_BOUNDS_CHECK")]
        public static void CHECK_PTR(void* ptr) {
            if (ptr == null) {
                throw new System.ArgumentException("CHECK_PTR failed");
            }
        }

        [System.Diagnostics.ConditionalAttribute("MEMORY_ALLOCATOR_BOUNDS_CHECK")]
        public static void CHECK_ZONE_ID(int id) {
            if (id != MemoryAllocator.ZONE_ID) {
                throw new System.ArgumentException("ZmFree: freed a pointer without ZONEID");
            }
        }

        [INLINE(256)]
        public static void ZmDumpHeap(MemZone* zone, System.Collections.Generic.List<string> results) {
            results.Add($"zone size: {zone->size}; location: {new System.IntPtr(zone)}; rover block offset: {zone->rover.value}");
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
        
        #if LOGS_ENABLED && UNITY_EDITOR
        public static bool startLog;
        public static System.Collections.Generic.Dictionary<MemPtr, string> strList = new System.Collections.Generic.Dictionary<MemPtr, string>();

        [Unity.Burst.BurstDiscardAttribute]
        public static void LogAdd(in MemPtr memPtr, long size) {
            if (startLog == true) {
                var str = "ALLOC: " + memPtr + ", SIZE: " + size;
                strList.Add(memPtr, str + "\n" + UnityEngine.StackTraceUtility.ExtractStackTrace());
            }
        }

        [Unity.Burst.BurstDiscardAttribute]
        public static void LogRemove(in MemPtr memPtr) {
            strList.Remove(memPtr);
        }

        [UnityEditor.MenuItem("ME.ECS/Debug/Allocator: Start Log")]
        public static void StartLog() {
            startLog = true;
        }
        
        [UnityEditor.MenuItem("ME.ECS/Debug/Allocator: End Log")]
        public static void EndLog() {
            startLog = false;
            MemoryAllocator.strList.Clear();
        }
        
        [UnityEditor.MenuItem("ME.ECS/Debug/Allocator: Print Log")]
        public static void PrintLog() {
            foreach (var item in MemoryAllocator.strList) {
                Logger.Core.Log(item.Key + "\n" + item.Value);
            }
        }
        #endif

    }

}
