using Unity.Jobs;

namespace ME.BECS {

    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides u int list hash storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct UIntListHash : IIsCreated {

        private MemArray<uint> arr;
        /// <summary>
        /// Indicates hash.
        /// </summary>
        public uint hash;
        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count;

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.arr.IsCreated;
        }

        /// <summary>
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                return this.arr.Length;
            }
        }

        /// <summary>
        /// Initializes <c>UIntListHash</c> with storage for the requested number of elements.
        /// </summary>
        [INLINE(256)]
        public UIntListHash(ref MemoryAllocator allocator, uint capacity) {

            if (capacity <= 0u) capacity = 1u;
            this = default;
            this.EnsureCapacity(ref allocator, capacity);

        }

        /// <summary>
        /// Updates cached native access for the requested Burst execution mode.
        /// </summary>
        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.arr.BurstMode(in allocator, state);
        }

        /// <summary>
        /// Returns the allocator-relative address of the underlying storage.
        /// </summary>
        [INLINE(256)]
        public readonly MemPtr GetMemPtr() {
            
            E.IS_CREATED(this);
            return this.arr.arrPtr;
            
        }

        /// <summary>
        /// Returns a borrowed pointer to collection storage; mutation that reallocates storage or disposal invalidates it.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr(in MemoryAllocator allocator) {

            E.IS_CREATED(this);
            return this.arr.GetUnsafePtr(in allocator);

        }

        /// <summary>
        /// Releases the resources owned by this u int list hash instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            E.IS_CREATED(this);
            this.arr.Dispose(ref allocator);
            this = default;
            
        }

        /// <summary>
        /// Schedules release of the owned storage after the supplied dependency and returns the disposal handle.
        /// </summary>
        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(ushort worldId, Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeJob() {
                ptr = this.arr.arrPtr,
                worldId = worldId,
            }.Schedule(inputDeps);
            
            this = default;

            return jobHandle;

        }

        /// <summary>
        /// Removes stored entries while retaining the backing allocation for reuse.
        /// </summary>
        [INLINE(256)]
        public void Clear() {

            E.IS_CREATED(this);
            this.Count = 0u;
            this.hash = 0u;

        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public ref uint this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Count);
                return ref this.arr[in allocator, index];
            }
        }

        /// <summary>
        /// Grows backing storage when needed to satisfy the requested capacity.
        /// </summary>
        [INLINE(256)]
        public bool EnsureCapacity(ref MemoryAllocator allocator, uint capacity) {

            capacity = Helpers.NextPot(capacity);
            return this.arr.Resize(ref allocator, capacity, 2, ClearOptions.UninitializedMemory);
            
        }
        
        /// <summary>
        /// Adds the supplied entry to u int list hash.
        /// </summary>
        [INLINE(256)]
        public uint Add(ref MemoryAllocator allocator, uint obj) {

            E.IS_CREATED(this);
            ++this.Count;
            this.EnsureCapacity(ref allocator, this.Count);

            this.hash ^= obj;
            this.arr[in allocator, this.Count - 1u] = obj;
            return this.Count - 1u;

        }
        
        [INLINE(256)]
        private void AddNoCheck(in MemoryAllocator allocator, uint obj) {

            this.hash ^= obj;
            this.arr[in allocator, this.Count] = obj;
            ++this.Count;
            
        }

        /// <summary>
        /// Removes fast.
        /// </summary>
        [INLINE(256)]
        public bool RemoveFast(in MemoryAllocator allocator, uint obj) {

            E.IS_CREATED(this);
            for (uint i = 0, cnt = this.Count; i < cnt; ++i) {

                if (obj.Equals(this.arr[in allocator, i]) == true) {

                    this.RemoveAtFast(in allocator, i);
                    return true;

                }

            }

            return false;
            

        }
        
        /// <summary>
        /// Removes the indexed element by moving a tail element into its place; element order is not preserved.
        /// </summary>
        [INLINE(256)]
        public bool RemoveAtFast(in MemoryAllocator allocator, uint index) {
            
            E.IS_CREATED(this);
            if (index >= this.Count) return false;
            
            this.hash ^= this.arr[in allocator, index];
            --this.Count;
            var last = this.arr[in allocator, this.Count];
            this.arr[in allocator, index] = last;
            
            return true;

        }

        /// <summary>
        /// Copies to.
        /// </summary>
        [INLINE(256)]
        public readonly void CopyTo(ref MemoryAllocator allocator, in MemPtr arrPtr, uint srcOffset, uint index, uint count) {
            
            E.IS_CREATED(this);

            const int size = sizeof(uint);
            allocator.MemCopy(arrPtr, index * size, this.arr.arrPtr, srcOffset * size, count * size);
            
        }

        /// <summary>
        /// Returns hash.
        /// </summary>
        [INLINE(256)]
        public readonly uint GetHash() {
            return this.hash;
        }

        /// <summary>
        /// Adds range.
        /// </summary>
        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, in UIntHashSet collection) {

            this.EnsureCapacity(ref allocator, this.Count + collection.Count);
            var e = collection.GetEnumerator(in allocator);
            while (e.MoveNext() == true) {
                var val = e.Current;
                this.AddNoCheck(in allocator, val);
            }

        }

        /// <summary>
        /// Adds range.
        /// </summary>
        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, in UIntListHash collection, uint fromIdx, uint toIdx) {

            E.IS_CREATED(this);
            E.IS_CREATED(collection);
            
            var index = this.Count;
            
            var srcOffset = fromIdx;
            var count = toIdx - fromIdx;
            if (count > 0u) {
                this.EnsureCapacity(ref allocator, this.Count + count);
                var size = TSize<uint>.size;
                if (index < this.Count) {
                    allocator.MemMove(this.arr.arrPtr, (index + count) * size, this.arr.arrPtr, index * size, (this.Count - index) * size);
                }

                if (this.arr.arrPtr == collection.arr.arrPtr) {
                    allocator.MemMove(this.arr.arrPtr, index * size, this.arr.arrPtr, 0, index * size);
                    allocator.MemMove(this.arr.arrPtr, (index * 2) * size, this.arr.arrPtr, (index + count) * size, (this.Count - index) * size);
                } else {
                    collection.CopyTo(ref allocator, this.arr.arrPtr, srcOffset, index, count);
                }

                this.Count += count;
            }
            
        }

        /// <summary>
        /// Converts the value to managed array.
        /// </summary>
        public uint[] ToManagedArray(in MemoryAllocator allocator) {
            
            E.IS_CREATED(this);
            var dst = new uint[this.Count];
            
            CopySafe(in allocator, this, 0, dst, 0, this.Count);
            return dst;
            
        }
        
        private static void CopySafe(
            in MemoryAllocator allocator,
            UIntListHash src,
            int srcIndex,
            uint[] dst,
            int dstIndex,
            uint length) {
            System.Runtime.InteropServices.GCHandle gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(dst, System.Runtime.InteropServices.GCHandleType.Pinned);
            var dstPtr = new safe_ptr((void*)gcHandle.AddrOfPinnedObject(), length * TSize<uint>.size);
            _memcpy(src.GetUnsafePtr(in allocator) + srcIndex * TSize<uint>.sizeInt, dstPtr + dstIndex * TSize<uint>.sizeInt, length * TSize<uint>.size);
            gcHandle.Free();
        }

        /// <summary>
        /// Tests whether the specified value is present.
        /// </summary>
        public bool Contains(in MemoryAllocator allocator, uint value) {

            for (uint i = 0; i < this.Count; ++i) {
                if (this[in allocator, i] == value) return true;
            }

            return false;

        }

        /// <summary>
        /// Returns the amount of reserved storage in bytes.
        /// </summary>
        public uint GetReservedSizeInBytes() {
            return this.arr.GetReservedSizeInBytes();
        }

    }

}