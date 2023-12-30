namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Jobs;

    
    public interface IJobParallelForComponentsBase { }
    public interface IJobParallelForAspectBase { }
    public interface IJobComponentsBase { }
    public interface IJobAspectBase { }

    public unsafe struct DisposeJob : Unity.Jobs.IJob {
        public MemPtr ptr;
        public ushort worldId;
        public void Execute() => Worlds.GetWorld(this.worldId).state->allocator.Free(this.ptr);
    }

    public unsafe struct DisposePtrJob : Unity.Jobs.IJob {
        [NativeDisableUnsafePtrRestriction]
        public void* ptr;
        public void Execute() => _free(ref this.ptr);
    }

    public struct DisposeHandleJob : Unity.Jobs.IJob {
        public GCHandle gcHandle;
        public void Execute() {
            if (this.gcHandle.IsAllocated == true) this.gcHandle.Free();
        }
    }

    public struct JobUtilsArray {

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<byte>> singleThreadsBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<byte>>.GetOrCreate<JobUtilsArray>();
        public static ref ME.BECS.Internal.Array<byte> singleThreads => ref singleThreadsBurst.Data;
        
    }

    public struct ReadWriteSpinner {

        private int value;
        private int writeValue;

        [INLINE(256)]
        public void ReadBegin() {
            // wait if we have write op running
            while (System.Threading.Volatile.Read(ref this.writeValue) == 1) {
            }
            // acquire read op
            System.Threading.Interlocked.Increment(ref this.value);
        }

        [INLINE(256)]
        public void WhiteBegin() {
            // acquire write op
            while (System.Threading.Interlocked.CompareExchange(ref this.writeValue, 1, 0) != 0) {
            }
            // wait if we have read op running
            while (System.Threading.Volatile.Read(ref this.value) > 0) {
            }
        }

        [INLINE(256)]
        public void ReadEnd() {
            // release read op
            System.Threading.Interlocked.Decrement(ref this.value);
        }

        [INLINE(256)]
        public void WhiteEnd() {
            // release write op
            while (System.Threading.Interlocked.CompareExchange(ref this.writeValue, 0, 1) != 1) {
            }
        }

    }
    
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

    public struct LockSpinner {
        
        private int value;
        [INLINE(256)]
        public void Lock(bool threaded = false) {
            while (0 != System.Threading.Interlocked.CompareExchange(ref this.value, 1, 0)) {
            }
            System.Threading.Interlocked.MemoryBarrier();
        }
        
        [INLINE(256)]
        public void Unlock(bool threaded = false) {
            System.Threading.Interlocked.MemoryBarrier();
            while (1 != System.Threading.Interlocked.CompareExchange(ref this.value, 0, 1)) {
            }
        }
        
    }
    
    public static unsafe class JobUtils {
        
        public static void Initialize() {
            CleanUp();
            JobUtilsArray.singleThreads.Resize((uint)JobsUtility.ThreadIndexCount);
        }

        [INLINE(256)]
        internal static void CleanUp() {

            JobUtilsArray.singleThreads.Dispose();

        }

        [INLINE(256)]
        public static int GetScheduleBatchCount(int count) => (int)GetScheduleBatchCount((uint)count);

        [INLINE(256)]
        public static uint GetScheduleBatchCount(uint count) {

            const uint batch = 64u;

            var batchCount = count / batch;
            if (batchCount == 0u) batchCount = 1u;
            if (count <= 10u && batchCount == 1u) {

                return batchCount;

            } else if (batchCount == 1u) {

                batchCount = 2u;

            }

            return batchCount;

        }

        [INLINE(256)]
        public static void SetCurrentThreadAsSingle(bool state) {

            JobUtilsArray.singleThreads.Get(JobsUtility.ThreadIndex) = (byte)(state == true ? 1 : 0);

        }
        
        [INLINE(256)]
        public static bool IsInParallelJob() {

            return JobsUtility.IsExecutingJob == true && JobUtilsArray.singleThreads.Get(JobsUtility.ThreadIndex) == 0;

        }

        [INLINE(256)]
        public static void RunScheduled() {
            
            JobHandle.ScheduleBatchedJobs();
            
        }

        [INLINE(256)]
        public static uint Increment(ref uint value) {
            return (uint)System.Threading.Interlocked.Increment(ref _as<uint, int>(ref value));
        }

        [INLINE(256)]
        public static int Increment(ref int value) {
            return System.Threading.Interlocked.Increment(ref value);
        }

        [INLINE(256)]
        public static uint Decrement(ref uint value) {
            return (uint)System.Threading.Interlocked.Decrement(ref _as<uint, int>(ref value));
        }

        [INLINE(256)]
        public static void Decrement(ref int value, int count) {
            int initialValue;
            int computedValue;
            do {
                initialValue = value;
                computedValue = initialValue - count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref value, computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Decrement(ref uint value, uint count) {
            int initialValue;
            int computedValue;
            do {
                initialValue = (int)value;
                computedValue = initialValue - (int)count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<uint, int>(ref value), computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Decrement(ref uint value, int count) {
            int initialValue;
            int computedValue;
            do {
                initialValue = (int)value;
                computedValue = initialValue - count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<uint, int>(ref value), computedValue, initialValue));
        }

        [INLINE(256)]
        public static T* CompareExchange<T>(ref T* location, T* value, T* comparand) where T : unmanaged {

            var loc = (System.IntPtr)location;
            var res = (T*)System.Threading.Interlocked.CompareExchange(ref loc, (System.IntPtr)value, (System.IntPtr)comparand);
            location = (T*)loc;
            return res;

        }

        [INLINE(256)]
        public static System.IntPtr CompareExchange(ref System.IntPtr location, System.IntPtr value, System.IntPtr comparand) {

            return System.Threading.Interlocked.CompareExchange(ref location, value, comparand);

        }

        [INLINE(256)]
        public static void Lock(ref LockSpinner spinner) {
            spinner.Lock();
        }

        [INLINE(256)]
        public static void Unlock(ref LockSpinner spinner) {
            spinner.Unlock();
        }

        [INLINE(256)]
        public static void LockThread(in MemoryAllocator allocator, ref MemArray<LockSpinner> spinner) {
            spinner[in allocator, JobsUtility.ThreadIndex].Lock(true);
        }

        [INLINE(256)]
        public static void UnlockThread(in MemoryAllocator allocator, ref MemArray<LockSpinner> spinner) {
            spinner[in allocator, JobsUtility.ThreadIndex].Unlock(true);
        }

        [INLINE(256)]
        public static void LockThreads(in MemoryAllocator allocator, ref MemArray<LockSpinner> spinner) {
            for (uint i = 0; i < spinner.Length; ++i) {
                spinner[in allocator, i].Lock(true);
            }
        }

        [INLINE(256)]
        public static void UnlockThreads(in MemoryAllocator allocator, ref MemArray<LockSpinner> spinner) {
            for (uint i = 0; i < spinner.Length; ++i) {
                spinner[in allocator, i].Unlock(true);
            }
        }

        [INLINE(256)]
        public static MemArray<LockSpinner> InitializeThreadLock(ref MemoryAllocator allocator) {

            return new MemArray<LockSpinner>(ref allocator, (uint)JobsUtility.ThreadIndexCount);

        }

    }

}