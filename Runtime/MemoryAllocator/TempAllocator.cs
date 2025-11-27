using System;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

namespace ME.BECS {

    [IgnoreProfiler]
    [BurstCompile]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct TempAllocator : AllocatorManager.IAllocator {

        [IgnoreProfiler]
        #if !BECS_IL2CPP_OPTIONS_DISABLE
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
        #endif
        public struct Block {

            [IgnoreProfiler]
            public struct MemoryBlock {

                private byte* data;
                private uint position;
                private uint size;

                public uint BytesAllocated => this.size;
                public uint BytesUsed => this.position;

                public void Initialize(uint initialSize) {
                    this.data = (byte*)UnsafeUtility.Malloc(initialSize, UnsafeUtility.AlignOf<byte>(), Allocator.Persistent);
                    this.size = initialSize;
                    this.position = 0u;
                }
                
                public byte* Alloc(uint size) {
                    if (this.position + size > this.size) return null;
                    var ptr = this.data + this.position;
                    this.position += size;
                    return ptr;
                }

                public void Dispose() {
                    UnsafeUtility.Free(this.data, Allocator.Persistent);
                }

                public void Rewind() {
                    this.position = 0u;
                }

            }

            private UnsafeList<MemoryBlock> data;
            private uint rover;
            private uint initialSize;

            public uint BlocksAllocated => (uint)this.data.Length;

            public uint BytesAllocated {
                get {
                    var count = 0u;
                    for (uint i = 0u; i < this.data.Length; ++i) {
                        count += (this.data.Ptr + i)->BytesAllocated;
                    }
                    return count;
                }
            }

            public uint BytesUsed {
                get {
                    var count = 0u;
                    for (uint i = 0u; i < this.data.Length; ++i) {
                        count += (this.data.Ptr + i)->BytesUsed;
                    }
                    return count;
                }
            }

            public void Initialize(uint initialSize) {
                this.initialSize = initialSize;
                this.data = new UnsafeList<MemoryBlock>(4, Allocator.Persistent);
                this.rover = 0u;
                this.AddBlock(initialSize);
            }

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

            public void Dispose() {
                for (uint i = 0u; i < this.data.Length; ++i) {
                    (this.data.Ptr + i)->Dispose();
                }
                this.data.Dispose();
            }

            public void Rewind() {
                for (uint i = 0u; i < this.data.Length; ++i) {
                    (this.data.Ptr + i)->Rewind();
                }
                this.rover = 0u;
            }

        }
        
        private AllocatorManager.AllocatorHandle handle;

        private Block* blocksPerThread;
        
        public uint BlocksAllocated {
            get {
                var count = 0u;
                for (uint i = 0; i < JobUtils.ThreadsCount; ++i) {
                    count += this.blocksPerThread[i].BlocksAllocated;
                }
                return count;
            }
        }

        public uint BytesAllocated {
            get {
                var count = 0u;
                for (uint i = 0; i < JobUtils.ThreadsCount; ++i) {
                    count += this.blocksPerThread[i].BytesAllocated;
                }
                return count;
            }
        }

        public uint BytesUsed {
            get {
                var count = 0u;
                for (uint i = 0; i < JobUtils.ThreadsCount; ++i) {
                    count += this.blocksPerThread[i].BytesUsed;
                }
                return count;
            }
        }

        public void Initialize(uint initialSize) {
            var count = JobUtils.ThreadsCount;
            this.blocksPerThread = (Block*)UnsafeUtility.Malloc(sizeof(Block) * count, UnsafeUtility.AlignOf<Block>(), Allocator.Persistent);
            for (uint i = 0u; i < count; ++i) {
                (this.blocksPerThread + i)->Initialize(initialSize);
            }
        }
        
        public void Dispose() {
            var count = JobUtils.ThreadsCount;
            for (uint i = 0u; i < count; ++i) {
                (this.blocksPerThread + i)->Dispose();
            }
            UnsafeUtility.Free(this.blocksPerThread, Allocator.Persistent);
        }

        public void Rewind() {
            var count = JobUtils.ThreadsCount;
            for (uint i = 0u; i < count; ++i) {
                (this.blocksPerThread + i)->Rewind();
            }
        }

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

        public AllocatorManager.TryFunction Function => Try;
        public AllocatorManager.AllocatorHandle Handle {
            get => this.handle;
            set => this.handle = value;
        }
        public Allocator ToAllocator => this.handle.ToAllocator;
        public bool IsCustomAllocator => this.handle.IsCustomAllocator;
        
    }

}