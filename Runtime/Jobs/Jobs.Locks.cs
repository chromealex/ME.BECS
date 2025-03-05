namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Jobs.LowLevel.Unsafe;
    using System.Runtime.InteropServices;
    using static Cuts;

    [BURST(CompileSynchronously = true)]
    public unsafe struct ReadWriteNativeSpinner : IIsCreated {

        private static readonly uint CACHE_LINE_SIZE = _align(TSize<int>.size, JobUtils.CacheLineSize);
        private Unity.Collections.Allocator allocator;
        private safe_ptr value;
        private int readValue;
        private int writeValue;
        
        public bool IsCreated => this.value.ptr != null;

        [INLINE(256)]
        public static ReadWriteNativeSpinner Create(Unity.Collections.Allocator allocator) {
            var size = CACHE_LINE_SIZE * JobUtils.ThreadsCount;
            var arr = _make(size, TAlign<int>.alignInt, allocator);
            _memclear(arr, size);
            return new ReadWriteNativeSpinner() {
                allocator = allocator,
                value = arr,
            };
        }

        [INLINE(256)]
        private int ReadCount() {
            var cnt = 0;
            for (uint i = 0u; i < JobUtils.ThreadsCount; ++i) {
                cnt += *(int*)(this.value + i * CACHE_LINE_SIZE).ptr;
            }
            return cnt;
        }
        
        [INLINE(256)]
        public void ReadBegin() {
            E.IS_CREATED(this);
            // wait if we have to write op running
            #if EXCEPTIONS_INTERNAL
            var i = 100_000_000;
            #endif
            while (System.Threading.Volatile.Read(ref this.writeValue) == 1) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
            // acquire read op
            ++*(int*)(this.value + CACHE_LINE_SIZE * JobUtils.ThreadIndex).ptr;
        }

        [INLINE(256)]
        public void ReadEnd() {
            E.IS_CREATED(this);
            // release read op
            --*(int*)(this.value + CACHE_LINE_SIZE * JobUtils.ThreadIndex).ptr;
        }

        [INLINE(256)]
        public void WriteBegin() {
            E.IS_CREATED(this);
            // acquire write op
            #if EXCEPTIONS_INTERNAL
            var i = 100_000_000;
            #endif
            E.ADDR_4(ref this.writeValue);
            while (System.Threading.Interlocked.CompareExchange(ref this.writeValue, 1, 0) != 0) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
            // wait if we have read op running
            #if EXCEPTIONS_INTERNAL
            i = 100_000_000;
            #endif
            while (this.ReadCount() > 0) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }

        [INLINE(256)]
        public void WriteEnd() {
            E.IS_CREATED(this);
            // release write op
            #if EXCEPTIONS_INTERNAL
            var i = 100_000_000;
            #endif
            E.ADDR_4(ref this.writeValue);
            while (System.Threading.Interlocked.CompareExchange(ref this.writeValue, 0, 1) != 1) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }

        [INLINE(256)]
        public void Dispose() {
            _free(this.value, this.allocator);
        }

    }
    
    [BURST(CompileSynchronously = true)]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ReadWriteSpinner : IIsCreated {

        private static readonly uint CACHE_LINE_SIZE = _align(TSize<int>.size, JobUtils.CacheLineSize);
        private MemPtr value;
        private int readValue;
        private int writeValue;
        #if USE_CACHE_PTR
        private int* ptr;
        #endif

        public bool IsCreated => this.value.IsValid();

        [INLINE(256)]
        public static ReadWriteSpinner Create(safe_ptr<State> state) {
            var size = CACHE_LINE_SIZE * JobUtils.ThreadsCount;
            var arr = state.ptr->allocator.Alloc(size, out var ptr);
            state.ptr->allocator.MemClear(arr, 0L, size);
            return new ReadWriteSpinner() {
                value = arr,
                #if USE_CACHE_PTR
                ptr = (int*)ptr,
                #endif
            };
        }

        [INLINE(256)]
        private int ReadCount(safe_ptr<State> state) {
            var cnt = 0;
            var ptr = state.ptr->allocator.GetUnsafePtr(this.value);
            for (uint i = 0u; i < JobUtils.ThreadsCount; ++i) {
                #if USE_CACHE_PTR
                cnt += this.ptr[i * CACHE_LINE_SIZE];
                #else
                cnt += *(int*)(ptr + i * CACHE_LINE_SIZE).ptr;
                #endif
            }
            return cnt;
        }
        
        [INLINE(256)]
        public void ReadBegin(safe_ptr<State> state) {
            E.IS_CREATED(this);
            // wait if we have to write op running
            #if EXCEPTIONS_INTERNAL
            var i = 100_000_000;
            #endif
            while (System.Threading.Volatile.Read(ref this.writeValue) == 1) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
            // acquire read op
            #if USE_CACHE_PTR
            ++this.ptr[CACHE_LINE_SIZE * JobUtils.ThreadIndex];
            #else
            ++*(int*)(state.ptr->allocator.GetUnsafePtr(this.value) + CACHE_LINE_SIZE * JobUtils.ThreadIndex).ptr;
            #endif
        }

        [INLINE(256)]
        public void ReadEnd(safe_ptr<State> state) {
            E.IS_CREATED(this);
            // release read op
            #if USE_CACHE_PTR
            --this.ptr[CACHE_LINE_SIZE * JobUtils.ThreadIndex];
            #else
            --*(int*)(state.ptr->allocator.GetUnsafePtr(this.value) + CACHE_LINE_SIZE * JobUtils.ThreadIndex).ptr;
            #endif
        }

        [INLINE(256)]
        public void WriteBegin(safe_ptr<State> state) {
            E.IS_CREATED(this);
            // acquire write op
            #if EXCEPTIONS_INTERNAL
            var i = 100_000_000;
            #endif
            E.ADDR_4(ref this.writeValue);
            while (System.Threading.Interlocked.CompareExchange(ref this.writeValue, 1, 0) != 0) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
            // wait if we have read op running
            #if EXCEPTIONS_INTERNAL
            i = 100_000_000;
            #endif
            while (this.ReadCount(state) > 0) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }

        [INLINE(256)]
        public void WriteEnd() {
            E.IS_CREATED(this);
            // release write op
            #if EXCEPTIONS_INTERNAL
            var i = 100_000_000;
            #endif
            E.ADDR_4(ref this.writeValue);
            while (System.Threading.Interlocked.CompareExchange(ref this.writeValue, 0, 1) != 1) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool value) {
            #if USE_CACHE_PTR
            this.ptr = (int*)allocator.GetUnsafePtr(this.value);
            #endif
        }

    }
    
    [BURST(CompileSynchronously = true)]
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

    [BURST(CompileSynchronously = true)]
    public struct LockSpinner {

        public const int SIZE = sizeof(int);

        private int value;
        public bool IsLocked => this.value != 0;

        [INLINE(256)]
        public bool Lock() {
            #if EXCEPTIONS_INTERNAL
            var i = 100_000_000;
            #endif
            E.ADDR_4(ref this.value);
            while (0 != System.Threading.Interlocked.CompareExchange(ref this.value, 1, 0)) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max lock iter");
                    return false;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
            System.Threading.Interlocked.MemoryBarrier();
            return true;
        }
        
        [INLINE(256)]
        public bool Unlock() {
            #if EXCEPTIONS_INTERNAL
            var i = 100_000_000;
            #endif
            System.Threading.Interlocked.MemoryBarrier();
            E.ADDR_4(ref this.value);
            while (1 != System.Threading.Interlocked.CompareExchange(ref this.value, 0, 1)) {
                #if EXCEPTIONS_INTERNAL
                --i;
                if (i == 0) {
                    UnityEngine.Debug.LogError("Max unlock iter");
                    return false;
                }
                #endif
                Unity.Burst.Intrinsics.Common.Pause();
            }
            return true;
        }
        
        [INLINE(256)]
        public void LockWhile() {
            E.ADDR_4(ref this.value);
            while (0 != System.Threading.Interlocked.CompareExchange(ref this.value, 1, 0)) {
                Unity.Burst.Intrinsics.Common.Pause();
            }
            System.Threading.Interlocked.MemoryBarrier();
        }
        
        [INLINE(256)]
        public void UnlockWhile() {
            System.Threading.Interlocked.MemoryBarrier();
            E.ADDR_4(ref this.value);
            while (1 != System.Threading.Interlocked.CompareExchange(ref this.value, 0, 1)) {
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }
        
    }
    
}