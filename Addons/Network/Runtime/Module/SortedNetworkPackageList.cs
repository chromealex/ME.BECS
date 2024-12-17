using Unity.Jobs;

namespace ME.BECS.Network {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public unsafe struct SortedNetworkPackageList : IIsCreated {

        public struct Enumerator {

            private readonly SortedNetworkPackageList list;
            private uint index;

            internal Enumerator(in SortedNetworkPackageList list) {
                this.list = list;
                this.index = 0u;
            }

            public bool MoveNext() {
                return this.index++ < this.list.Count;
            }

            public ref NetworkPackage GetCurrent(in MemoryAllocator allocator) {
                return ref this.list[in allocator, this.index - 1u];
            }

        }

        internal MemArray<NetworkPackage> arr;
        public uint Count;

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
        public SortedNetworkPackageList(ref MemoryAllocator allocator, uint capacity) {

            if (capacity <= 0u) {
                capacity = 1u;
            }

            this.arr = new MemArray<NetworkPackage>(ref allocator, capacity);
            this.Count = 0u;
            this.EnsureCapacity(ref allocator, capacity);

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.arr.BurstMode(in allocator, state);
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
        public void Dispose(ref MemoryAllocator allocator) {

            E.IS_CREATED(this);
            this.arr.Dispose(ref allocator);
            this = default;

        }

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

        [INLINE(256)]
        public readonly Enumerator GetEnumerator() {

            E.IS_CREATED(this);
            return new Enumerator(in this);

        }

        [INLINE(256)]
        public void Clear() {

            E.IS_CREATED(this);
            this.Count = 0;

        }

        public ref NetworkPackage this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Count);
                return ref this.arr[in allocator, index];
            }
        }

        [INLINE(256)]
        private bool EnsureCapacity(ref MemoryAllocator allocator, uint capacity) {

            E.IS_CREATED(this);
            capacity = Helpers.NextPot(capacity);
            return this.arr.Resize(ref allocator, capacity, 2, ClearOptions.UninitializedMemory);

        }

        [INLINE(256)]
        public void Add(ref MemoryAllocator allocator, NetworkPackage value) {

            E.IS_CREATED(this);
            var i = BinarySearch(in allocator, this.arr, 0, (int)this.Count, value);
            if (i >= 0) {
                throw new System.Exception("Item already exists");
            }

            this.Insert(ref allocator, ~i, value);

        }

        [INLINE(256)]
        private void Insert(ref MemoryAllocator allocator, int index, NetworkPackage value) {

            this.EnsureCapacity(ref allocator, this.Count + 1u);

            if (index < this.Count) {
                this.CopyTo(ref allocator, this.arr, (uint)index, (uint)index + 1u, this.Count - (uint)index);
            }

            this.arr[in allocator, index] = value;
            ++this.Count;

        }

        [INLINE(256)]
        public bool Remove<U>(ref MemoryAllocator allocator, U obj) where U : unmanaged, System.IEquatable<NetworkPackage> {

            E.IS_CREATED(this);
            for (uint i = 0, cnt = this.Count; i < cnt; ++i) {

                if (obj.Equals(this.arr[in allocator, i]) == true) {

                    this.RemoveAt(ref allocator, i);
                    return true;

                }

            }

            return false;

        }

        [INLINE(256)]
        public unsafe bool RemoveAt(ref MemoryAllocator allocator, uint index) {

            E.IS_CREATED(this);
            if (index >= this.Count) {
                return false;
            }

            if (index == this.Count - 1) {

                --this.Count;
                this.arr[in allocator, this.Count] = default;
                return true;

            }

            var ptr = this.arr.arrPtr;
            var size = sizeof(NetworkPackage);
            allocator.MemMove(ptr, size * index, ptr, size * (index + 1), (this.Count - index - 1) * size);

            --this.Count;
            this.arr[in allocator, this.Count] = default;

            return true;

        }

        [INLINE(256)]
        public bool RemoveAtFast(in MemoryAllocator allocator, uint index) {

            E.IS_CREATED(this);
            if (index >= this.Count) {
                return false;
            }

            --this.Count;
            var last = this.arr[in allocator, this.Count];
            this.arr[in allocator, index] = last;

            return true;

        }

        [INLINE(256)]
        public bool Resize(ref MemoryAllocator allocator, uint newLength, ClearOptions options = ClearOptions.ClearMemory) {

            E.IS_CREATED(this);
            if (this.IsCreated == false) {

                this = new SortedNetworkPackageList(ref allocator, newLength);
                return true;

            }

            if (newLength <= this.Count) {

                return false;

            }

            this.arr.Resize(ref allocator, newLength, 2, options);
            this.Count = newLength;
            return true;

        }

        [INLINE(256)]
        public readonly void CopyTo(ref MemoryAllocator allocator, MemArray<NetworkPackage> arr, uint srcOffset, uint index, uint count) {

            E.IS_CREATED(this);
            E.IS_CREATED(arr);

            var size = sizeof(NetworkPackage);
            allocator.MemMove(arr.arrPtr, index * size, this.arr.arrPtr, srcOffset * size, count * size);

        }

        private static int GetMedian(int low, int hi) {
            // Note both may be negative, if we are dealing with arrays w/ negative lower bounds.
            return low + ((hi - low) >> 1);
        }

        public static int BinarySearch(in MemoryAllocator allocator, in MemArray<NetworkPackage> array, int index, int length, NetworkPackage value) {

            var lo = index;
            var hi = index + length - 1;
            while (lo <= hi) {
                // i might overflow if lo and hi are both large positive numbers. 
                var i = GetMedian(lo, hi);

                var c = array[in allocator, i].CompareTo(value);
                if (c == 0) {
                    return i;
                }

                if (c < 0) {
                    lo = i + 1;
                } else {
                    hi = i - 1;
                }
            }

            return ~lo;
        }

    }

}