#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Jobs;
    using static Cuts;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides thread cache line storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayThreadCacheLineProxy<>))]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct ThreadCacheLine<T> : IIsCreated where T : unmanaged {

        private static readonly uint CACHE_LINE_SIZE = _align(TSize<T>.size, JobUtils.CacheLineSize);

        private readonly safe_ptr arrPtr;
        private readonly Unity.Collections.Allocator allocator;
        /// <summary>
        /// Number of elements exposed by this value.
        /// </summary>
        public static uint Length => JobUtils.ThreadsCount;
        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count => Length;

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.arrPtr.ptr != null;
        }

        /// <summary>
        /// Initializes <c>ThreadCacheLine</c> from the supplied allocator, clear options.
        /// </summary>
        [INLINE(256)]
        public ThreadCacheLine(Unity.Collections.Allocator allocator, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            this.allocator = allocator;
            var memPtr = _make(CACHE_LINE_SIZE * Length, TAlign<T>.alignInt, allocator);
            if (clearOptions == ClearOptions.ClearMemory) {
                _memclear(memPtr, CACHE_LINE_SIZE * Length);
            }
            
            this.arrPtr = memPtr;

        }

        /// <summary>
        /// Releases the resources owned by this thread cache line instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {

            E.IS_CREATED(this);

            if (this.arrPtr.ptr != null) {
                _free(this.arrPtr, this.allocator);
            }
            this = default;

        }

        /// <summary>
        /// Schedules release of the owned storage after the supplied dependency and returns the disposal handle.
        /// </summary>
        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeWithAllocatorPtrJob() {
                ptr = this.arrPtr,
                allocator = this.allocator,
            }.Schedule(inputDeps);
            
            return jobHandle;

        }

        /// <summary>
        /// Returns a borrowed pointer to collection storage; mutation that reallocates storage or disposal invalidates it.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr() {

            return this.arrPtr;

        }
        
        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr() + index * CACHE_LINE_SIZE).ptr;
            }
        }

        /// <summary>
        /// Zeroes the stored data while preserving the length and backing allocation.
        /// </summary>
        [INLINE(256)]
        public void Clear() {

            _memclear(this.arrPtr, Length * CACHE_LINE_SIZE);

        }

        /// <summary>
        /// Returns the amount of reserved storage in bytes.
        /// </summary>
        public uint GetReservedSizeInBytes() {

            return Length * CACHE_LINE_SIZE;

        }

    }

}