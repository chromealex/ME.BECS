using System;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

namespace ME.BECS {

    /// <summary>
    /// Defines temp allocator state and operations.
    /// </summary>
    [IgnoreProfiler]
    [BurstCompile]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct TempAllocator : AllocatorManager.IAllocator {

        /// <summary>
        /// Stores a block record used by <c>TempAllocator</c>.
        /// </summary>
        [IgnoreProfiler]
        #if !BECS_IL2CPP_OPTIONS_DISABLE
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
        #endif
        public struct Block {

            /// <summary>
            /// Describes a block of native memory.
            /// </summary>
            [IgnoreProfiler]
            public struct MemoryBlock {

                private byte* data;
                private uint position;
                private uint size;

                /// <summary>
                /// Number of bytes allocated for the associated storage.
                /// </summary>
                public uint BytesAllocated => this.size;
                /// <summary>
                /// Number of allocated bytes currently in use.
                /// </summary>
                public uint BytesUsed => this.position;

                /// <summary>
                /// Initializes memory block state from the supplied context.
                /// </summary>
                public void Initialize(uint initialSize) {
                    this.data = (byte*)UnsafeUtility.Malloc(initialSize, UnsafeUtility.AlignOf<byte>(), Allocator.Persistent);
                    this.size = initialSize;
                    this.position = 0u;
                }
                
                /// <summary>
                /// Allocates native storage and returns its allocator-relative address.
                /// </summary>
                public byte* Alloc(uint size) {
                    if (this.position + size > this.size) return null;
                    var ptr = this.data + this.position;
                    this.position += size;
                    return ptr;
                }

                /// <summary>
                /// Releases the resources owned by this memory block instance.
                /// </summary>
                public void Dispose() {
                    UnsafeUtility.Free(this.data, Allocator.Persistent);
                }

                /// <summary>
                /// Resets allocation positions for reuse while retaining the allocated storage.
                /// </summary>
                public void Rewind() {
                    this.position = 0u;
                }

            }

            private UnsafeList<MemoryBlock> data;
            private uint rover;
            private uint initialSize;

            /// <summary>
            /// Blocks allocated used by <c>TempAllocator.Block</c>.
            /// </summary>
            public uint BlocksAllocated => (uint)this.data.Length;

            /// <summary>
            /// Number of bytes allocated for the associated storage.
            /// </summary>
            public uint BytesAllocated {
                get {
                    var count = 0u;
                    for (uint i = 0u; i < this.data.Length; ++i) {
                        count += (this.data.Ptr + i)->BytesAllocated;
                    }
                    return count;
                }
            }

            /// <summary>
            /// Number of allocated bytes currently in use.
            /// </summary>
            public uint BytesUsed {
                get {
                    var count = 0u;
                    for (uint i = 0u; i < this.data.Length; ++i) {
                        count += (this.data.Ptr + i)->BytesUsed;
                    }
                    return count;
                }
            }

            /// <summary>
            /// Initializes block state from the supplied context.
            /// </summary>
            public void Initialize(uint initialSize) {
                this.initialSize = initialSize;
                this.data = new UnsafeList<MemoryBlock>(4, Allocator.Persistent);
                this.rover = 0u;
                this.AddBlock(initialSize);
            }

            /// <summary>
            /// Allocates native storage and returns its allocator-relative address.
            /// </summary>
            public byte* Alloc(uint size) {
                while (true) {
                    for (uint i = this.rover; i < this.data.Length; ++i) {
                        var ptr = (this.data.Ptr + i)->Alloc(size);
                        if (ptr != null) {
                            this.rover = i;
                            return ptr;
                        }
                    }
                    this.AddBlock(size);
                }
            }

            private void AddBlock(uint size) {
                var block = new MemoryBlock();
                block.Initialize(math.max(this.initialSize, size));
                this.data.Add(block);
            }

            /// <summary>
            /// Releases the resources owned by this block instance.
            /// </summary>
            public void Dispose() {
                for (uint i = 0u; i < this.data.Length; ++i) {
                    (this.data.Ptr + i)->Dispose();
                }
                this.data.Dispose();
            }

            /// <summary>
            /// Resets allocation positions for reuse while retaining the allocated storage.
            /// </summary>
            public void Rewind() {
                for (uint i = 0u; i < this.data.Length; ++i) {
                    (this.data.Ptr + i)->Rewind();
                }
                this.rover = 0u;
            }

        }
        
        private AllocatorManager.AllocatorHandle handle;

        private Block* blocksPerThread;
        
        /// <summary>
        /// Blocks allocated used by <c>TempAllocator</c>.
        /// </summary>
        public uint BlocksAllocated {
            get {
                var count = 0u;
                for (uint i = 0; i < JobUtils.ThreadsCount; ++i) {
                    count += this.blocksPerThread[i].BlocksAllocated;
                }
                return count;
            }
        }

        /// <summary>
        /// Number of bytes allocated for the associated storage.
        /// </summary>
        public uint BytesAllocated {
            get {
                var count = 0u;
                for (uint i = 0; i < JobUtils.ThreadsCount; ++i) {
                    count += this.blocksPerThread[i].BytesAllocated;
                }
                return count;
            }
        }

        /// <summary>
        /// Number of allocated bytes currently in use.
        /// </summary>
        public uint BytesUsed {
            get {
                var count = 0u;
                for (uint i = 0; i < JobUtils.ThreadsCount; ++i) {
                    count += this.blocksPerThread[i].BytesUsed;
                }
                return count;
            }
        }

        /// <summary>
        /// Initializes temp allocator state from the supplied context.
        /// </summary>
        public void Initialize(uint initialSize) {
            var count = JobUtils.ThreadsCount;
            this.blocksPerThread = (Block*)UnsafeUtility.Malloc(sizeof(Block) * count, UnsafeUtility.AlignOf<Block>(), Allocator.Persistent);
            for (uint i = 0u; i < count; ++i) {
                (this.blocksPerThread + i)->Initialize(initialSize);
            }
        }
        
        /// <summary>
        /// Releases the resources owned by this temp allocator instance.
        /// </summary>
        public void Dispose() {
            var count = JobUtils.ThreadsCount;
            for (uint i = 0u; i < count; ++i) {
                (this.blocksPerThread + i)->Dispose();
            }
            UnsafeUtility.Free(this.blocksPerThread, Allocator.Persistent);
        }

        /// <summary>
        /// Resets the positions of per-thread allocation blocks for reuse while retaining the allocated blocks.
        /// </summary>
        public void Rewind() {
            var count = JobUtils.ThreadsCount;
            for (uint i = 0u; i < count; ++i) {
                (this.blocksPerThread + i)->Rewind();
            }
        }

        /// <summary>
        /// Attempts to  and reports whether the operation succeeded.
        /// </summary>
        public int Try(ref AllocatorManager.Block block) {
            if (block.Range.Pointer == IntPtr.Zero) {
                // Make the alignment multiple of cacheline size
                var alignment = math.max((uint)JobsUtility.CacheLineSize, (uint)block.Alignment);
                var extra = alignment != JobsUtility.CacheLineSize ? 1u : 0u;
                var cachelineMask = JobsUtility.CacheLineSize - 1u;
                if (extra == 1u) {
                    alignment = (alignment + cachelineMask) & ~cachelineMask;
                }
                // Adjust the size to be multiple of alignment, add extra alignment
                // to size if alignment is more than cacheline size
                var mask = alignment - 1u;
                var size = ((uint)block.Bytes + extra * alignment + mask) & ~mask;
                var index = JobsUtility.ThreadIndex;
                var thread = (this.blocksPerThread + index);
                var ptr = thread->Alloc(size);
                block.Range.Pointer = (IntPtr)ptr;
                block.AllocatedItems = block.Range.Items;
                return 0;
            } else {
                // To free memory, no-op unless allocator enables individual block to be freed
                if (block.Range.Items == 0) {
                    return 0;
                }
            }
            return -1;
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        internal static int Try(IntPtr state, ref AllocatorManager.Block block) => ((TempAllocator*)state)->Try(ref block);

        /// <summary>
        /// Function used by <c>TempAllocator</c>.
        /// </summary>
        public AllocatorManager.TryFunction Function => Try;
        /// <summary>
        /// Handle used by <c>TempAllocator</c>.
        /// </summary>
        public AllocatorManager.AllocatorHandle Handle {
            get => this.handle;
            set => this.handle = value;
        }
        /// <summary>
        /// Gets to allocator; this implementation returns <c>this.handle.ToAllocator</c>.
        /// </summary>
        public Allocator ToAllocator => this.handle.ToAllocator;
        /// <summary>
        /// Gets is custom allocator; this implementation returns <c>this.handle.IsCustomAllocator</c>.
        /// </summary>
        public bool IsCustomAllocator => this.handle.IsCustomAllocator;
        
    }

}