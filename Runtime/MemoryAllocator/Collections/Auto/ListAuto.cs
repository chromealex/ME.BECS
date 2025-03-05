
namespace ME.BECS {

    #if NO_INLINE
    using INLINE = NoInlineAttribute;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using static Cuts;

    [System.SerializableAttribute]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(ListAutoProxy<>))]
    public unsafe struct ListAuto<T> : IMemList, IUnmanagedList, System.IEquatable<ListAuto<T>> where T : unmanaged {

        public struct Enumerator {
            
            private readonly ListAuto<T> list;
            private uint index;

            [INLINE(256)]
            internal Enumerator(in ListAuto<T> list) {
                this.list = list;
                this.index = 0u;
            }

            [INLINE(256)]
            public bool MoveNext() {
                return this.index++ < this.list.Count;
            }

            public ref T Current => ref this.list[this.index - 1u];

        }

        internal MemArrayAuto<T> arr;
        public uint Count;

        public uint GetConfigId() => this.Count;

        public readonly Ent ent => this.arr.ent;
        public Ent Ent => this.ent;
        
        [INLINE(256)]
        public static bool operator ==(in ListAuto<T> a, in ListAuto<T> b) => a.arr.arrPtr == b.arr.arrPtr;
        [INLINE(256)]
        public static bool operator !=(ListAuto<T> a, ListAuto<T> b) => !(a == b);

        object[] IUnmanagedList.ToManagedArray() {
            var arr = new object[this.Count];
            for (uint i = 0u; i < this.Count; ++i) {
                arr[i] = this[i];
            }
            return arr;
        }
        
        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.arr.IsCreated;
        }

        public uint Capacity {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                return this.arr.Length;
            }
        }
        
        [INLINE(256)]
        public ListAuto(in Ent ent, safe_ptr data, uint length) {

            this = default;
            this.arr = new MemArrayAuto<T>(in ent, data, length);
            this.Count = length;

        }
        
        [INLINE(256)]
        public ListAuto(in Ent ent, uint capacity) {

            if (capacity <= 0u) capacity = 1u;
            this = default;
            this.arr = new MemArrayAuto<T>(in ent, capacity);
            this.EnsureCapacity(in ent, capacity);

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.arr.BurstMode(in allocator, state);
        }

        [INLINE(256)]
        public void ReplaceWith(in ListAuto<T> other) {
            
            if (other.arr.arrPtr == this.arr.arrPtr) {
                return;
            }
            
            this.Dispose();
            this = other;
            
        }

        [INLINE(256)]
        public void CopyFrom(in ListAuto<T> other) {

            if (other.arr.arrPtr == this.arr.arrPtr) return;
            if (this.arr.arrPtr.IsValid() == false && other.arr.arrPtr.IsValid() == false) return;
            if (this.arr.arrPtr.IsValid() == true && other.arr.arrPtr.IsValid() == false) {
                this.Dispose();
                return;
            }
            if (this.arr.arrPtr.IsValid() == false) this = new ListAuto<T>(this.ent, other.Capacity);

            var state = this.ent.World.state;
            NativeArrayUtils.Copy(in other.arr, ref this.arr);
            this.Count = other.Count;

        }

        [INLINE(256)]
        public readonly MemPtr GetMemPtr() {
            
            E.IS_CREATED(this);
            return this.arr.arrPtr;
            
        }

        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr(in MemoryAllocator allocator) {

            E.IS_CREATED(this);
            return this.arr.GetUnsafePtr(allocator);

        }

        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr() {

            E.IS_CREATED(this);
            return this.arr.GetUnsafePtr(this.ent.World.state.ptr->allocator);

        }

        [INLINE(256)]
        public void Dispose() {

            E.IS_CREATED(this);
            this.arr.Dispose();
            this = default;
            
        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(ushort worldId, Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeAutoJob() {
                ptr = this.arr.arrPtr,
                ent = this.arr.ent,
                worldId = worldId,
            }.Schedule(inputDeps);
            
            this = default;

            return jobHandle;

        }

        [INLINE(256)]
        public readonly Enumerator GetEnumerator() {

            if (this.IsCreated == false) return default;
            return new Enumerator(in this);
            
        }
        
        [INLINE(256)]
        public void Clear() {

            E.IS_CREATED(this);
            this.Count = 0;

        }

        public ref T this[uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Count);
                return ref this.arr[this.ent.World.state, index];
            }
        }

        public ref T this[safe_ptr<State> state, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Count);
                return ref this.arr[state, index];
            }
        }

        public ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Count);
                return ref this.arr[in allocator, index];
            }
        }

        [INLINE(256)]
        private bool EnsureCapacity(in Ent ent, uint capacity) {

            capacity = Helpers.NextPot(capacity);
            return this.arr.Resize(capacity, 2, ClearOptions.UninitializedMemory);
            
        }
        
        [INLINE(256)]
        public uint Add(T obj) {

            E.IS_CREATED(this);
            ++this.Count;
            this.EnsureCapacity(this.ent, this.Count);

            var state = this.ent.World.state;
            this.arr[in state.ptr->allocator, this.Count - 1u] = obj;
            return this.Count - 1u;

        }

        [INLINE(256)]
        public void Sort<U>() where U : unmanaged, System.IComparable<U> {
            Unity.Collections.NativeSortExtension.Sort((U*)this.GetUnsafePtr().ptr, (int)this.Count);
        }

        [INLINE(256)]
        public readonly bool Contains<U>(U obj) where U : unmanaged, System.IEquatable<T> {
            
            E.IS_CREATED(this);
            for (uint i = 0, cnt = this.Count; i < cnt; ++i) {

                if (obj.Equals(this.arr[i]) == true) {

                    return true;

                }
                
            }

            return false;

        }

        [INLINE(256)]
        public bool Remove<U>(U obj) where U : unmanaged, System.IEquatable<T> {

            E.IS_CREATED(this);
            var state = this.ent.World.state;
            for (uint i = 0, cnt = this.Count; i < cnt; ++i) {

                if (obj.Equals(this.arr[in state.ptr->allocator, i]) == true) {

                    this.RemoveAt(ref state.ptr->allocator, i);
                    return true;

                }
                
            }

            return false;

        }

        [INLINE(256)]
        public bool RemoveFast<U>(U obj) where U : unmanaged, System.IEquatable<T> {

            E.IS_CREATED(this);
            var state = this.ent.World.state;
            for (uint i = 0, cnt = this.Count; i < cnt; ++i) {

                if (obj.Equals(this.arr[in state.ptr->allocator, i]) == true) {

                    this.RemoveAtFast(in state.ptr->allocator, i);
                    return true;

                }

            }

            return false;

        }

        [INLINE(256)]
        public unsafe bool RemoveAt(ref MemoryAllocator allocator, uint index) {
            
            E.IS_CREATED(this);
            if (index >= this.Count) return false;

            if (index == this.Count - 1) {

                --this.Count;
                this.arr[in allocator, this.Count] = default;
                return true;

            }
            
            var ptr = this.arr.arrPtr;
            var size = sizeof(T);
            allocator.MemMove(ptr, size * index, ptr, size * (index + 1), (this.Count - index - 1) * size);
            
            --this.Count;
            this.arr[in allocator, this.Count] = default;
            
            return true;

        }

        [INLINE(256)]
        public bool RemoveAtFast(in MemoryAllocator allocator, uint index) {
            
            E.IS_CREATED(this);
            if (index >= this.Count) return false;
            
            --this.Count;
            var last = this.arr[in allocator, this.Count];
            this.arr[in allocator, index] = last;
            
            return true;

        }

        [INLINE(256)]
        public void AddRange(in ListAuto<T> collection) {
            
            this.AddRange(in collection, 0u, collection.Count);
            
        }

        [INLINE(256)]
        public void AddRange(in ListAuto<T> collection, uint fromIdx, uint toIdx) {

            E.IS_CREATED(this);
            E.IS_CREATED(collection);
            
            var index = this.Count;

            var state = this.ent.World.state;
            var srcOffset = fromIdx;
            var count = toIdx - fromIdx;
            if (count > 0u) {
                this.EnsureCapacity(this.ent, this.Count + count);
                var size = sizeof(T);
                if (index < this.Count) {
                    state.ptr->allocator.MemMove(this.arr.arrPtr, (index + count) * size, this.arr.arrPtr, index * size, (this.Count - index) * size);
                }

                if (this.arr.arrPtr == collection.arr.arrPtr) {
                    state.ptr->allocator.MemMove(this.arr.arrPtr, index * size, this.arr.arrPtr, 0, index * size);
                    state.ptr->allocator.MemMove(this.arr.arrPtr, (index * 2) * size, this.arr.arrPtr, (index + count) * size, (this.Count - index) * size);
                } else {
                    collection.CopyTo(this.arr.arrPtr, srcOffset, index, count);
                }

                this.Count += count;
            }
            
        }

        [INLINE(256)]
        public void AddRange(in UnsafeList<T> collection) {

            E.IS_CREATED(this);
            
            var index = this.Count;
            var count = (uint)collection.Length;
            if (count > 0u) {
                this.EnsureCapacity(this.ent, this.Count + count);
                var size = TSize<T>.size;
                _memcpy((safe_ptr)collection.Ptr, (safe_ptr)(this.arr.GetUnsafePtr(in this.ent.World.state.ptr->allocator) + index * size), count * size);
                this.Count += count;
            }
        }

        [INLINE(256)]
        public void AddRange(in Unity.Collections.NativeArray<T> collection) {

            E.IS_CREATED(this);
            
            var index = this.Count;
            var count = (uint)collection.Length;
            if (count > 0u) {
                this.EnsureCapacity(this.ent, this.Count + count);
                var size = TSize<T>.size;
                _memcpy((safe_ptr)collection.GetUnsafeReadOnlyPtr(), (safe_ptr)(this.arr.GetUnsafePtr(in this.ent.World.state.ptr->allocator) + index * size), count * size);
                this.Count += count;
            }
        }

        [INLINE(256)]
        public void AddRange(in Unity.Collections.NativeArray<T> collection, int offset, int collectionLength) {

            E.IS_CREATED(this);
            
            var index = this.Count;
            var count = (uint)collectionLength;
            if (count > 0u) {
                this.EnsureCapacity(this.ent, this.Count + count);
                var size = TSize<T>.size;
                _memcpy((safe_ptr)collection.GetUnsafeReadOnlyPtr() + offset * TSize<T>.sizeInt, this.arr.GetUnsafePtr(in this.ent.World.state.ptr->allocator) + index * size, count * size);
                this.Count += count;
            }
        }

        [INLINE(256)]
        public void AddRange(in UnsafeList<T> collection, int offset, int collectionLength) {

            E.IS_CREATED(this);
            
            var index = this.Count;
            var count = (uint)collectionLength;
            if (count > 0u) {
                this.EnsureCapacity(this.ent, this.Count + count);
                var size = TSize<T>.size;
                _memcpy((safe_ptr)((byte*)collection.Ptr + offset * TSize<T>.sizeInt), this.arr.GetUnsafePtr(in this.ent.World.state.ptr->allocator) + index * size, count * size);
                this.Count += count;
            }
        }

        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, Unity.Collections.NativeArray<T> collection) {

            E.IS_CREATED(this);
            
            var index = this.Count;
            var count = (uint)collection.Length;
            if (count > 0u) {
                this.EnsureCapacity(this.ent, this.Count + count);
                var size = TSize<T>.size;
                _memcpy((safe_ptr)collection.GetUnsafeReadOnlyPtr(), this.arr.GetUnsafePtr(in allocator) + index * size, count * size);
                this.Count += count;
            }
        }

        [INLINE(256)]
        public void AddRange(ref MemoryAllocator allocator, ListAuto<T> collection) {

            E.IS_CREATED(this);
            
            var index = this.Count;
            var count = collection.Count;
            if (count > 0u) {
                this.EnsureCapacity(this.ent, this.Count + count);
                var size = TSize<T>.size;
                _memcpy(collection.GetUnsafePtr(), this.arr.GetUnsafePtr(in allocator) + index * size, count * size);
                this.Count += count;
            }
        }

        [INLINE(256)]
        public readonly void CopyTo(in MemPtr arrPtr, uint srcOffset, uint index, uint count) {
            
            E.IS_CREATED(this);

            var size = TSize<T>.size;
            this.ent.World.state.ptr->allocator.MemCopy(arrPtr, index * size, this.arr.arrPtr, srcOffset * size, count * size);
            
        }

        public uint GetReservedSizeInBytes() {
            
            return this.arr.GetReservedSizeInBytes();
            
        }

        public override int GetHashCode() => this.arr.GetHashCode();

        public bool Equals(ListAuto<T> other) {
            return this.arr.Equals(other.arr) && this.Count == other.Count;
        }

        public override bool Equals(object obj) {
            return obj is ListAuto<T> other && this.Equals(other);
        }

    }

}