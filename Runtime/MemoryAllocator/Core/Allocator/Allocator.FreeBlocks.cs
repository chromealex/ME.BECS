namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    /// <summary>
    /// Indexes allocator blocks available for reuse.
    /// </summary>
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct FreeBlocks {
        
        /// <summary>
        /// Pots constant used by <c>FreeBlocks</c>.
        /// </summary>
        public const uint POTS = 16u;

        /// <summary>
        /// Stores a block record used by <c>FreeBlocks</c>.
        /// </summary>
        [IgnoreProfiler]
        #if !BECS_IL2CPP_OPTIONS_DISABLE
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
        #endif
        public struct Block {

            /// <summary>
            /// Free allocator blocks available for reuse.
            /// </summary>
            public UnsafeList<MemPtr> freeBlocks;

            /// <summary>
            /// Adds the supplied entry to block.
            /// </summary>
            [INLINE(256)][IgnoreProfiler]
            public void Add(MemoryAllocator.BlockHeader* header, in MemPtr memPtr) {
                header->freeIndex = (uint)this.freeBlocks.Length;
                this.freeBlocks.Add(memPtr);
            }

            /// <summary>
            /// Removes the specified entry from block.
            /// </summary>
            [INLINE(256)][IgnoreProfiler]
            public void Remove(in MemoryAllocator allocator, MemoryAllocator.BlockHeader* header) {
                var last = this.freeBlocks[this.freeBlocks.Length - 1];
                var lastHeader = (MemoryAllocator.BlockHeader*)allocator.GetPtr(last);
                lastHeader->freeIndex = header->freeIndex;
                this.freeBlocks.RemoveAtSwapBack((int)header->freeIndex);
            }

            /// <summary>
            /// Removes and returns the next entry according to this container's ordering.
            /// </summary>
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

            /// <summary>
            /// Releases the resources owned by this block instance.
            /// </summary>
            [INLINE(256)][IgnoreProfiler]
            public void Dispose() {
                this.freeBlocks.Dispose();
            }

        }
        
        /// <summary>
        /// Free allocator blocks available for reuse.
        /// </summary>
        public UnsafeList<Block> freeBlocks;
        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.freeBlocks.IsCreated;

        /// <summary>
        /// Allocator used to access or manage the associated native storage.
        /// </summary>
        public Allocator Allocator {
            set => this.freeBlocks.Allocator = value;
        }

        /// <summary>
        /// Initializes free blocks state from the supplied context.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public void Initialize(int capacity, Allocator allocator) {
            this.freeBlocks = new UnsafeList<Block>((int)FreeBlocks.POTS, allocator);
            for (int i = 0; i < FreeBlocks.POTS; ++i) {
                this.freeBlocks.Add(new Block() {
                    freeBlocks = new UnsafeList<MemPtr>(capacity, allocator, NativeArrayOptions.ClearMemory),
                });
            }
        }

        /// <summary>
        /// Releases the resources owned by this free blocks instance.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public void Dispose() {
            for (int i = 0; i < this.freeBlocks.Length; ++i) {
                this.freeBlocks[i].Dispose();
            }
            this.freeBlocks.Dispose();
        }

        /// <summary>
        /// Copies the supplied source state into this free blocks instance.
        /// </summary>
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

        /// <summary>
        /// Writes free blocks to the supplied serialized representation.
        /// </summary>
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
        
        /// <summary>
        /// Restores free blocks from the supplied serialized representation.
        /// </summary>
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

        /// <summary>
        /// Adds the supplied entry to free blocks.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public void Add(in MemoryAllocator allocator, MemoryAllocator.BlockHeader* header, uint zoneId) {
            ref var block = ref this.GetBlockExact(header->size);
            block.Add(header, allocator.GetSafePtr((byte*)header, zoneId));
        }

        /// <summary>
        /// Adds the supplied entry to free blocks.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public void Add(in MemoryAllocator allocator, MemoryAllocator.BlockHeader* header, MemPtr memPtr) {
            ref var block = ref this.GetBlockExact(header->size);
            block.Add(header, memPtr);
        }

        /// <summary>
        /// Removes and returns the next entry according to this container's ordering.
        /// </summary>
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

        /// <summary>
        /// Removes the specified entry from free blocks.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public void Remove(in MemoryAllocator allocator, MemoryAllocator.BlockHeader* header) {
            ref var block = ref this.GetBlockExact(header->size);
            block.Remove(in allocator, header);
        }

        /// <summary>
        /// Resolves the requested address to a native pointer.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly MemPtr GetPtr(in MemoryAllocator.BlockHeader header) {
            ref var block = ref this.GetBlockExact(header.size);
            return block.freeBlocks[(int)header.freeIndex];
        }

        /// <summary>
        /// Returns size.
        /// </summary>
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