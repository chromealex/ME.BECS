namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Jobs;

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

    public static unsafe class JobUtils {

        internal sealed class Destructor {
            ~Destructor() {
                JobUtils.CleanUp();
            }
        }

        internal static readonly Destructor staticDestructor = new Destructor();

        [INLINE(256)]
        public static void Initialize() {
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
            return (uint)System.Threading.Interlocked.Increment(ref UnsafeUtility.As<uint, int>(ref value));
        }

        [INLINE(256)]
        public static int Increment(ref int value) {
            return System.Threading.Interlocked.Increment(ref value);
        }

        [INLINE(256)]
        public static uint Decrement(ref uint value) {
            return (uint)System.Threading.Interlocked.Decrement(ref UnsafeUtility.As<uint, int>(ref value));
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
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref UnsafeUtility.As<uint, int>(ref value), computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Decrement(ref uint value, int count) {
            int initialValue;
            int computedValue;
            do {
                initialValue = (int)value;
                computedValue = initialValue - count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref UnsafeUtility.As<uint, int>(ref value), computedValue, initialValue));
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
        public static void Lock(ref int lockIndex) {
            var iters = 100_000_000;
            //var spin = new System.Threading.SpinWait();
            for (;;) {
                if (--iters == 0) {
                    // Debug iterations case
                    UnityEngine.Debug.LogError("Max iterations lock break. This should never happens.");
                    break;
                }

                // Block thread while another thread is working on tasks
                // Wait while previous thread complete all tasks
                if (System.Threading.Interlocked.Exchange(ref lockIndex, 1) == 0) {
                    break;
                }
                //spin.SpinOnce();
            }
            //UnityEngine.Debug.Log("LOCK");
        }

        [INLINE(256)]
        public static void LockThrow(ref int lockIndex) {
            for (;;) {
                // Block thread while another thread is working on tasks
                // Wait while previous thread complete all tasks
                if (System.Threading.Interlocked.Exchange(ref lockIndex, 1) == 0) {
                    break;
                }

                throw new System.Exception();
            }
        }

        [INLINE(256)]
        public static void Unlock(ref int lockIndex) {
            //UnityEngine.Debug.Log("UNLOCK");
            System.Threading.Interlocked.Exchange(ref lockIndex, 0);
        }

    }

}