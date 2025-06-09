
using ME.BECS.Jobs;
#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections;
    using Unity.Jobs;

    public enum ScheduleFlags {

        None = 0,
        Single = 1 << 0,
        Parallel = 1 << 1,
        
        IsReadonly = 1 << 4,

    }
    
    public interface IJobParallelForAspectsComponentsBase { }
    public interface IJobParallelForComponentsBase { }
    public interface IJobParallelForAspectsBase { }
    public interface IJobForAspectsComponentsBase { }
    public interface IJobForComponentsBase { }
    public interface IJobForAspectsBase { }

    public struct JobReflectionData<T> {
        internal static readonly Unity.Burst.SharedStatic<System.IntPtr> data = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobReflectionData<T>>();
    }

    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
    public struct JobReflectionUnsafeData<T> {
        internal static readonly Unity.Burst.SharedStatic<System.IntPtr> data = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobReflectionUnsafeData<T>>();
    }
    #endif

    public struct JobInjectDeltaTimeOffset<TJob> {

        public static readonly Unity.Burst.SharedStatic<int> data = Unity.Burst.SharedStatic<int>.GetOrCreate<JobInjectDeltaTimeOffset<TJob>>();

    }

    public struct JobInjectDeltaTime<TJob> {

        public static readonly Unity.Burst.SharedStatic<byte> data = Unity.Burst.SharedStatic<byte>.GetOrCreate<JobInjectDeltaTime<TJob>>();

    }

    public struct JobStaticInfoLoopCount<TJob> {
        
        public static readonly Unity.Burst.SharedStatic<uint> data = Unity.Burst.SharedStatic<uint>.GetOrCreate<JobStaticInfoLoopCount<TJob>>();

    }

    public struct JobStaticInfoInlineCount<TJob> {
        
        public static readonly Unity.Burst.SharedStatic<uint> data = Unity.Burst.SharedStatic<uint>.GetOrCreate<JobStaticInfoInlineCount<TJob>>();

    }

    public unsafe struct JobStaticInfo<TJob> where TJob : struct {

        public static ref uint loopCount => ref JobStaticInfoLoopCount<TJob>.data.Data;
        public static ref uint inlineCount => ref JobStaticInfoInlineCount<TJob>.data.Data;
        public static bool IsParallelSupport => loopCount == 0u;
        
        [INLINE(256)]
        public static JobHandle SchedulePatch(ref JobInfo jobInfo, CommandBuffer* buffer, ScheduleMode scheduleMode, JobHandle dependsOn) {

            if (scheduleMode == ScheduleMode.Parallel) {
                if (IsParallelSupport == false) {
                    E.THROW_ENT_NEW();
                } else if (inlineCount > 0u) {
                    jobInfo.itemsPerCall = inlineCount;
                    if (inlineCount > 1u) {
                        // if we have more than 1 entity to create per iteration
                        // we need to be sure that we have free entities to supply
                        dependsOn = new StartParallelJob() {
                            buffer = buffer,
                            jobInfo = jobInfo,
                        }.ScheduleSingle(dependsOn);
                    }
                }
            }

            return dependsOn;

        }

    }
    
    public unsafe struct JobInject<TJob> where TJob : struct {
        
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> data = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<JobInject<TJob>>();

        public static void Init() {
            if (data.Data.IsCreated == false) data.Data = new UnsafeHashMap<int, System.IntPtr>(10, Allocator.Domain);
            data.Data.Clear();
        }
        
        [INLINE(256)]
        public static void Register(int offset, System.IntPtr systemPtr) {
            data.Data.Add(offset, systemPtr);
        }

        [INLINE(256)]
        public static void RegisterDeltaTime(int offset, byte fieldType) {
            JobInjectDeltaTimeOffset<TJob>.data.Data = offset;
            JobInjectDeltaTime<TJob>.data.Data = fieldType;
        }

        [INLINE(256)]
        public static void Patch(ref TJob instance, ushort worldId) {
            if (JobInjectDeltaTime<TJob>.data.Data > 0) {
                var addr = (byte*)_addressPtr(ref instance) + JobInjectDeltaTimeOffset<TJob>.data.Data;
                var dtMs = Worlds.GetWorldDeltaTime(worldId);
                var systemContext = SystemContext.Create(dtMs, default, default);
                if (JobInjectDeltaTime<TJob>.data.Data == 1) {
                    *((tfloat*)addr) = systemContext.deltaTime;
                } else if (JobInjectDeltaTime<TJob>.data.Data == 2) {
                    *((uint*)addr) = systemContext.deltaTimeMs;
                }
            }
            if (data.Data.IsCreated == false || data.Data.Count == 0) return;
            foreach (var kv in data.Data) {
                var addr = (byte*)_addressPtr(ref instance) + kv.Key;
                *((System.IntPtr*)addr) = kv.Value;
            }
        }
        
    }
    
    public unsafe struct JobInfo : IIsCreated {

        public uint count;
        public volatile uint index;
        public volatile uint itemsPerCall;
        public safe_ptr<uint> localOffset;
        public ushort worldId;

        public bool IsCreated => this.worldId > 0;

        public uint Offset => this.index * this.itemsPerCall + *this.localOffset.ptr;

        [INLINE(256)]
        public void CreateLocalCounter() {
            this.localOffset = _makeDefault<uint>(0u, Constants.ALLOCATOR_TEMP);
        }
        
        [INLINE(256)]
        public void ResetLocalCounter() {
            *this.localOffset.ptr = 0u;
        }

        [INLINE(256)]
        public readonly void IncrementLocalCounter() {
            ++*this.localOffset.ptr;
        }

        [INLINE(256)]
        public static JobInfo Create(ushort worldId) {
            return new JobInfo() {
                itemsPerCall = 1u,
                worldId = worldId,
            };
        }

        [INLINE(256)]
        public static JobInfo Create(in SystemContext context) {
            return context.jobInfo;
        }

        [INLINE(256)]
        public static implicit operator JobInfo(in SystemContext context) {
            return context.jobInfo;
        }

    }
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct DisposeJob : IJob {
        public MemPtr ptr;
        public ushort worldId;
        public void Execute() => Worlds.GetWorld(this.worldId).state.ptr->allocator.Free(this.ptr);
    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct DisposeAutoJob : IJob {
        public MemPtr ptr;
        public Ent ent;
        public ushort worldId;

        public void Execute() {
            var state = Worlds.GetWorld(this.worldId).state;
            CollectionsRegistry.Remove(state, in this.ent, in this.ptr);
            state.ptr->allocator.Free(this.ptr);
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct DisposePtrJob : IJob {
        [NativeDisableUnsafePtrRestriction]
        public safe_ptr ptr;
        public void Execute() => _free(ref this.ptr);
    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct DisposeWithAllocatorPtrJob : IJob {

        public AllocatorManager.AllocatorHandle allocator;
        [NativeDisableUnsafePtrRestriction]
        public safe_ptr ptr;
        public void Execute() => _free(this.ptr, this.allocator.ToAllocator);

    }

    public struct DisposeHandleJob : IJob {
        public GCHandle gcHandle;
        public void Execute() {
            if (this.gcHandle.IsAllocated == true) this.gcHandle.Free();
        }
    }

    public struct JobSingleThread {

        public static readonly Unity.Burst.SharedStatic<Internal.ArrayCacheLine<byte>> singleThreadsBurst = Unity.Burst.SharedStatic<Internal.ArrayCacheLine<byte>>.GetOrCreate<JobSingleThread>();
        public static ref Internal.ArrayCacheLine<byte> singleThreads => ref singleThreadsBurst.Data;
        
    }

    public static unsafe class JobUtils {

        public const uint CacheLineSize = JobsUtility.CacheLineSize;
        public static uint ThreadsCount => (uint)JobsUtility.ThreadIndexCount;
        public static uint ThreadIndex => (uint)JobsUtility.ThreadIndex;

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
            E.ADDR_4(ref target);
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
            E.ADDR_4(ref target);
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
            E.ADDR_4(ref target);
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
            E.ADDR_4(ref target);
            float snapshot;
            bool stillMore;
            do {
                snapshot = target;
                stillMore = newValue > snapshot;
            } while (stillMore && System.Threading.Interlocked.CompareExchange(ref target, newValue, snapshot) != snapshot);

            return stillMore;
        }

        [INLINE(256)]
        public static bool SetIfGreater(ref sfloat target, sfloat newValue) {
            E.ADDR_4(ref target);
            sfloat snapshot;
            bool stillMore;
            do {
                snapshot = target;
                stillMore = newValue > snapshot;
            } while (stillMore && (sfloat)System.Threading.Interlocked.CompareExchange(ref _as<sfloat, float>(ref target), (float)newValue, (float)snapshot) != snapshot);

            return stillMore;
        }

        [INLINE(256)]
        public static bool SetIfGreaterOrEquals(ref int target, int newValue) {
            E.ADDR_4(ref target);
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
            E.ADDR_4(ref value);
            unchecked {
                return (uint)System.Threading.Interlocked.Increment(ref _as<uint, int>(ref value));
            }
        }

        [INLINE(256)]
        public static int Increment(ref int value) {
            E.ADDR_4(ref value);
            return System.Threading.Interlocked.Increment(ref value);
        }

        [INLINE(256)]
        public static uint Decrement(ref uint value) {
            E.ADDR_4(ref value);
            unchecked {
                return (uint)System.Threading.Interlocked.Decrement(ref _as<uint, int>(ref value));
            }
        }

        [INLINE(256)]
        public static void Increment(ref float value, float count) {
            E.ADDR_4(ref value);
            float initialValue;
            float computedValue;
            do {
                initialValue = value;
                computedValue = initialValue + count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref value, computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Increment(ref sfloat value, sfloat count) {
            E.ADDR_4(ref value);
            sfloat initialValue;
            sfloat computedValue;
            do {
                initialValue = value;
                computedValue = initialValue + count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<sfloat, float>(ref value), (float)computedValue, (float)initialValue));
        }

        [INLINE(256)]
        public static void Increment(ref int value, int count) {
            E.ADDR_4(ref value);
            int initialValue;
            int computedValue;
            do {
                initialValue = value;
                computedValue = initialValue + count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref value, computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Increment(ref uint value, uint count) {
            E.ADDR_4(ref value);
            int initialValue;
            int computedValue;
            do {
                initialValue = (int)value;
                computedValue = initialValue + (int)count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<uint, int>(ref value), computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Decrement(ref int value, int count) {
            E.ADDR_4(ref value);
            int initialValue;
            int computedValue;
            do {
                initialValue = value;
                computedValue = initialValue - count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref value, computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Decrement(ref float value, float count) {
            E.ADDR_4(ref value);
            float initialValue;
            float computedValue;
            do {
                initialValue = value;
                computedValue = initialValue - count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref value, computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Decrement(ref uint value, uint count) {
            E.ADDR_4(ref value);
            int initialValue;
            int computedValue;
            do {
                initialValue = (int)value;
                computedValue = initialValue - (int)count;
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<uint, int>(ref value), computedValue, initialValue));
        }

        [INLINE(256)]
        public static void Decrement(ref uint value, int count) {
            E.ADDR_4(ref value);
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