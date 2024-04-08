namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Jobs;

    public interface IJobParallelForComponentsBase { }
    public interface IJobParallelForAspectBase { }
    public interface IJobComponentsBase { }
    public interface IJobAspectBase { }

    [BURST(CompileSynchronously = true)]
    public unsafe struct DisposeJob : Unity.Jobs.IJob {
        public MemPtr ptr;
        public ushort worldId;
        public void Execute() => Worlds.GetWorld(this.worldId).state->allocator.Free(this.ptr);
    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct DisposeAutoJob : Unity.Jobs.IJob {
        public MemPtr ptr;
        public Ent ent;
        public ushort worldId;

        public void Execute() {
            var state = Worlds.GetWorld(this.worldId).state;
            state->collectionsRegistry.Remove(state, in this.ent, in this.ptr);
            state->allocator.Free(this.ptr);
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct DisposePtrJob : Unity.Jobs.IJob {
        [NativeDisableUnsafePtrRestriction]
        public void* ptr;
        public void Execute() => _free(ref this.ptr);
    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct DisposeWithAllocatorPtrJob : Unity.Jobs.IJob {

        public Unity.Collections.AllocatorManager.AllocatorHandle allocator;
        [NativeDisableUnsafePtrRestriction]
        public void* ptr;
        public void Execute() => Unity.Collections.AllocatorManager.Free(this.allocator, this.ptr);

    }

    public struct DisposeHandleJob : Unity.Jobs.IJob {
        public GCHandle gcHandle;
        public void Execute() {
            if (this.gcHandle.IsAllocated == true) this.gcHandle.Free();
        }
    }

    public struct JobSingleThread {

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.ArrayCacheLine<byte>> singleThreadsBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.ArrayCacheLine<byte>>.GetOrCreate<JobSingleThread>();
        public static ref ME.BECS.Internal.ArrayCacheLine<byte> singleThreads => ref singleThreadsBurst.Data;
        
    }

    public static unsafe class JobUtils {

        public const uint CacheLineSize = JobsUtility.CacheLineSize;
        public static readonly uint ThreadsCount = (uint)Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndexCount;

        public static void Initialize() {
            CleanUp();
            JobSingleThread.singleThreads.Initialize();
        }

        [INLINE(256)]
        internal static void CleanUp() {
            JobSingleThread.singleThreads.Dispose();
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

            JobSingleThread.singleThreads.Get(JobsUtility.ThreadIndex) = (byte)(state == true ? 1 : 0);

        }
        
        [INLINE(256)]
        public static bool IsInParallelJob() {

            return JobsUtility.IsExecutingJob == true && JobSingleThread.singleThreads.Get(JobsUtility.ThreadIndex) == 0;

        }

        [INLINE(256)]
        public static void RunScheduled() {
            
            JobHandle.ScheduleBatchedJobs();
            
        }

        [INLINE(256)]
        public static bool SetIfSmaller(ref int target, int newValue) {
            int snapshot;
            bool stillLess;
            do {
                snapshot = target;
                stillLess = newValue < snapshot;
            } while (stillLess && System.Threading.Interlocked.CompareExchange(ref target, newValue, snapshot) != snapshot);

            return stillLess;
        }

        [INLINE(256)]
        public static bool SetIfGreater(ref int target, int newValue) {
            int snapshot;
            bool stillMore;
            do {
                snapshot = target;
                stillMore = newValue > snapshot;
            } while (stillMore && System.Threading.Interlocked.CompareExchange(ref target, newValue, snapshot) != snapshot);

            return stillMore;
        }

        [INLINE(256)]
        public static bool SetIfGreater(ref uint target, uint newValue) {
            int snapshot;
            bool stillMore;
            do {
                snapshot = (int)target;
                stillMore = newValue > snapshot;
            } while (stillMore && System.Threading.Interlocked.CompareExchange(ref _as<uint, int>(ref target), (int)newValue, snapshot) != snapshot);

            return stillMore;
        }

        [INLINE(256)]
        public static bool SetIfGreater(ref float target, float newValue) {
            float snapshot;
            bool stillMore;
            do {
                snapshot = target;
                stillMore = newValue > snapshot;
            } while (stillMore && System.Threading.Interlocked.CompareExchange(ref target, newValue, snapshot) != snapshot);

            return stillMore;
        }

        [INLINE(256)]
        public static bool SetIfGreaterOrEquals(ref int target, int newValue) {
            int snapshot;
            bool stillMore;
            do {
                snapshot = target;
                stillMore = newValue >= snapshot;
            } while (stillMore && System.Threading.Interlocked.CompareExchange(ref target, newValue, snapshot) != snapshot);

            return stillMore;
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
        public static void Increment(ref float value, float count) {
            float initialValue;
            float computedValue;
            do {
                initialValue = value;
                computedValue = initialValue + count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref value, computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Increment(ref int value, int count) {
            int initialValue;
            int computedValue;
            do {
                initialValue = value;
                computedValue = initialValue + count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref value, computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Increment(ref uint value, uint count) {
            int initialValue;
            int computedValue;
            do {
                initialValue = (int)value;
                computedValue = initialValue + (int)count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<uint, int>(ref value), computedValue, initialValue));
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
        public static void Decrement(ref float value, float count) {
            float initialValue;
            float computedValue;
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

    }

}