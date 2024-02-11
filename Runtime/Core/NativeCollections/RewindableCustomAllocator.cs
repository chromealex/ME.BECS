namespace ME.BECS.NativeCollections {

    using AOT;
    using Unity.Collections;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using UnityEngine.Assertions;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Mathematics;

    [GenerateTestsForBurstCompatibility]
    internal struct Spinner {

        private int Lock;

        /// <summary>
        /// Continually spin until the lock can be acquired.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Acquire() {
            for (;;) {
                // Optimistically assume the lock is free on the first try.
                if (Interlocked.CompareExchange(ref this.Lock, 1, 0) == 0) {
                    return;
                }

                // Wait for lock to be released without generate cache misses.
                while (Volatile.Read(ref this.Lock) == 1) {
                    continue;
                }

                // Future improvement: the 'continue` instruction above could be swapped for a 'pause' intrinsic
                // instruction when the CPU supports it, to further reduce contention by reducing load-store unit
                // utilization. However, this would need to be optional because if you don't use hyper-threading
                // and you don't care about power efficiency, using the 'pause' instruction will slow down lock
                // acquisition in the contended scenario.
            }
        }

        /// <summary>
        /// Try to acquire the lock and immediately return without spinning.
        /// </summary>
        /// <returns><see langword="true"/> if the lock was acquired, <see langword="false"/> otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryAcquire() {
            // First do a memory load (read) to check if lock is free in order to prevent uncessary cache missed.
            return Volatile.Read(ref this.Lock) == 0 &&
                   Interlocked.CompareExchange(ref this.Lock, 1, 0) == 0;
        }

        /// <summary>
        /// Try to acquire the lock, and spin only if <paramref name="spin"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="spin">Set to true to spin the lock.</param>
        /// <returns><see langword="true"/> if the lock was acquired, <see langword="false" otherwise./></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryAcquire(bool spin) {
            if (spin) {
                this.Acquire();
                return true;
            }

            return this.TryAcquire();
        }

        /// <summary>
        /// Release the lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Release() {
            Volatile.Write(ref this.Lock, 0);
        }

    }

    internal unsafe struct UnmanagedArray<T> : IDisposable where T : unmanaged {

        private void* pointer;
        private int length;
        public int Length => this.length;
        private AllocatorManager.AllocatorHandle allocator;

        public UnmanagedArray(int length, AllocatorManager.AllocatorHandle allocator) {
            this.pointer = UnsafeUtility.Malloc(length, TAlign<T>.alignInt, allocator.ToAllocator);
            this.length = length;
            this.allocator = allocator;
        }

        public void Dispose() {
            UnsafeUtility.Free(this.pointer, this.allocator.ToAllocator);
        }

        public unsafe T* GetUnsafePointer() {
            return (T*)this.pointer;
        }

        public ref T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                unsafe {
                    return ref ((T*)this.pointer)[index];
                }
            }
        }

    }

    /// <summary>
    /// An allocator that is fast like a linear allocator, is threadsafe, and automatically invalidates
    /// all allocations made from it, when "rewound" by the user.
    /// </summary>
    [BurstCompile]
    public struct RewindableCustomAllocator : AllocatorManager.IAllocator {

        internal struct Union {

            internal long data;

            // Number of bits used to store current position in a block to give out memory.
            // This limits the maximum block size to 1TB (2^40).
            private const int currentBits = 40;
            // Offset of current position in long
            private const int currentOffset = 0;
            // Number of bits used to store the allocation count in a block
            private const long currentMask = (1L << currentBits) - 1;

            // Number of bits used to store allocation count in a block.
            // This limits the maximum number of allocations per block to 16 millions (2^24)
            private const int allocCountBits = 24;
            // Offset of allocation count in long
            private const int allocCountOffset = currentOffset + currentBits;
            private const long allocCountMask = (1L << allocCountBits) - 1;

            // Current position in a block to give out memory
            internal long current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (this.data >> currentOffset) & currentMask;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    this.data &= ~(currentMask << currentOffset);
                    this.data |= (value & currentMask) << currentOffset;
                }
            }

            // The number of allocations in a block
            internal long allocCount {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (this.data >> allocCountOffset) & allocCountMask;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    this.data &= ~(allocCountMask << allocCountOffset);
                    this.data |= (value & allocCountMask) << allocCountOffset;
                }
            }

        }

        [GenerateTestsForBurstCompatibility]
        internal unsafe struct MemoryBlock : IDisposable {

            // can't align any coarser than this many bytes
            public const int kMaximumAlignment = 16384;
            // pointer to contiguous memory
            public byte* pointer;
            // how many bytes of contiguous memory it points to
            public long bytes;
            // Union of current position to give out memory and allocation counts
            public Union union;

            public MemoryBlock(long bytes, int alignment) {
                this.pointer = (byte*)UnsafeUtility.Malloc(bytes, alignment, Allocator.Domain);
                Assert.IsTrue(this.pointer != null, "Memory block allocation failed, system out of memory");
                this.bytes = bytes;
                this.union = default;
            }

            public void Free() {
                this.union = default;
            }

            public void Rewind() {
                this.union = default;
            }

            public void Dispose() {
                UnsafeUtility.Free(this.pointer, Allocator.Domain);
                this.pointer = null;
                this.bytes = 0;
                this.union = default;
            }

            public bool Contains(IntPtr ptr) {
                var pointer = (void*)ptr;
                return pointer >= this.pointer && pointer < this.pointer + this.union.current;
            }

        };

        // Log2 of Maximum memory block size.  Cannot exceed MemoryBlock.Union.currentBits.
        //private const int kLog2MaxMemoryBlockSize = 26;

        // Maximum memory block size.  Can exceed maximum memory block size if user requested more.
        private const long kMaxMemoryBlockSize = 4;//1L << kLog2MaxMemoryBlockSize; // 64MB

        /// Minimum memory block size, 128KB.
        private const long kMinMemoryBlockSize = 4;//128 * 1024;

        /// Maximum number of memory blocks.
        private const int kMaxNumBlocks = 64_000 * 2; // 1.5mb = 64000

        // Bit mask (bit 31) of the memory block busy flag indicating whether the block is busy rewinding.
        private const int kBlockBusyRewindMask = 0x1 << 31;

        // Bit mask of the memory block busy flag indicating whether the block is busy allocating.
        private const int kBlockBusyAllocateMask = ~kBlockBusyRewindMask;

        private LockSpinner spinner;
        private AllocatorManager.AllocatorHandle handle;
        private UnmanagedArray<MemoryBlock> block;
        private int last; // highest-index block that has memory to allocate from
        private int used; // highest-index block that we actually allocated from, since last rewind
        private byte enableBlockFree; // flag indicating if allocator enables individual block free
        private byte reachMaxBlockSize; // flag indicating if reach maximum block size

        /// <summary>
        /// Initializes the allocator. Must be called before first use.
        /// </summary>
        /// <param name="initialSizeInBytes">The initial capacity of the allocator, in bytes</param>
        /// <param name="enableBlockFree">A flag indicating if allocator enables individual block free</param>
        public void Initialize(int initialSizeInBytes, bool enableBlockFree = false) {
            this.spinner = default;
            this.block = new UnmanagedArray<MemoryBlock>(kMaxNumBlocks, Allocator.Domain);
            // Initial block size should be larger than min block size
            //var blockSize = initialSizeInBytes > kMinMemoryBlockSize ? initialSizeInBytes : kMinMemoryBlockSize;
            //this.block[0] = new MemoryBlock(blockSize);
            this.last = this.used = 0;
            this.enableBlockFree = enableBlockFree ? (byte)1 : (byte)0;
            this.reachMaxBlockSize = initialSizeInBytes >= kMaxMemoryBlockSize ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Property to get and set enable block free flag, a flag indicating whether the allocator should enable individual block to be freed.
        /// </summary>
        public bool EnableBlockFree {
            get => this.enableBlockFree != 0;
            set => this.enableBlockFree = value ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Retrieves the number of memory blocks that the allocator has requested from the system.
        /// </summary>
        public int BlocksAllocated => (int)(this.last);

        internal int BlocksUsed {
            get {
                var cnt = 0;
                for (var i = 0; i < this.last; i++) {
                    if (this.block[i].union.allocCount > 0) ++cnt;
                }

                return cnt;
            }
        }
        
        /// <summary>
        /// Retrieves the size of the initial memory block, as requested in the Initialize function.
        /// </summary>
        public int InitialSizeInBytes => (int)this.block[0].bytes;

        /// <summary>
        /// Retrieves the maximum memory block size.
        /// </summary>
        internal long MaxMemoryBlockSize => kMaxMemoryBlockSize;

        /// <summary>
        /// Retrieves the total bytes of the memory blocks allocated by this allocator.
        /// </summary>
        internal long BytesAllocated {
            get {
                long totalBytes = 0;
                for (var i = 0; i < this.last; i++) {
                    totalBytes += this.block[i].bytes;
                }

                return totalBytes;
            }
        }
        
        internal long BytesUsed {
            get {
                long totalBytes = 0;
                for (var i = 0; i < this.last; i++) {
                    if (this.block[i].union.allocCount > 0) totalBytes += this.block[i].bytes;
                }

                return totalBytes;
            }
        }

        /// <summary>
        /// Rewind the allocator; invalidate all allocations made from it, and potentially also free memory blocks
        /// it has allocated from the system.
        /// </summary>
        public void Rewind() {
            if (JobsUtility.IsExecutingJob) {
                throw new InvalidOperationException("You cannot Rewind a RewindableCustomAllocator from a Job.");
            }

            //handle.Rewind(); // bump the allocator handle version, invalidate all dependents
            while (this.last > this.used) // *delete* all blocks we didn't even allocate from this time around.
            {
                this.block[this.last--].Dispose();
            }

            while (this.used > 0) // simply *rewind* all blocks we used in this update, to avoid allocating again, every update.
            {
                this.block[this.used--].Rewind();
            }

            this.block[0].Rewind();
        }

        /// <summary>
        /// Dispose the allocator. This must be called to free the memory blocks that were allocated from the system.
        /// </summary>
        public void Dispose() {
            if (JobsUtility.IsExecutingJob) {
                throw new InvalidOperationException("You cannot Dispose a RewindableCustomAllocator from a Job.");
            }

            this.used = 0; // so that we delete all blocks in Rewind() on the next line
            this.Rewind();
            this.block[0].Dispose();
            this.block.Dispose();
            this.last = this.used = 0;
        }

        /// <summary>
        /// All allocators must implement this property, in order to be installed in the custom allocator table.
        /// </summary>
        [ExcludeFromBurstCompatTesting("Uses managed delegate")]
        public AllocatorManager.TryFunction Function => Try;

        private unsafe int TryAllocate(ref AllocatorManager.Block block, int startIndex, int lastIndex, long alignedSize, long alignmentMask) {
            for (var best = startIndex; best <= lastIndex; best++) {
                Union oldUnion;
                Union readUnion = default;
                long begin = 0;
                var skip = false;
                readUnion.data = Interlocked.Read(ref this.block[best].union.data);
                do {
                    begin = (readUnion.current + alignmentMask) & ~alignmentMask;
                    if (this.block[best].union.allocCount > 0 && begin + block.Bytes > this.block[best].bytes) {
                        skip = true;
                        break;
                    }

                    oldUnion = readUnion;
                    Union newUnion = default;
                    newUnion.current = begin + alignedSize > this.block[best].bytes ? this.block[best].bytes : begin + alignedSize;
                    newUnion.allocCount = readUnion.allocCount + 1;
                    readUnion.data = Interlocked.CompareExchange(ref this.block[best].union.data, newUnion.data, oldUnion.data);
                } while (readUnion.data != oldUnion.data);

                if (skip) {
                    continue;
                }

                block.Range.Pointer = (IntPtr)(this.block[best].pointer + begin);
                block.AllocatedItems = block.Range.Items;

                Interlocked.MemoryBarrier();
                int oldUsed;
                int readUsed;
                int newUsed;
                readUsed = this.used;
                do {
                    oldUsed = readUsed;
                    newUsed = best > oldUsed ? best : oldUsed;
                    readUsed = Interlocked.CompareExchange(ref this.used, newUsed, oldUsed);
                } while (newUsed != oldUsed);

                return AllocatorManager.kErrorNone;
            }

            return AllocatorManager.kErrorBufferOverflow;
        }

        /// <summary>
        /// Try to allocate, free, or reallocate a block of memory. This is an internal function, and
        /// is not generally called by the user.
        /// </summary>
        /// <param name="block">The memory block to allocate, free, or reallocate</param>
        /// <returns>0 if successful. Otherwise, returns the error code from the allocator function.</returns>
        public unsafe int Try(ref AllocatorManager.Block block) {
            if (block.Range.Pointer == IntPtr.Zero) {
                // Make the alignment multiple of cacheline size
                var alignment = math.max(JobsUtility.CacheLineSize, block.Alignment);
                var extra = alignment != JobsUtility.CacheLineSize ? 1 : 0;
                var cachelineMask = JobsUtility.CacheLineSize - 1;
                if (extra == 1) {
                    alignment = (alignment + cachelineMask) & ~cachelineMask;
                }

                // Adjust the size to be multiple of alignment, add extra alignment
                // to size if alignment is more than cacheline size
                var mask = alignment - 1L;
                var size = (block.Bytes + extra * alignment + mask) & ~mask;

                this.spinner.Lock();
                //var error = 0;
                {
                    var bytes = size;
                    // search for block
                    var useIdx = -1;
                    // looking for exact bytes block or free one
                    for (int i = 0; i < this.last; ++i) {
                        if (this.block[i].union.allocCount == 0 &&
                            this.block[i].bytes == 0) {
                            // free block found
                            // use this block
                            this.block[i] = new MemoryBlock(bytes, block.Alignment);
                            useIdx = i;
                            break;
                        } else if (this.block[i].union.allocCount == 0 &&
                                   bytes == this.block[i].bytes) {
                            // free block - use pointer
                            useIdx = i;
                            break;
                        }
                    }

                    if (useIdx == -1) {
                        // look for low bytes value
                        for (int i = 0; i < this.last; ++i) {
                            if (this.block[i].union.allocCount == 0 &&
                                bytes < this.block[i].bytes) {
                                // free block - use pointer
                                useIdx = i;
                                break;
                            }
                        }
                    }

                    if (useIdx == -1) {
                        // alloc new block
                        var newBlock = new MemoryBlock(bytes, block.Alignment);
                        useIdx = this.last;
                        this.block[useIdx] = newBlock;
                        Interlocked.Increment(ref this.last);
                    }

                    if (useIdx >= 0) {
                        
                        this.block[useIdx].union.current = bytes;
                        this.block[useIdx].union.allocCount = 1;
                        block.Range.Pointer = (IntPtr)(this.block[useIdx].pointer);
                        block.AllocatedItems = block.Range.Items;
                        this.spinner.Unlock();
                        return 0;

                    }

                }
                this.spinner.Unlock();
                return -1;
            }

            // To free memory, no-op unless allocator enables individual block to be freed
            if (block.Range.Items == 0) {
                if (this.enableBlockFree != 0) {
                    for (var blockIndex = 0; blockIndex <= this.last; ++blockIndex) {
                        if (this.block[blockIndex].Contains(block.Range.Pointer) == true) {
                            this.spinner.Lock();
                            this.block[blockIndex].Free();
                            this.spinner.Unlock();
                            break;
                        }
                    }
                }

                return 0; // we could check to see if the pointer belongs to us, if we want to be strict about it.
            }

            return -1;
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        internal static int Try(IntPtr state, ref AllocatorManager.Block block) {
            unsafe {
                return ((RewindableCustomAllocator*)state)->Try(ref block);
            }
        }

        /// <summary>
        /// Retrieve the AllocatorHandle associated with this allocator. The handle is used as an index into a
        /// global table, for times when a reference to the allocator object isn't available.
        /// </summary>
        /// <value>The AllocatorHandle retrieved.</value>
        public AllocatorManager.AllocatorHandle Handle {
            get => this.handle;
            set => this.handle = value;
        }

        /// <summary>
        /// Retrieve the Allocator associated with this allocator.
        /// </summary>
        /// <value>The Allocator retrieved.</value>
        public Allocator ToAllocator => this.handle.ToAllocator;

        /// <summary>
        /// Check whether this AllocatorHandle is a custom allocator.
        /// </summary>
        /// <value>True if this AllocatorHandle is a custom allocator.</value>
        public bool IsCustomAllocator => this.handle.IsCustomAllocator;

        /// <summary>
        /// Check whether this allocator will automatically dispose allocations.
        /// </summary>
        /// <remarks>Allocations made by Rewindable allocator are automatically disposed.</remarks>
        /// <value>Always true</value>
        public bool IsAutoDispose => true;

    }

}