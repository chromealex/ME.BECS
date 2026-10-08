using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using static Cuts;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides list storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(ListProxy<>))]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct List<T> : IIsCreated where T : unmanaged {

        /// <summary>
        /// Storage size or fixed element count used by this representation.
        /// </summary>
        public const int SIZE = MemArray<T>.SIZE + sizeof(uint);

        /// <summary>
        /// Traverses the entries exposed by <c>List</c>.
        /// </summary>
        public struct Enumerator {
            
            private readonly List<T> list;
            private uint index;

            internal Enumerator(in List<T> list) {
                this.list = list;
                this.index = 0u;
            }

            /// <summary>
            /// Advances the enumerator and reports whether a current element is available.
            /// </summary>
            public bool MoveNext() {
                return this.index++ < this.list.Count;
            }

            /// <summary>
            /// Returns current.
            /// </summary>
            public ref T GetCurrent(in MemoryAllocator allocator) => ref this.list[in allocator, this.index - 1u];

        }

        private MemArray<T> arr;
        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count;

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.arr);
            writer.Write(this.Count);
        }

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.arr);
            reader.Read(ref this.Count);
        }

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
        /// Initializes <c>List</c> with storage for the requested number of elements.
        /// </summary>
        [INLINE(256)]
        public List(ref MemoryAllocator allocator, uint capacity) {

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
        /// Disposes the current storage and copies the other collection handle; the two values then refer to the same allocation.
        /// </summary>
        [INLINE(256)]
        public void ReplaceWith(ref MemoryAllocator allocator, in List<T> other) {
            
            if (other.arr.arrPtr == this.arr.arrPtr) {
                return;
            }
            
            this.Dispose(ref allocator);
            this = other;
            
        }

        /// <summary>
        /// Copies the supplied source state into this list instance.
        /// </summary>
        [INLINE(256)]
        public void CopyFrom(ref MemoryAllocator allocator, in List<T> other) {

            if (other.arr.arrPtr == this.arr.arrPtr) return;
            if (this.arr.arrPtr.IsValid() == false && other.arr.arrPtr.IsValid() == false) return;
            if (this.arr.arrPtr.IsValid() == true && other.arr.arrPtr.IsValid() == false) {
                this.Dispose(ref allocator);
                return;
            }
            if (this.arr.arrPtr.IsValid() == false) this = new List<T>(ref allocator, other.Capacity);
            
            NativeArrayUtils.Copy(ref allocator, in other.arr, ref this.arr);
            this.Count = other.Count;

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
            return this.arr.GetUnsafePtr(allocator);

        }

        /// <summary>
        /// Releases the resources owned by this list instance.
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
        /// Returns an enumerator over the current collection contents.
        /// </summary>
        [INLINE(256)]
        public readonly Enumerator GetEnumerator() {

            if (this.IsCreated == false) return default;
            return new Enumerator(in this);
            
        }
        
        /// <summary>
        /// Removes stored entries while retaining the backing allocation for reuse.
        /// </summary>
        [INLINE(256)]
        public void Clear() {

            E.IS_CREATED(this);
            this.Count = 0;

        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public ref T this[safe_ptr<State> state, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Count);
                return ref this.arr[state, index];
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Count);
                return ref this.arr[in allocator, index];
            }
        }

        [INLINE(256)]
        private bool EnsureCapacity(ref MemoryAllocator allocator, uint capacity) {

            capacity = Helpers.NextPot(capacity);
            return this.arr.Resize(ref allocator, capacity, 1, ClearOptions.UninitializedMemory);
            
        }
        
        /// <summary>
        /// Adds the supplied entry to list.
        /// </summary>
        [INLINE(256)]
        public uint Add(ref MemoryAllocator allocator, T obj) {

            E.IS_CREATED(this);
            ++this.Count;
            this.EnsureCapacity(ref allocator, this.Count);

            this.arr[in allocator, this.Count - 1u] = obj;
            return this.Count - 1u;

        }

        /// <summary>
        /// Tests whether the specified value is present.
        /// </summary>
        [INLINE(256)]
        public readonly bool Contains<U>(in MemoryAllocator allocator, U obj) where U : unmanaged, System.IEquatable<T> {
            
            E.IS_CREATED(this);
            for (uint i = 0, cnt = this.Count; i < cnt; ++i) {

                if (obj.Equals(this.arr[in allocator, i]) == true) {

                    return true;

                }
                
            }

            return false;

        }

        /// <summary>
        /// Removes the specified entry from list.
        /// </summary>
        [INLINE(256)]
        public bool Remove<U>(ref MemoryAllocator allocator, U obj) where U : unmanaged, System.IEquatable<T> {

            E.IS_CREATED(this);
            for (uint i = 0, cnt = this.Count; i < cnt; ++i) {

                if (obj.Equals(this.arr[in allocator, i]) == true) {

                    this.RemoveAt(ref allocator, i);
                    return true;

                }
                
            }

            return false;

        }

        /// <summary>
        /// Removes fast.
        /// </summary>
        [INLINE(256)]
        public bool RemoveFast<U>(in MemoryAllocator allocator, U obj) where U : unmanaged, System.IEquatable<T> {

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
        /// Removes at.
        /// </summary>
        [INLINE(256)]
        public unsafe bool RemoveAt(ref MemoryAllocator allocator, uint index) {
            
            E.IS_CREATED(this);
            E.RANGE(index, 0, this.Count);
            if (index >= this.Count) return false;

            if (index == this.Count - 1) {

                --this.Count;
                this.arr[in allocator, this.Count] = default;
                return true;

            }
            
            var ptr = this.arr.arrPtr;
            var size = TSize<T>.size;
            allocator.MemMove(ptr, size * index, ptr, size * (index + 1), (this.Count - index - 1) * size);
            
            --this.Count;
            this.arr[in allocator, this.Count] = default;
            
            return true;

        }

        /// <summary>
        /// Removes the indexed element by moving a tail element into its place; element order is not preserved.
        /// </summary>
        [INLINE(256)]
        public bool RemoveAtFast(in MemoryAllocator allocator, uint index) {
            
            E.IS_CREATED(this);
            E.RANGE(index, 0, this.Count);
            if (index >= this.Count) return false;
            
            --this.Count;
            var last = this.arr[in allocator, this.Count];
            this.arr[in allocator, index] = last;
            
            return true;

        }

        /// <summary>
        /// Creates storage or grows capacity to accommodate the requested length without changing the element count; smaller requests do not shrink storage.
        /// </summary>
        [INLINE(256)]
        public bool Resize(ref MemoryAllocator allocator, uint newLength) {

            if (this.IsCreated == false) {
                
                this = new List<T>(ref allocator, newLength);
                return true;

            }
            
            if (newLength <= this.Capacity) {

                return false;
                
            }

            return this.EnsureCapacity(ref allocator, newLength);

        }

        /// <summary>
        /// Adds range.
        /// </summary>
        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, in List<T> collection) {
            
            this.AddRange(ref allocator, in collection, 0u, collection.Count);
            
        }

        /// <summary>
        /// Adds range.
        /// </summary>
        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, in List<T> collection, uint fromIdx, uint toIdx) {

            E.IS_CREATED(this);
            E.IS_CREATED(collection);
            
            var index = this.Count;
            
            var srcOffset = fromIdx;
            var count = toIdx - fromIdx;
            if (count > 0u) {
                this.EnsureCapacity(ref allocator, this.Count + count);
                var size = TSize<T>.size;
                if (index < this.Count) {
                    allocator.MemMove(this.arr.arrPtr, (index + count) * size, this.arr.arrPtr, index * size, (this.Count - index) * size);
                }

                if (this.arr.arrPtr == collection.arr.arrPtr) {
                    allocator.MemMove(this.arr.arrPtr, index * size, this.arr.arrPtr, 0, index * size);
                    allocator.MemMove(this.arr.arrPtr, (index * 2) * size, this.arr.arrPtr, (index + count) * size, (this.Count - index) * size);
                } else {
                    collection.CopyTo(ref allocator, this.arr, srcOffset, index, count);
                }

                this.Count += count;
            }
            
        }

        /// <summary>
        /// Adds range.
        /// </summary>
        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, MemArray<T> collection) {

            E.IS_CREATED(this);
            E.IS_CREATED(collection);
            
            var index = this.Count;
            var count = collection.Length;
            if (count > 0u) {
                this.EnsureCapacity(ref allocator, this.Count + count);
                var size = TSize<T>.size;
                if (index < this.Count) {
                    allocator.MemMove(this.arr.arrPtr, (index + count) * size, this.arr.arrPtr, index * size, (this.Count - index) * size);
                }

                if (this.arr.arrPtr == collection.arrPtr) {
                    allocator.MemMove(this.arr.arrPtr, index * size, this.arr.arrPtr, 0, index * size);
                    allocator.MemMove(this.arr.arrPtr, (index * 2) * size, this.arr.arrPtr, (index + count) * size, (this.Count - index) * size);
                } else {
                    this.CopyFrom(ref allocator, collection, index);
                }

                this.Count += count;
            }
        }

        /// <summary>
        /// Adds range.
        /// </summary>
        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, Unity.Collections.NativeArray<T> collection) {

            E.IS_CREATED(this);
            
            var index = this.Count;
            var count = (uint)collection.Length;
            if (count > 0u) {
                this.EnsureCapacity(ref allocator, this.Count + count);
                var size = TSize<T>.size;
                _memcpy((safe_ptr)collection.GetUnsafeReadOnlyPtr(), (safe_ptr)(this.arr.GetUnsafePtr(in allocator) + index * size), count * size);
                this.Count += count;
            }
        }

        /// <summary>
        /// Adds range.
        /// </summary>
        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, in UnsafeList<T> collection) {

            E.IS_CREATED(this);
            
            var index = this.Count;
            var count = (uint)collection.Length;
            if (count > 0u) {
                this.EnsureCapacity(ref allocator, this.Count + count);
                var size = TSize<T>.size;
                _memcpy((safe_ptr)collection.Ptr, (safe_ptr)(this.arr.GetUnsafePtr(in allocator) + index * size), count * size);
                this.Count += count;
            }
        }

        /// <summary>
        /// Copies to.
        /// </summary>
        [INLINE(256)]
        public readonly void CopyTo(ref MemoryAllocator allocator, MemArray<T> arr, uint srcOffset, uint index, uint count) {
            
            E.IS_CREATED(this);
            E.IS_CREATED(arr);

            var size = TSize<T>.size;
            allocator.MemCopy(arr.arrPtr, index * size, this.arr.arrPtr, srcOffset * size, count * size);
            
        }

        /// <summary>
        /// Copies to.
        /// </summary>
        [INLINE(256)]
        public readonly void CopyTo(ref MemoryAllocator allocator, in MemPtr arrPtr, uint srcOffset, uint index, uint count) {
            
            E.IS_CREATED(this);

            var size = TSize<T>.size;
            allocator.MemCopy(arrPtr, index * size, this.arr.arrPtr, srcOffset * size, count * size);
            
        }

        /// <summary>
        /// Copies the supplied source state into this list instance.
        /// </summary>
        [INLINE(256)]
        public readonly void CopyFrom(ref MemoryAllocator allocator, MemArray<T> arr, uint index) {

            E.IS_CREATED(this);
            E.IS_CREATED(arr);

            var size = TSize<T>.size;
            allocator.MemCopy(this.arr.arrPtr, index * size, arr.arrPtr, 0, arr.Length * size);

        }

        /// <summary>
        /// Orders the stored entries using the supplied comparison.
        /// </summary>
        [INLINE(256)]
        public void Sort<U>(safe_ptr<State> state) where U : unmanaged, System.IComparable<U> {
            Unity.Collections.NativeSortExtension.Sort((U*)this.GetUnsafePtr(in state.ptr->allocator).ptr, (int)this.Count);
        }

        /// <summary>
        /// Returns the amount of reserved storage in bytes.
        /// </summary>
        public uint GetReservedSizeInBytes() {
            
            return this.arr.GetReservedSizeInBytes() + TSize<List<uint>>.size;
            
        }

    }

}