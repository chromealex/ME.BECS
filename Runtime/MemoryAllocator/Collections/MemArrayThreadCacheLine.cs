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
    /// Provides mem array thread cache line storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayThreadCacheLineProxy<>))]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct MemArrayThreadCacheLine<T> : IIsCreated where T : unmanaged {

        private static readonly uint CACHE_LINE_SIZE = _align(TSize<T>.size, JobUtils.CacheLineSizeFixed);

        private MemPtr arrPtr;
        /// <summary>
        /// Number of elements exposed by this value.
        /// </summary>
        public readonly uint Length => JobUtils.ThreadsCountMax;

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.arrPtr);
        }

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.arrPtr);
        }

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.arrPtr.IsValid();
        }

        /// <summary>
        /// Initializes <c>MemArrayThreadCacheLine</c> from the supplied allocator, clear options.
        /// </summary>
        [INLINE(256)]
        public MemArrayThreadCacheLine(ref MemoryAllocator allocator, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            this = default;
            var memPtr = allocator.Alloc(CACHE_LINE_SIZE * this.Length);
            if (clearOptions == ClearOptions.ClearMemory) {
                allocator.MemClear(memPtr, 0u, CACHE_LINE_SIZE * this.Length);
            }
            
            this.arrPtr = memPtr;

        }

        /// <summary>
        /// Releases the resources owned by this mem array thread cache line instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            E.IS_CREATED(this);

            if (this.arrPtr.IsValid() == true) {
                allocator.Free(this.arrPtr);
            }
            this = default;

        }

        /// <summary>
        /// Schedules release of the owned storage after the supplied dependency and returns the disposal handle.
        /// </summary>
        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(ushort worldId, Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeJob() {
                ptr = this.arrPtr,
                worldId = worldId,
            }.Schedule(inputDeps);
            
            return jobHandle;

        }

        /// <summary>
        /// Returns a borrowed pointer to collection storage; mutation that reallocates storage or disposal invalidates it.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr(in MemoryAllocator allocator) {

            return allocator.GetUnsafePtr(this.arrPtr);

        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[safe_ptr<State> state, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr(in state.ptr->allocator) + (uint)index * CACHE_LINE_SIZE).ptr;
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[in MemoryAllocator allocator, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr(in allocator) + (uint)index * CACHE_LINE_SIZE).ptr;
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr(in allocator) + index * CACHE_LINE_SIZE).ptr;
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[safe_ptr<State> state, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr(in state.ptr->allocator) + index * CACHE_LINE_SIZE).ptr;
            }
        }

        /// <summary>
        /// Zeroes the stored data while preserving the length and backing allocation.
        /// </summary>
        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator) {

            allocator.MemClear(this.arrPtr, 0L, this.Length * CACHE_LINE_SIZE);

        }

        /// <summary>
        /// Provides the <c>BurstMode</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            
        }

        /// <summary>
        /// Returns the amount of reserved storage in bytes.
        /// </summary>
        public uint GetReservedSizeInBytes() {

            return this.Length * CACHE_LINE_SIZE;

        }

    }

}