namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using static Cuts;

    public struct SparseSet {

        public MemArray<uint> dense;
        public MemArray<uint> sparse;
        public uint denseSize;
        public bool isCreated;
        public LockSpinner lockIndex;

        [INLINE(256)]
        public SparseSet(ref MemoryAllocator allocator, uint size) {

            this.isCreated = true;
            this.denseSize = 0u;
            this.dense = new MemArray<uint>(ref allocator, size);
            this.sparse = new MemArray<uint>(ref allocator, size);
            this.lockIndex = default;

        }

        [INLINE(256)]
        private void Validate(ref MemoryAllocator allocator, uint newSize) {

            if (newSize > this.dense.Length) {
                this.dense.Resize(ref allocator, newSize, 2);
                this.sparse.Resize(ref allocator, newSize, 2);
            }

        }

        [System.Diagnostics.Conditional(COND.SPARSESET_VALIDATION)]
        internal void ValidateStruct(in MemoryAllocator allocator) {

            for (int i = 0; i < this.sparse.Length; ++i) {
                var spIdx = this.sparse[allocator, i];
                if (spIdx > 0u) {
                    UnityEngine.Debug.Assert(spIdx <= this.denseSize);
                }
            }

            for (int i = 0; i < this.sparse.Length; ++i) {
                var spIdx = this.sparse[allocator, i];
                if (spIdx > 0u) {
                    for (int j = 0; j < this.sparse.Length; ++j) {
                        if (this.sparse[allocator, j] == spIdx && i != j) {
                            UnityEngine.Debug.Assert(false);
                        }
                    }
                }
            }

            for (int i = 0; i < this.sparse.Length; ++i) {
                var spIdx = this.sparse[allocator, i];
                if (spIdx > 0u) {
                    var res = this.dense[allocator, spIdx - 1] == i;
                    UnityEngine.Debug.Assert(res);
                }
            }
            
        }

        [INLINE(256)]
        public uint Set(ref MemoryAllocator allocator, uint value, out bool isNew) {
            isNew = false;
            this.ValidateStruct(allocator);
            JobUtils.Lock(ref this.lockIndex);
            this.Validate(ref allocator, value + 1u);
            var denseIdx = this.sparse[in allocator, value];
            if (denseIdx > 0u) {
                // element exists
                JobUtils.Unlock(ref this.lockIndex);
                return denseIdx - 1u;
            }

            isNew = true;
            this.Validate(ref allocator, this.denseSize + 1u);
            this.sparse[in allocator, value] = this.denseSize + 1u;
            this.dense[in allocator, this.denseSize] = value;
            ++this.denseSize;
            JobUtils.Unlock(ref this.lockIndex);
            this.ValidateStruct(allocator);
            return this.denseSize - 1u;
        }

        [INLINE(256)]
        public bool Remove(in MemoryAllocator allocator, uint value, out uint fromIndex, out uint toIndex) {
            fromIndex = 0u;
            toIndex = 0u;
            this.ValidateStruct(allocator);
            JobUtils.Lock(ref this.lockIndex);
            if (value >= this.sparse.Length) {
                JobUtils.Unlock(ref this.lockIndex);
                return false;
            }
            var denseIdx = this.sparse[in allocator, value];
            if (denseIdx > 0u) {
                if (denseIdx <= this.denseSize - 1u) {
                    ref var sparseIdxVal = ref this.dense[in allocator, this.denseSize - 1u];
                    var sparseIdx = sparseIdxVal;
                    sparseIdxVal = 0u;
                    this.sparse[in allocator, sparseIdx] = denseIdx;
                    this.dense[in allocator, denseIdx - 1u] = sparseIdx;
                } else {
                    this.dense[in allocator, denseIdx - 1u] = 0u;
                }
                this.sparse[in allocator, value] = 0u;

                fromIndex = this.denseSize - 1u;
                toIndex = denseIdx - 1u;
                --this.denseSize;
                this.ValidateStruct(allocator);
                JobUtils.Unlock(ref this.lockIndex);
                return true;
            }
            JobUtils.Unlock(ref this.lockIndex);
            return false;
        }

        [INLINE(256)]
        public readonly bool Has(in MemoryAllocator allocator, uint value, out uint index) {
            index = 0u;
            if (value >= this.sparse.Length) return false;
            var idx = this.sparse[in allocator, value];
            index = idx - 1u;
            return idx > 0u;
        }

        [INLINE(256)]
        public readonly bool Read(in MemoryAllocator allocator, uint value, out uint index) {
            index = 0u;
            if (value >= this.sparse.Length) return false;
            var denseIdx = this.sparse[in allocator, value];
            if (denseIdx > 0u) {
                // element exists
                index = denseIdx - 1u;
                return true;
            }
            return false;
        }

    }

}
