namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Jobs;
    using Unity.Mathematics;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayThreadCacheLineProxy<>))]
    public unsafe struct MemArrayThreadCacheLine<T> : IIsCreated where T : unmanaged {

        private static readonly uint CACHE_LINE_SIZE_IN_BYTES = math.max(JobUtils.CacheLineSize, TSize<T>.size);

        private readonly MemPtr arrPtr;
        public readonly uint Length => JobUtils.ThreadsCount;

        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.arrPtr.IsValid();
        }

        [INLINE(256)]
        public MemArrayThreadCacheLine(ref MemoryAllocator allocator, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            this = default;
            var memPtr = allocator.Alloc(this.Length * CACHE_LINE_SIZE_IN_BYTES);
            if (clearOptions == ClearOptions.ClearMemory) {
                allocator.MemClear(memPtr, 0u, this.Length * CACHE_LINE_SIZE_IN_BYTES);
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
        public readonly safe_ptr GetUnsafePtr(in MemoryAllocator allocator) {

            return allocator.GetUnsafePtr(this.arrPtr);

        }

        public readonly ref T this[safe_ptr<State> state, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr(in state.ptr->allocator) + (uint)index * CACHE_LINE_SIZE_IN_BYTES).ptr;
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr(in allocator) + (uint)index * CACHE_LINE_SIZE_IN_BYTES).ptr;
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr(in allocator) + index * CACHE_LINE_SIZE_IN_BYTES).ptr;
            }
        }

        public readonly ref T this[safe_ptr<State> state, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr(in state.ptr->allocator) + index * CACHE_LINE_SIZE_IN_BYTES).ptr;
            }
        }

        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator) {

            allocator.MemClear(this.arrPtr, 0L, this.Length * CACHE_LINE_SIZE_IN_BYTES);

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            
        }

        public uint GetReservedSizeInBytes() {

            return this.Length * CACHE_LINE_SIZE_IN_BYTES;

        }

    }

}