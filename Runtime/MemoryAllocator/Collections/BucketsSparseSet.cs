namespace ME.BECS {

    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    /*
    public readonly unsafe struct BucketsSparseSetPtr<T> where T : unmanaged {

        internal readonly Components.Storage<T>* storage;

        [INLINE(256)]
        internal BucketsSparseSetPtr(Components.Storage<T>* storage) {
            this.storage = storage;
        }

        [INLINE(256)]
        public ref T GetValue(SafePtr<State> state, uint id) {
            return ref this.storage->Get(state, id);
        }

        [INLINE(256)]
        public ref readonly T ReadValue(SafePtr<State> state, uint id) {
            return ref this.storage->Read(state, id);
        }

    }
    
    public unsafe struct BucketsSparseSet<T> where T : unmanaged {

        private MemArray<SparseSet<T>> buckets;
        private readonly uint elementsPerBucket;

        [INLINE(256)]
        public BucketsSparseSet(ref MemoryAllocator allocator, uint capacity, uint elementsPerBucket) {

            if (capacity == 0u) capacity = 1u;
            if (elementsPerBucket == 0u) elementsPerBucket = 100u;
            
            this.buckets = new MemArray<SparseSet<T>>(ref allocator, capacity, growFactor: 2);
            this.elementsPerBucket = elementsPerBucket;

        }

        [INLINE(256)]
        public void* GetUnsafePtr(in MemoryAllocator allocator) {
            return this.buckets.GetUnsafePtr(in allocator);
        }

        [INLINE(256)]
        private ref SparseSet<T> GetBucket(ref MemoryAllocator allocator, uint id, out uint offset) {

            var bucketId = id / this.elementsPerBucket;
            offset = bucketId * this.elementsPerBucket;
            if (bucketId >= this.buckets.Length) this.buckets.Resize(ref allocator, bucketId + 1u);
            ref var sparseSet = ref this.buckets[in allocator, bucketId];
            if (sparseSet.isCreated == false) sparseSet = new SparseSet<T>(ref allocator, this.elementsPerBucket);
            return ref sparseSet;

        }

        [INLINE(256)]
        private SparseSet<T> GetBucketRead(in MemoryAllocator allocator, uint id, out uint offset) {

            var bucketId = id / this.elementsPerBucket;
            offset = bucketId * this.elementsPerBucket;
            if (bucketId >= this.buckets.Length) return default;
            ref var sparseSet = ref this.buckets[in allocator, bucketId];
            return sparseSet.isCreated == false ? default : sparseSet;

        }

        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, uint id, in T data, out bool isNew) {

            ref var sparseSet = ref this.GetBucket(ref allocator, id, out uint offset);
            sparseSet.Set(ref allocator, id - offset, data, out isNew);

        }

        [INLINE(256)]
        public bool Remove(ref MemoryAllocator allocator, uint id) {
            
            ref var sparseSet = ref this.GetBucket(ref allocator, id, out uint offset);
            return sparseSet.Remove(ref allocator, id - offset);

        }

        [INLINE(256)]
        public ref T Get(SafePtr<State> state, uint id, out bool isNew) {
            
            ref var sparseSet = ref this.GetBucket(ref state.ptr->allocator, id, out uint offset);
            return ref sparseSet.Get(state, id - offset, out isNew);

        }

        [INLINE(256)]
        public ref T Get(SafePtr<State> state, uint id) {
            
            ref var sparseSet = ref this.GetBucket(ref state.ptr->allocator, id, out uint offset);
            return ref sparseSet.Get(state, id - offset);

        }

        [INLINE(256)]
        public ref readonly T Read(SafePtr<State> state, uint id) {
            
            var sparseSet = this.GetBucketRead(in state.ptr->allocator, id, out uint offset);
            if (sparseSet.isCreated == false) return ref StaticTypes<T>.defaultValue;
            return ref sparseSet.Read(state, id - offset);

        }

        [INLINE(256)]
        public bool Has(in MemoryAllocator allocator, uint id) {
            
            var sparseSet = this.GetBucketRead(in allocator, id, out uint offset);
            if (sparseSet.isCreated == false) return false;
            return sparseSet.Has(in allocator, id - offset);

        }

    }
    */

}