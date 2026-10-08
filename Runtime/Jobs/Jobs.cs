using ME.BECS.Jobs;
#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS {
    
    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections;
    using Unity.Jobs;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Defines the supported schedule flags values.
    /// </summary>
    [System.Flags]
    public enum ScheduleFlags {

        /// <summary>
        /// None option for <c>ScheduleFlags</c>.
        /// </summary>
        None = 0,
        /// <summary>
        /// Single option for <c>ScheduleFlags</c>.
        /// </summary>
        Single = 1 << 0,
        /// <summary>
        /// Parallel option for <c>ScheduleFlags</c>.
        /// </summary>
        Parallel = 1 << 1,
        
        /// <summary>
        /// Is readonly option for <c>ScheduleFlags</c>.
        /// </summary>
        IsReadonly = 1 << 4,

    }
    
    /// <summary>
    /// Supplies ro metadata to annotated declarations.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public class ROAttribute : System.Attribute {}

    /// <summary>
    /// Supplies rw metadata to annotated declarations.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public class RWAttribute : System.Attribute {}

    /// <summary>
    /// Supplies wo metadata to annotated declarations.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public class WOAttribute : System.Attribute {}

    /// <summary>
    /// Defines the operations required by job parallel for aspects components base.
    /// </summary>
    public interface IJobParallelForAspectsComponentsBase { }
    /// <summary>
    /// Defines the operations required by job parallel for components base.
    /// </summary>
    public interface IJobParallelForComponentsBase { }
    /// <summary>
    /// Defines the operations required by job parallel for aspects base.
    /// </summary>
    public interface IJobParallelForAspectsBase { }
    /// <summary>
    /// Defines the operations required by job for aspects components base.
    /// </summary>
    public interface IJobForAspectsComponentsBase { }
    /// <summary>
    /// Defines the operations required by job for components base.
    /// </summary>
    public interface IJobForComponentsBase { }
    /// <summary>
    /// Defines the operations required by job for aspects base.
    /// </summary>
    public interface IJobForAspectsBase { }

    /// <summary>
    /// Caches job reflection data metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobReflectionData<T> {
        internal static readonly Unity.Burst.SharedStatic<System.IntPtr> data = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobReflectionData<T>>();
    }

    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
    /// <summary>
    /// Caches job reflection unsafe data metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobReflectionUnsafeData<T> {
        internal static readonly Unity.Burst.SharedStatic<System.IntPtr> data = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobReflectionUnsafeData<T>>();
    }
    #endif

    /// <summary>
    /// Stores the field offset used to inject a job's delta time.
    /// </summary>
    public struct JobInjectDeltaTimeOffset<TJob> {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<int> data = Unity.Burst.SharedStatic<int>.GetOrCreate<JobInjectDeltaTimeOffset<TJob>>();

    }

    /// <summary>
    /// Patches the delta-time value injected into a job.
    /// </summary>
    public struct JobInjectDeltaTime<TJob> {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<byte> data = Unity.Burst.SharedStatic<byte>.GetOrCreate<JobInjectDeltaTime<TJob>>();

    }

    /// <summary>
    /// Caches job static info loop count metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobStaticInfoLoopCount<TJob> {
        
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> data = Unity.Burst.SharedStatic<uint>.GetOrCreate<JobStaticInfoLoopCount<TJob>>();

    }

    /// <summary>
    /// Caches job static info entities max count metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobStaticInfoEntitiesMaxCount<TJob> {
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> data = Unity.Burst.SharedStatic<uint>.GetOrCreate<JobStaticInfoEntitiesMaxCount<TJob>>();
    }

    /// <summary>
    /// Caches job static info inline count metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobStaticInfoInlineCount<TJob> {
        
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<safe_ptr<uint>> data = Unity.Burst.SharedStatic<safe_ptr<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<JobStaticInfoInlineCount<TJob>>(TAlign<uint>.align, 9090);

    }

    /// <summary>
    /// Caches job static info inline capacity metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobStaticInfoInlineCapacity<TJob> {
        
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> data = Unity.Burst.SharedStatic<uint>.GetOrCreate<JobStaticInfoInlineCapacity<TJob>>();

    }

    /// <summary>
    /// Caches job static info weights metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobStaticInfoWeights<TJob> {
        
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> data = Unity.Burst.SharedStatic<uint>.GetOrCreate<JobStaticInfoWeights<TJob>>();

    }

    /// <summary>
    /// Caches job static info max struct size metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobStaticInfoMaxStructSize<TJob> {
        
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> data = Unity.Burst.SharedStatic<uint>.GetOrCreate<JobStaticInfoMaxStructSize<TJob>>();

    }

    /// <summary>
    /// Caches job static info last count metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobStaticInfoLastCount<TJob> {
        
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> data = Unity.Burst.SharedStatic<uint>.GetOrCreate<JobStaticInfoLastCount<TJob>>();

    }

    /// <summary>
    /// Caches job static info handle metadata used when preparing and scheduling jobs.
    /// </summary>
    public struct JobStaticInfoHandle {
        
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<JobHandle> data = Unity.Burst.SharedStatic<JobHandle>.GetOrCreate<JobStaticInfoHandle>();

    }

    /// <summary>
    /// Caches job static info metadata used when preparing and scheduling jobs.
    /// </summary>
    public unsafe struct JobStaticInfo<TJob> where TJob : struct {

        /// <summary>
        /// Last count for the associated storage.
        /// </summary>
        public static ref uint lastCount => ref JobStaticInfoLastCount<TJob>.data.Data;
        /// <summary>
        /// Loop count for the associated storage.
        /// </summary>
        public static ref uint loopCount => ref JobStaticInfoLoopCount<TJob>.data.Data;
        /// <summary>
        /// Entities max count for the associated storage.
        /// </summary>
        public static ref uint entitiesMaxCount => ref JobStaticInfoEntitiesMaxCount<TJob>.data.Data;
        /// <summary>
        /// Inline count for the associated storage.
        /// </summary>
        public static ref safe_ptr<uint> inlineCount => ref JobStaticInfoInlineCount<TJob>.data.Data;

        // Called by generated job initializers on every bootstrap load. Domain memory is
        // only released on domain unload and Unity tracks each block in a fixed-size table
        // (DomainUnloadAutoFree, 262144 entries): allocating per load overflows it and
        // crashes the Editor after enough reloads (e.g. a test run calling LoadInstalled
        // in every SetUp). Reuse the block for this job instead.
        /// <summary>
        /// Allocates inline count.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<uint> AllocateInlineCount(uint groupCount) {
            ref var capacity = ref JobStaticInfoInlineCapacity<TJob>.data.Data;
            var current = inlineCount;
            if (current.ptr != null && capacity >= groupCount) {
                if (groupCount > 0u) Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemClear(current.ptr, TSize<uint>.size * capacity);
                return current;
            }
            capacity = groupCount;
            return _makeArray<uint>(groupCount, Unity.Collections.Allocator.Domain);
        }
        /// <summary>
        /// Ops weight controlling the associated calculation.
        /// </summary>
        public static ref uint opsWeight => ref JobStaticInfoWeights<TJob>.data.Data;
        /// <summary>
        /// Maximum struct size.
        /// </summary>
        public static ref uint maxStructSize => ref JobStaticInfoMaxStructSize<TJob>.data.Data;
        /// <summary>
        /// Indicates is parallel support.
        /// </summary>
        public static bool IsParallelSupport => loopCount == 0u || entitiesMaxCount > 0u;
        
        /// <summary>
        /// Schedules patch.
        /// </summary>
        [INLINE(256)]
        public static JobHandle SchedulePatch(ref JobInfo jobInfo, CommandBuffer* buffer, ScheduleMode scheduleMode, JobHandle dependsOn) {
            jobInfo.entitiesMaxCount = entitiesMaxCount;

            if (scheduleMode == ScheduleMode.Parallel) {
                if (IsParallelSupport == false) {
                    E.THROW_ENT_NEW();
                } else if (inlineCount.ptr != null) {
                    // if we have more than 1 entity to create per iteration
                    // we need to be sure that we have free entities to supply
                    jobInfo.Initialize(inlineCount);
                    dependsOn = new StartParallelJob(buffer, inlineCount, in jobInfo).ScheduleSingle(JobHandle.CombineDependencies(dependsOn, JobStaticInfoHandle.data.Data));
                    JobStaticInfoHandle.data.Data = dependsOn;
                }
            }

            return dependsOn;

        }

        /// <summary>
        /// Schedules result.
        /// </summary>
        [INLINE(256)]
        public static JobHandle ScheduleResult(JobHandle dependsOn, CommandBuffer* buffer, JobInfo jobInfo, ScheduleMode scheduleMode) {
            if (scheduleMode == ScheduleMode.Parallel) {
                if (IsParallelSupport == false) {
                    E.THROW_ENT_NEW();
                } else if (inlineCount.ptr != null) {
                    dependsOn = new FinishParallelJob(buffer, inlineCount, in jobInfo).ScheduleSingle(dependsOn);
                }
            }
            return dependsOn;
        }

    }

    /// <summary>
    /// Defines job patch inject delegate state and operations.
    /// </summary>
    public unsafe struct JobPatchInjectDelegate {
        
        /// <summary>
        /// Defines the callback signature for patch delegate.
        /// </summary>
        public delegate void PatchDelegate(void* job, ushort worldId);

    }
    
    /// <summary>
    /// Patches the world-specific dependencies of a job before execution.
    /// </summary>
    public unsafe struct JobInject<TJob> where TJob : struct {

        /// <summary>
        /// Indicates has delegate.
        /// </summary>
        public static bool hasDelegate;
        /// <summary>
        /// Non burst delegate used by <c>JobInject</c>.
        /// </summary>
        public static JobPatchInjectDelegate.PatchDelegate nonBurstDelegate;
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Unity.Burst.FunctionPointer<JobPatchInjectDelegate.PatchDelegate>> data = Unity.Burst.SharedStatic<Unity.Burst.FunctionPointer<JobPatchInjectDelegate.PatchDelegate>>.GetOrCreate<JobInject<TJob>>();

        /// <summary>
        /// Registers the supplied instance or type for subsequent lookup.
        /// </summary>
        [INLINE(256)]
        public static void Register(JobPatchInjectDelegate.PatchDelegate patchDelegate) {
            data.Data = Unity.Burst.BurstCompiler.CompileFunctionPointer(patchDelegate);
            nonBurstDelegate = patchDelegate;
            hasDelegate = true;
        }

        /// <summary>
        /// Applies the requested patch to the supplied runtime data.
        /// </summary>
        [INLINE(256)]
        public static void Patch(ref TJob instance, ushort worldId) {
            var callManaged = false;
            PatchManagedOnly(ref instance, worldId, ref callManaged);
            if (callManaged == false) PatchBurstOnly(ref instance, worldId);
        }

        /// <summary>
        /// Patches burst only.
        /// </summary>
        public static void PatchBurstOnly(ref TJob instance, ushort worldId) {
            if (data.Data.IsCreated == true) data.Data.Invoke(_addressPtr(ref instance), worldId);
        }

        /// <summary>
        /// Patches managed only.
        /// </summary>
        [Unity.Burst.BurstDiscardAttribute]
        public static void PatchManagedOnly(ref TJob instance, ushort worldId, ref bool call) {
            if (hasDelegate == true) nonBurstDelegate.Invoke(_addressPtr(ref instance), worldId);
            call = true;
        }

    }
    
    /// <summary>
    /// Identifies the world and execution context of an ECS job.
    /// </summary>
    [IgnoreProfiler]
    public unsafe struct JobInfo : IIsCreated {

        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count;
        /// <summary>
        /// Index of this entry within its containing storage.
        /// </summary>
        public volatile uint index;
        /// <summary>
        /// Items per call used by <c>JobInfo</c>.
        /// </summary>
        public safe_ptr<uint> itemsPerCall;
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public safe_ptr<safe_ptr<Ent>> results;
        /// <summary>
        /// Local offsets used by <c>JobInfo</c>.
        /// </summary>
        public safe_ptr<uint> localOffsets;
        /// <summary>
        /// Entities max count for the associated storage.
        /// </summary>
        public uint entitiesMaxCount;
        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        public ushort worldId;

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.worldId > 0;

        /// <summary>
        /// Returns offset.
        /// </summary>
        [INLINE(256)]
        public readonly uint GetOffset(uint groupId) {
            if (this.itemsPerCall.ptr == null) return 0u;
            var itemsPerCall = this.itemsPerCall[groupId];
            var localOffset = this.localOffsets[groupId];
            if (localOffset >= itemsPerCall) {
                if (this.entitiesMaxCount > 0u) E.JOB_ENTITIES_MAX_COUNT();
                throw new System.InvalidOperationException("Entity creation exceeds the analyzed reservation for this group.");
            }
            E.RANGE(localOffset, 0u, itemsPerCall);
            return this.index * itemsPerCall + localOffset;
        }

        /// <summary>
        /// Initializes job info state from the supplied context.
        /// </summary>
        [INLINE(256)]
        public void Initialize(safe_ptr<uint> inlineCount) {
            this.itemsPerCall = inlineCount;
            this.results = _makeArray<safe_ptr<Ent>>(EntityTypes.groupsCount, WorldsTempAllocator.allocatorTemp.Get(this.worldId).Allocator.ToAllocator);
        }

        /// <summary>
        /// Prepares reusable instances or storage before normal execution.
        /// </summary>
        [INLINE(256)]
        public void Prewarm(CommandBuffer* buffer, safe_ptr<uint> inlineCount) {
            // Check before allocating entities or entering the prewarm state.
            for (uint group = 0; group < EntityTypes.groupsCount; ++group)
                if ((ulong)inlineCount[group] * buffer->count > (uint)(int.MaxValue / sizeof(Ent)))
                    throw new System.InvalidOperationException("Entity reservation is too large. Reduce EntitiesJobMaxCount or the query size.");
            var maxId = 0u;
            Ents.PrewarmBegin(buffer->state);
            var allocator = WorldsTempAllocator.allocatorTemp.Get(this.worldId).Allocator.ToAllocator;
            for (ushort groupId = 0; groupId < EntityTypes.groupsCount; ++groupId) {
                var count = inlineCount[groupId];
                if (count == 0u) continue;
                var cnt = count * buffer->count;
                this.results[groupId] = _makeArray<Ent>(cnt, allocator, false);
                for (uint i = 0u; i < cnt; ++i) {
                    var ent = Ent.NewWithGroup_INTERNAL(this.worldId, groupId, default);
                    if (ent.id > maxId) maxId = ent.id;
                    this.results[groupId][i] = ent;
                }
            }
            Ents.PrewarmEnd(buffer->state);
            Ent.Resize(this.worldId, buffer->state, maxId + 1u);
        }

        /// <summary>
        /// Returns entity.
        /// </summary>
        [INLINE(256)]
        public readonly Ent GetEntity(ushort groupId) {
            this.CheckEntityLimit();
            ref var ent = ref this.results[groupId][this.GetOffset(groupId)];
            var newEnt = ent;
            ent = default;
            this.IncrementLocalCounter(groupId);
            return newEnt;
        }

        /// <summary>
        /// Releases the resources owned by this job info instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(CommandBuffer* buffer, safe_ptr<uint> inlineCount) {
            // return all unused entities
            for (ushort groupId = 0; groupId < EntityTypes.groupsCount; ++groupId) {
                var count = inlineCount[groupId];
                if (count == 0u) continue;
                var cnt = count * buffer->count;
                for (uint i = 0u; i < cnt; ++i) {
                    var ent = this.results[groupId][i];
                    if (ent == default) continue;
                    Ents.Remove(buffer->state, in ent);
                }
            }
        }

        /// <summary>
        /// Creates local counter.
        /// </summary>
        [INLINE(256)]
        public void CreateLocalCounter() {
            var length = (this.itemsPerCall.ptr != null ? EntityTypes.groupsCount : 0u) + (this.entitiesMaxCount > 0u ? 1u : 0u);
            if (length == 0u) return;
            this.localOffsets = _makeArray<uint>(length, Constants.ALLOCATOR_TEMP);
        }
        
        /// <summary>
        /// Resets local counter.
        /// </summary>
        [INLINE(256)]
        public void ResetLocalCounter() {
            var length = (this.itemsPerCall.ptr != null ? EntityTypes.groupsCount : 0u) + (this.entitiesMaxCount > 0u ? 1u : 0u);
            if (length == 0u) return;
            _memclear(this.localOffsets, length * sizeof(uint));
        }

        /// <summary>
        /// Checks entity limit.
        /// </summary>
        [INLINE(256)]
        public readonly void CheckEntityLimit() {
            if (this.entitiesMaxCount == 0u) return;
            if (this.localOffsets.ptr == null) { E.JOB_ENTITIES_MAX_COUNT(); return; }
            ref var created = ref this.localOffsets[this.itemsPerCall.ptr != null ? EntityTypes.groupsCount : 0u];
            if (created >= this.entitiesMaxCount) { E.JOB_ENTITIES_MAX_COUNT(); return; }
            ++created;
        }

        /// <summary>
        /// Increments local counter.
        /// </summary>
        [INLINE(256)]
        public readonly void IncrementLocalCounter(uint groupId) {
            ++this.localOffsets[groupId];
        }

        /// <summary>
        /// Creates <c>JobInfo</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static JobInfo Create(ushort worldId) {
            return new JobInfo() {
                itemsPerCall = default,
                worldId = worldId,
            };
        }

        /// <summary>
        /// Creates <c>JobInfo</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static JobInfo Create(in SystemContext context) {
            return context.jobInfo;
        }

        /// <summary>
        /// Converts the supplied value to <c>JobInfo</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator JobInfo(in SystemContext context) {
            return context.jobInfo;
        }

    }
    
    /// <summary>
    /// Executes dispose work through the job scheduler.
    /// </summary>
    [BURST]
    public unsafe partial struct DisposeJob : IJob {
        /// <summary>
        /// Native address of the associated storage; ownership is defined by the containing API.
        /// </summary>
        public MemPtr ptr;
        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        public ushort worldId;
        /// <summary>
        /// Processes dispose using the supplied job inputs.
        /// </summary>
        public void Execute() => Worlds.GetWorld(this.worldId).state.ptr->allocator.Free(this.ptr);
    }

    /// <summary>
    /// Executes dispose auto work through the job scheduler.
    /// </summary>
    [BURST]
    public unsafe partial struct DisposeAutoJob : IJob {
        /// <summary>
        /// Native address of the associated storage; ownership is defined by the containing API.
        /// </summary>
        public MemPtr ptr;
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent;
        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        public ushort worldId;

        /// <summary>
        /// Processes dispose auto using the supplied job inputs.
        /// </summary>
        public void Execute() {
            var state = Worlds.GetWorld(this.worldId).state;
            CollectionsRegistry.Remove(state, in this.ent, in this.ptr);
            state.ptr->allocator.Free(this.ptr);
        }

    }

    /// <summary>
    /// Executes dispose ptr work through the job scheduler.
    /// </summary>
    [BURST]
    public unsafe partial struct DisposePtrJob : IJob {
        /// <summary>
        /// Native address of the associated storage; ownership is defined by the containing API.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public safe_ptr ptr;
        /// <summary>
        /// Processes dispose ptr using the supplied job inputs.
        /// </summary>
        public void Execute() => _free(ref this.ptr);
    }

    /// <summary>
    /// Executes dispose with allocator ptr work through the job scheduler.
    /// </summary>
    [BURST]
    public unsafe partial struct DisposeWithAllocatorPtrJob : IJob {

        /// <summary>
        /// Allocator used to access or manage the associated native storage.
        /// </summary>
        public AllocatorManager.AllocatorHandle allocator;
        /// <summary>
        /// Native address of the associated storage; ownership is defined by the containing API.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public safe_ptr ptr;
        /// <summary>
        /// Processes dispose with allocator ptr using the supplied job inputs.
        /// </summary>
        public void Execute() => _free(this.ptr, this.allocator.ToAllocator);

    }

    /// <summary>
    /// Executes dispose handle work through the job scheduler.
    /// </summary>
    public partial struct DisposeHandleJob : IJob {
        /// <summary>
        /// Gc handle used by <c>DisposeHandleJob</c>.
        /// </summary>
        public GCHandle gcHandle;
        /// <summary>
        /// Processes dispose handle using the supplied job inputs.
        /// </summary>
        public void Execute() {
            if (this.gcHandle.IsAllocated == true) this.gcHandle.Free();
        }
    }

    /// <summary>
    /// Defines job single thread state and operations.
    /// </summary>
    public struct JobSingleThread {

        /// <summary>
        /// Single threads burst used by <c>JobSingleThread</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Internal.ArrayCacheLine<byte>> singleThreadsBurst = Unity.Burst.SharedStatic<Internal.ArrayCacheLine<byte>>.GetOrCreate<JobSingleThread>();
        /// <summary>
        /// Single threads used by <c>JobSingleThread</c>.
        /// </summary>
        public static ref Internal.ArrayCacheLine<byte> singleThreads => ref singleThreadsBurst.Data;
        
    }

    /// <summary>
    /// Provides helper operations for job.
    /// </summary>
    [IgnoreProfiler]
    public static unsafe class JobUtils {

        /// <summary>
        /// Cache line size used by <c>JobUtils</c>.
        /// </summary>
        public const uint CacheLineSize = JobsUtility.CacheLineSize;
        /// <summary>
        /// Threads count for the associated storage.
        /// </summary>
        public static uint ThreadsCount => (uint)JobsUtility.ThreadIndexCount;
        /// <summary>
        /// Threads count max used by <c>JobUtils</c>.
        /// </summary>
        public static uint ThreadsCountMax => 128u;
        /// <summary>
        /// Cache line size fixed used by <c>JobUtils</c>.
        /// </summary>
        public const uint CacheLineSizeFixed = 64u;
        /// <summary>
        /// Thread index used to locate the associated entry.
        /// </summary>
        public static uint ThreadIndex => (uint)JobsUtility.ThreadIndex;

        /// <summary>
        /// Initializes job utils state from the supplied context.
        /// </summary>
        public static void Initialize() {
            CleanUp();
            JobSingleThread.singleThreads.Initialize();
        }

        [INLINE(256)]
        internal static void CleanUp() {
            JobSingleThread.singleThreads.Dispose();
        }

        /// <summary>
        /// Returns schedule batch count.
        /// </summary>
        [INLINE(256)]
        public static int GetScheduleBatchCount(int count) => (int)GetScheduleBatchCount<TNull>((uint)count);

        /// <summary>
        /// Returns schedule batch count.
        /// </summary>
        [INLINE(256)]
        public static uint GetScheduleBatchCount(uint count) => GetScheduleBatchCount<TNull>(count);

        /// <summary>
        /// Returns schedule batch count.
        /// </summary>
        [INLINE(256)]
        public static uint GetScheduleBatchCount<T>(uint count) where T : struct {

            if (count == 0u) count = JobStaticInfo<T>.lastCount;
            if (count == 0u) count = 1u;
            var maxStructSize = JobStaticInfo<T>.maxStructSize;
            if (maxStructSize == 0u) maxStructSize = 1u;
            
            var threadCount = JobUtils.ThreadsCount;
            var minItemsPerThread = count / threadCount;
            var itemsPerCacheLine = Unity.Mathematics.math.max(1u, JobUtils.CacheLineSize / maxStructSize);
            var minBatch = itemsPerCacheLine;
            var maxBatch = minItemsPerThread;
            var avgBatch = Unity.Mathematics.math.max(minBatch, (minBatch + maxBatch) / 2f);
            avgBatch = (uint)(avgBatch / itemsPerCacheLine) * itemsPerCacheLine;
            return Unity.Mathematics.math.max((uint)avgBatch, 1u);

        }

        /// <summary>
        /// Sets current thread as single.
        /// </summary>
        [INLINE(256)]
        public static void SetCurrentThreadAsSingle(bool state) {

            JobSingleThread.singleThreads.Get(JobsUtility.ThreadIndex) = (byte)(state == true ? 1 : 0);

        }
        
        /// <summary>
        /// Reports whether the current execution context is a parallel job.
        /// </summary>
        [INLINE(256)]
        public static bool IsInParallelJob() {

            return JobsUtility.IsExecutingJob == true && JobSingleThread.singleThreads.Get(JobsUtility.ThreadIndex) == 0;

        }

        /// <summary>
        /// Schedules the callback for execution in the supported job context.
        /// </summary>
        [INLINE(256)]
        public static void RunScheduled() {
            
            JobHandle.ScheduleBatchedJobs();
            
        }

        /// <summary>
        /// Sets if smaller.
        /// </summary>
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

        /// <summary>
        /// Sets if greater.
        /// </summary>
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

        /// <summary>
        /// Sets if greater.
        /// </summary>
        [INLINE(256)]
        public static bool SetIfGreater(ref uint target, uint newValue) {
            E.ADDR_4(ref target);
            uint snapshot;
            bool stillMore;
            do {
                snapshot = target;
                stillMore = newValue > snapshot;
            } while (stillMore && (uint)System.Threading.Interlocked.CompareExchange(ref _as<uint, int>(ref target), (int)newValue, (int)snapshot) != snapshot);

            return stillMore;
        }

        /// <summary>
        /// Sets if greater.
        /// </summary>
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

        /// <summary>
        /// Sets if greater.
        /// </summary>
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

        /// <summary>
        /// Sets if greater or equals.
        /// </summary>
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

        /// <summary>
        /// Increments the associated counter and returns the result when applicable.
        /// </summary>
        [INLINE(256)]
        public static uint Increment(ref uint value) {
            E.ADDR_4(ref value);
            unchecked {
                return (uint)System.Threading.Interlocked.Increment(ref _as<uint, int>(ref value));
            }
        }

        /// <summary>
        /// Increments the associated counter and returns the result when applicable.
        /// </summary>
        [INLINE(256)]
        public static int Increment(ref int value) {
            E.ADDR_4(ref value);
            return System.Threading.Interlocked.Increment(ref value);
        }

        /// <summary>
        /// Decrements the associated counter and returns the result when applicable.
        /// </summary>
        [INLINE(256)]
        public static uint Decrement(ref uint value) {
            E.ADDR_4(ref value);
            unchecked {
                return (uint)System.Threading.Interlocked.Decrement(ref _as<uint, int>(ref value));
            }
        }

        /// <summary>
        /// Decrements the associated counter and returns the result when applicable.
        /// </summary>
        [INLINE(256)]
        public static int Decrement(ref int value) {
            E.ADDR_4(ref value);
            unchecked {
                return System.Threading.Interlocked.Decrement(ref value);
            }
        }

        /// <summary>
        /// Increments the associated counter and returns the result when applicable.
        /// </summary>
        [INLINE(256)]
        public static void Increment(ref float value, float count) {
            E.ADDR_4(ref value);
            int initialValue;
            int computedValue;
            do {
                initialValue = _as<float, int>(ref value);
                var currentValue = _as<int, float>(ref initialValue);
                var newValue = currentValue + count;
                computedValue = _as<float, int>(ref newValue);
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<float, int>(ref value), computedValue, initialValue));
        }

        /// <summary>
        /// Increments the associated counter and returns the result when applicable.
        /// </summary>
        [INLINE(256)]
        public static void Increment(ref sfloat value, sfloat count) {
            E.ADDR_4(ref value);
            int initialValue;
            int computedValue;
            do {
                initialValue = _as<sfloat, int>(ref value);
                var currentValue = _as<int, sfloat>(ref initialValue);
                var newValue = currentValue + count;
                computedValue = _as<sfloat, int>(ref newValue);
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<sfloat, int>(ref value), computedValue, initialValue));
        }

        /// <summary>
        /// Increments the associated counter and returns the result when applicable.
        /// </summary>
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

        /// <summary>
        /// Increments the associated counter and returns the result when applicable.
        /// </summary>
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

        /// <summary>
        /// Decrements the associated counter and returns the result when applicable.
        /// </summary>
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

        /// <summary>
        /// Decrements the associated counter and returns the result when applicable.
        /// </summary>
        [INLINE(256)]
        public static void Decrement(ref float value, float count) {
            E.ADDR_4(ref value);
            int initialValue;
            int computedValue;
            do {
                initialValue = _as<float, int>(ref value);
                var currentValue = _as<int, float>(ref initialValue);
                var newValue = currentValue - count;
                computedValue = _as<float, int>(ref newValue);
            } while (initialValue != System.Threading.Interlocked.CompareExchange(ref _as<float, int>(ref value), computedValue, initialValue));
        }

        /// <summary>
        /// Decrements the associated counter and returns the result when applicable.
        /// </summary>
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

        /// <summary>
        /// Decrements the associated counter and returns the result when applicable.
        /// </summary>
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

        /// <summary>
        /// Compares exchange.
        /// </summary>
        [INLINE(256)]
        public static T* CompareExchange<T>(ref T* location, T* value, T* comparand) where T : unmanaged {
            fixed (T** locationPtr = &location) {
                return (T*)System.Threading.Interlocked.CompareExchange(ref *(System.IntPtr*)locationPtr, (System.IntPtr)value, (System.IntPtr)comparand);
            }

        }

        /// <summary>
        /// Compares exchange.
        /// </summary>
        [INLINE(256)]
        public static System.IntPtr CompareExchange(ref System.IntPtr location, System.IntPtr value, System.IntPtr comparand) {

            return System.Threading.Interlocked.CompareExchange(ref location, value, comparand);

        }

        /// <summary>
        /// Compares exchange.
        /// </summary>
        [INLINE(256)]
        public static ulong CompareExchange(ref ulong location, ulong value, ulong comparand) {

            return (ulong)System.Threading.Interlocked.CompareExchange(ref _as<ulong, long>(ref location), (long)value, (long)comparand);

        }

        /// <summary>
        /// Compares exchange.
        /// </summary>
        [INLINE(256)]
        public static uint CompareExchange(ref uint location, uint value, uint comparand) {

            return (uint)System.Threading.Interlocked.CompareExchange(ref _as<uint, int>(ref location), (int)value, (int)comparand);

        }

        /// <summary>
        /// Acquires the synchronization lock before accessing protected state.
        /// </summary>
        [INLINE(256)]
        public static void Lock(ref LockSpinner spinner) {
            spinner.Lock();
        }

        /// <summary>
        /// Releases the synchronization lock after accessing protected state.
        /// </summary>
        [INLINE(256)]
        public static void Unlock(ref LockSpinner spinner) {
            spinner.Unlock();
        }

    }

}
