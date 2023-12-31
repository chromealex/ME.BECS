namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Jobs.LowLevel.Unsafe;

    [BURST]
    public unsafe struct ReadWriteSpinner : IIsCreated {

        private const uint INTS_PER_CACHE_LINE = JobsUtility.CacheLineSize / sizeof(int);
        private MemPtr value;
        private int readValue;
        private int writeValue;
        private int* ptr;

        public bool isCreated => this.value.IsValid();

        [INLINE(256)]
        public static ReadWriteSpinner Create(State* state) {
            var size = TSize<int>.size * INTS_PER_CACHE_LINE * JobsUtility.MaxJobThreadCount;
            var arr = MemoryAllocatorExt.Alloc(ref state->allocator, size, out var ptr);
            state->allocator.MemClear(arr, 0L, size);
            return new ReadWriteSpinner() {
                value = arr,
                ptr = (int*)ptr,
            };
        }

        [INLINE(256)]
        private int ReadCount(State* state) {
            var cnt = 0;
            for (uint i = 0u; i < JobsUtility.MaxJobThreadCount; ++i) {
                cnt += this.ptr[i * INTS_PER_CACHE_LINE];
            }
            return cnt;
        }
        
        [INLINE(256)]
        public void ReadBegin(State* state) {
            E.IS_CREATED(this);
            // wait if we have write op running
            while (System.Threading.Volatile.Read(ref this.writeValue) == 1) {
                Unity.Burst.Intrinsics.Common.Pause();
            }
            // acquire read op
            ++this.ptr[INTS_PER_CACHE_LINE * JobsUtility.ThreadIndex];
        }

        [INLINE(256)]
        public void ReadEnd(State* state) {
            E.IS_CREATED(this);
            // release read op
            --this.ptr[INTS_PER_CACHE_LINE * JobsUtility.ThreadIndex];
        }

        [INLINE(256)]
        public void WriteBegin(State* state) {
            E.IS_CREATED(this);
            // acquire write op
            while (System.Threading.Interlocked.CompareExchange(ref this.writeValue, 1, 0) != 0) {
                Unity.Burst.Intrinsics.Common.Pause();
            }
            // wait if we have read op running
            while (this.ReadCount(state) > 0) {
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }

        [INLINE(256)]
        public void WriteEnd() {
            E.IS_CREATED(this);
            // release write op
            while (System.Threading.Interlocked.CompareExchange(ref this.writeValue, 0, 1) != 1) {
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool value) {
            
            this.ptr = (int*)MemoryAllocatorExt.GetUnsafePtr(in allocator, this.value);
            
        }

    }
    
    [BURST]
    public struct Spinner {
        
        private int lockIndex;

        /// <summary>
        /// Continually spin until the lock can be acquired.
        /// </summary>
        [INLINE(256)]
        public void Acquire() {
            
            for(;;) {
                // Optimistically assume the lock is free on the first try.
                if (System.Threading.Interlocked.CompareExchange(ref this.lockIndex, 1, 0) == 0) {
                    return;
                }

                // Wait for lock to be released without generate cache misses.
                while (System.Threading.Volatile.Read(ref this.lockIndex) == 1) {
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
        [INLINE(256)]
        public bool TryAcquire() {
            // First do a memory load (read) to check if lock is free in order to prevent uncessary cache missed.
            return System.Threading.Volatile.Read(ref this.lockIndex) == 0 &&
                System.Threading.Interlocked.CompareExchange(ref this.lockIndex, 1, 0) == 0;
        }

        /// <summary>
        /// Try to acquire the lock, and spin only if <paramref name="spin"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="spin">Set to true to spin the lock.</param>
        /// <returns><see langword="true"/> if the lock was acquired, <see langword="false" otherwise./></returns>
        [INLINE(256)]
        public bool TryAcquire(bool spin) {
            if (spin == true) {
                this.Acquire();
                return true;
            }

            return this.TryAcquire();
        }

        /// <summary>
        /// Release the lock
        /// </summary>
        [INLINE(256)]
        public void Release() {
            System.Threading.Volatile.Write(ref this.lockIndex, 0);
        }
    }

    [BURST]
    public struct LockSpinner {
        
        private int value;
        [INLINE(256)]
        public void Lock() {
            while (0 != System.Threading.Interlocked.CompareExchange(ref this.value, 1, 0)) {
                Unity.Burst.Intrinsics.Common.Pause();
            }
            System.Threading.Interlocked.MemoryBarrier();
        }
        
        [INLINE(256)]
        public void Unlock() {
            System.Threading.Interlocked.MemoryBarrier();
            while (1 != System.Threading.Interlocked.CompareExchange(ref this.value, 0, 1)) {
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }
        
    }
    
}