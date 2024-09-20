using Unity.Jobs;

namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Mathematics;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayThreadCacheLineProxy<>))]
    public unsafe struct MemArrayThreadCacheLine<T> : IIsCreated where T : unmanaged {

        private static readonly uint CACHE_LINE_SIZE = math.max(JobUtils.CacheLineSize / TSize<T>.size, 1u);

        private readonly MemPtr arrPtr;
        public readonly uint Length => JobUtils.ThreadsCount;

        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.arrPtr.IsValid();
        }

        [INLINE(256)]
        public MemArrayThreadCacheLine(ref MemoryAllocator allocator, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            this = default;
            var size = TSize<T>.size;
            var memPtr = allocator.Alloc(this.Length * size * CACHE_LINE_SIZE);
            
            if (clearOptions == ClearOptions.ClearMemory) {
                allocator.MemClear(memPtr, 0u, this.Length * size * CACHE_LINE_SIZE);
            }
            
            this.arrPtr = memPtr;

        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            E.IS_CREATED(this);

            if (this.arrPtr.IsValid() == true) {
                allocator.Free(this.arrPtr);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(ushort worldId, Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeJob() {
                ptr = this.arrPtr,
                worldId = worldId,
            }.Schedule(inputDeps);
            
            return jobHandle;

        }

        [INLINE(256)]
        public readonly void* GetUnsafePtr(in MemoryAllocator allocator) {

            return allocator.GetUnsafePtr(this.arrPtr);

        }

        public readonly ref T this[State* state, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtr(in state->allocator) + index * CACHE_LINE_SIZE);
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtr(in allocator) + index * CACHE_LINE_SIZE);
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtr(in allocator) + index * CACHE_LINE_SIZE);
            }
        }

        public readonly ref T this[State* state, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtr(in state->allocator) + index * CACHE_LINE_SIZE);
            }
        }

        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator) {

            var size = TSize<T>.size * CACHE_LINE_SIZE;
            allocator.MemClear(this.arrPtr, 0L, this.Length * size);

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            
        }

        public uint GetReservedSizeInBytes() {

            return this.Length * (uint)sizeof(T) * CACHE_LINE_SIZE;

        }

    }

}