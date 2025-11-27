namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct FreeBlocks {
        
        public const uint POTS = 16u;

        [IgnoreProfiler]
        #if !BECS_IL2CPP_OPTIONS_DISABLE
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
        #endif
        public struct Block {

            public UnsafeList<MemPtr> freeBlocks;

            [INLINE(256)][IgnoreProfiler]
            public void Add(MemoryAllocator.BlockHeader* header, in MemPtr memPtr) {
                header->freeIndex = (uint)this.freeBlocks.Length;
                this.freeBlocks.Add(memPtr);
            }

            [INLINE(256)][IgnoreProfiler]
            public void Remove(in MemoryAllocator allocator, MemoryAllocator.BlockHeader* header) {
                var last = this.freeBlocks[this.freeBlocks.Length - 1];
                var lastHeader = (MemoryAllocator.BlockHeader*)allocator.GetPtr(last);
                lastHeader->freeIndex = header->freeIndex;
                this.freeBlocks.RemoveAtSwapBack((int)header->freeIndex);
            }

            [INLINE(256)][IgnoreProfiler]
            public MemPtr Pop(in MemoryAllocator allocator, uint size, bool iterateAll = false) {
                if (this.freeBlocks.Length == 0) return MemPtr.Invalid;
                if (iterateAll == true) {
                    for (int i = 0; i < this.freeBlocks.Length; ++i) {
                        var ptr = this.freeBlocks[i];
                        var blockPtr = allocator.GetPtr(ptr);
                        var header = (MemoryAllocator.BlockHeader*)blockPtr;
                        if (size > header->size) continue;
                        this.Remove(in allocator, header);
                        return ptr;
                    }
                    return MemPtr.Invalid;
                } else {
                    var ptr = this.freeBlocks[this.freeBlocks.Length - 1];
                    var blockPtr = allocator.GetPtr(ptr);
                    var header = (MemoryAllocator.BlockHeader*)blockPtr;
                    if (size > header->size) return MemPtr.Invalid;
                    --this.freeBlocks.Length;
                    return ptr;
                }
            }

            [INLINE(256)][IgnoreProfiler]
            public void Dispose() {
                this.freeBlocks.Dispose();
            }

        }
        
        public UnsafeList<Block> freeBlocks;
        public bool IsCreated => this.freeBlocks.IsCreated;

        public Allocator Allocator {
            set => this.freeBlocks.Allocator = value;
        }

        [INLINE(256)][IgnoreProfiler]
        public void Initialize(int capacity, Allocator allocator) {
            this.freeBlocks = new UnsafeList<Block>((int)FreeBlocks.POTS, allocator);
            for (int i = 0; i < FreeBlocks.POTS; ++i) {
                this.freeBlocks.Add(new Block() {
                    freeBlocks = new UnsafeList<MemPtr>(capacity, allocator, NativeArrayOptions.ClearMemory),
                });
            }
        }

        [INLINE(256)][IgnoreProfiler]
        public void Dispose() {
            for (int i = 0; i < this.freeBlocks.Length; ++i) {
                this.freeBlocks[i].Dispose();
            }
            this.freeBlocks.Dispose();
        }

        [INLINE(256)][IgnoreProfiler]
        public void CopyFrom(in FreeBlocks other) {
            
            this.freeBlocks.Resize(other.freeBlocks.Length, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < other.freeBlocks.Length; ++i) {
                var item = this.freeBlocks[i];
                item.freeBlocks.Allocator = this.freeBlocks.Allocator;
                item.freeBlocks.CopyFrom(other.freeBlocks[i].freeBlocks);
                this.freeBlocks[i] = item;
            }

        }

        [INLINE(256)][IgnoreProfiler]
        public void Serialize(ref StreamBufferWriter writer) {
            writer.Write(this.freeBlocks.Length);
            for (int i = 0; i < this.freeBlocks.Length; ++i) {
                var block = this.freeBlocks[i];
                writer.Write(block.freeBlocks.Length);
                if (block.freeBlocks.Length == 0) continue;
                writer.Write(block.freeBlocks.Ptr, (uint)block.freeBlocks.Length);
            }
        }
        
        [INLINE(256)][IgnoreProfiler]
        public void Deserialize(ref StreamBufferReader reader, Allocator allocator) {
            var freeBlocksLength = 0;
            reader.Read(ref freeBlocksLength);
            this.freeBlocks = new UnsafeList<Block>(freeBlocksLength, allocator);
            this.freeBlocks.Length = freeBlocksLength;
            for (int i = 0; i < this.freeBlocks.Length; ++i) {
                var block = this.freeBlocks[i];
                var length = 0;
                reader.Read(ref length);
                block.freeBlocks = new UnsafeList<MemPtr>(length, allocator);
                if (length > 0) {
                    block.freeBlocks.Length = length;
                    reader.Read(ref block.freeBlocks.Ptr, (uint)length);
                }
                this.freeBlocks[i] = block;
            }
        }

        [INLINE(256)][IgnoreProfiler]
        private readonly ref Block GetBlockExact(uint size) {
            var pot = Helpers.NextPot(size);
            var index = (uint)math.log2(pot) - 1u;
            if (index >= this.freeBlocks.Length) {
                index = (uint)(this.freeBlocks.Length - 1);
            }
            return ref UnsafeUtility.ArrayElementAsRef<Block>(this.freeBlocks.Ptr, (int)index);
        }

        [INLINE(256)][IgnoreProfiler]
        private readonly ref Block GetBlockMin(uint size, ref uint index) {
            var pot = Helpers.NextPot(size);
            if (index == 0u) index = (uint)math.log2(pot) - 1;
            while (true) {
                if (index >= this.freeBlocks.Length) {
                    return ref UnsafeUtility.ArrayElementAsRef<Block>(this.freeBlocks.Ptr, this.freeBlocks.Length - 1);
                }
                ref var block = ref UnsafeUtility.ArrayElementAsRef<Block>(this.freeBlocks.Ptr, (int)index);
                if (block.freeBlocks.Length == 0) {
                    ++index;
                    continue;
                }
                return ref block;
            }
        }

        [INLINE(256)][IgnoreProfiler]
        public void Add(in MemoryAllocator allocator, MemoryAllocator.BlockHeader* header, uint zoneId) {
            ref var block = ref this.GetBlockExact(header->size);
            block.Add(header, allocator.GetSafePtr((byte*)header, zoneId));
        }

        [INLINE(256)][IgnoreProfiler]
        public void Add(in MemoryAllocator allocator, MemoryAllocator.BlockHeader* header, MemPtr memPtr) {
            ref var block = ref this.GetBlockExact(header->size);
            block.Add(header, memPtr);
        }

        [INLINE(256)][IgnoreProfiler]
        public MemPtr Pop(in MemoryAllocator allocator, uint size) {
            var index = 0u;
            while (true) {
                ref var block = ref this.GetBlockMin(size, ref index);
                var ptr = block.Pop(in allocator, size, index == this.freeBlocks.Length - 1u);
                if (ptr.IsValid() == false) {
                    if (index >= this.freeBlocks.Length) {
                        // we hit the last block
                        return MemPtr.Invalid;
                    }
                    // try next index
                    ++index;
                    continue;
                }
                return ptr;
            }
        }

        [INLINE(256)][IgnoreProfiler]
        public void Remove(in MemoryAllocator allocator, MemoryAllocator.BlockHeader* header) {
            ref var block = ref this.GetBlockExact(header->size);
            block.Remove(in allocator, header);
        }

        [INLINE(256)][IgnoreProfiler]
        public readonly MemPtr GetPtr(in MemoryAllocator.BlockHeader header) {
            ref var block = ref this.GetBlockExact(header.size);
            return block.freeBlocks[(int)header.freeIndex];
        }

        public readonly uint GetSize(in MemoryAllocator allocator) {
            var freeSize = 0u;
            foreach (var block in this.freeBlocks) {
                foreach (var item in block.freeBlocks) {
                    var header = (MemoryAllocator.BlockHeader*)allocator.GetPtr(item);
                    freeSize += header->size;
                }
            }
            return freeSize;
        }

    }

}