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
            this.dense = new MemArray<uint>(ref allocator, size, growFactor: 2);
            this.sparse = new MemArray<uint>(ref allocator, size, growFactor: 2);
            this.lockIndex = default;

        }

        [INLINE(256)]
        private void Validate(ref MemoryAllocator allocator, uint newSize) {

            if (newSize > this.dense.Length) {
                this.dense.Resize(ref allocator, newSize);
                this.sparse.Resize(ref allocator, newSize);
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
            if (value >= this.sparse.Length) return false;
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

    public unsafe struct IndexPage {

        public MemArray<uint> entToDataIdx;
        public MemArray<uint> dataIdxToEnt;
        public MemArray<ushort> generations;
        public uint headIndex;
        public LockSpinner lockIndex;

        public uint GetReservedSizeInBytes(State* state) {

            var size = 0u;
            if (this.generations.isCreated == true) {
                size += this.entToDataIdx.GetReservedSizeInBytes();
                size += this.dataIdxToEnt.GetReservedSizeInBytes();
                size += this.generations.GetReservedSizeInBytes();
            }

            return size;

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            
            this.entToDataIdx.BurstMode(in allocator, state);
            this.dataIdxToEnt.BurstMode(in allocator, state);
            this.generations.BurstMode(in allocator, state);
            
        }

    }

    public unsafe struct DataPage {

        public MemPtr data;
        public bool isCreated;
        public LockSpinner lockIndex;

        public uint GetReservedSizeInBytes(State* state, uint dataSize, uint dataPerPage) {

            if (this.isCreated == true) {
                return dataSize * dataPerPage;
            }
            return 0u;

        }

    }

    public unsafe partial struct SparseSetUnknownType {

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.indexPages.BurstMode(in allocator, state);
            this.dataPages.BurstMode(in allocator, state);
            for (uint i = 0; i < this.indexPages.Length; ++i) {
                ref var pageIndex = ref this.indexPages[in allocator, i];
                pageIndex.BurstMode(in allocator, state);
            }
        }

        [INLINE(256)]
        private static uint _size(uint capacity) {
            return (capacity - 1u) / ENTITIES_PER_PAGE + 1u;
        }

        [INLINE(256)]
        private static ref IndexPage _pageIndex(State* state, in MemArray<IndexPage> pages, ref uint entityId, out uint pageIndex) {

            pageIndex = entityId / ENTITIES_PER_PAGE;
            ref var page = ref pages[state, pageIndex];
            if (page.generations.isCreated == false) {
                JobUtils.Lock(ref page.lockIndex);
                if (page.generations.isCreated == false) {
                    page.entToDataIdx = new MemArray<uint>(ref state->allocator, ENTITIES_PER_PAGE);
                    page.dataIdxToEnt = new MemArray<uint>(ref state->allocator, ENTITIES_PER_PAGE);
                    page.generations = new MemArray<ushort>(ref state->allocator, ENTITIES_PER_PAGE);
                }
                JobUtils.Unlock(ref page.lockIndex);
            }
            entityId %= ENTITIES_PER_PAGE;
            return ref page;

        }

        [INLINE(256)]
        private static uint _index(uint localIndex, uint pageIndex) {
            return localIndex - 1u + pageIndex * ENTITIES_PER_PAGE;
        }

        [INLINE(256)]
        private static uint _sizeData(uint capacity, uint dataPerPage) {
            var cap = (capacity - 1u) / dataPerPage + 1u;
            return Unity.Mathematics.math.max(cap, MIN_DATA_PAGES_CAPACITY);
        }

        [INLINE(256)]
        private static ref DataPage _pageData(State* state, in MemArray<DataPage> pages, uint headIndex, uint dataSize, uint dataPerPage, out uint pageIndex) {
            
            pageIndex = headIndex / dataPerPage;
            ref var page = ref pages[state, pageIndex];
            if (page.isCreated == false) {
                JobUtils.Lock(ref page.lockIndex);
                if (page.isCreated == false) {
                    page.data = MemoryAllocatorExt.Alloc(ref state->allocator, dataSize * dataPerPage);
                    page.isCreated = true;
                }
                JobUtils.Unlock(ref page.lockIndex);
            }

            return ref page;

        }

        [INLINE(256)]
        private static uint _dataIndex(uint absoluteIndex, uint pageIndex, uint dataPerPage) {
            return absoluteIndex % dataPerPage;
        }

    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe partial struct SparseSetUnknownType {

        private const uint MIN_DATA_PAGES_CAPACITY = 10u;
        private const uint ENTITIES_PER_PAGE = 64u;
        private const uint PAGE_SIZE_BYTES_PER_PAGE = 1024u; // 1 KB

        private MemArray<IndexPage> indexPages;
        private uint version;
        private MemArray<DataPage> dataPages;
        private readonly uint dataSize;
        private readonly uint dataPerPage;

        [INLINE(256)]
        public SparseSetUnknownType(State* state, uint dataSize, uint capacity, uint entitiesCapacity) {

            this.dataSize = dataSize;
            if (this.dataSize == 0u) dataSize = 1u;
            this.dataPerPage = PAGE_SIZE_BYTES_PER_PAGE / dataSize;
            this.indexPages = new MemArray<IndexPage>(ref state->allocator, _size(entitiesCapacity), growFactor: 2);
            this.dataPages = new MemArray<DataPage>(ref state->allocator, _sizeData(entitiesCapacity, this.dataPerPage), growFactor: 2);
            this.version = state->allocator.version;
            MemoryAllocatorExt.ValidateConsistency(state->allocator);

        }

        public uint GetReservedSizeInBytes(State* state) {

            var size = 0u;
            for (int i = 0; i < this.indexPages.Length; ++i) {
                size += this.indexPages[state, i].GetReservedSizeInBytes(state);
            }
            for (int i = 0; i < this.dataPages.Length; ++i) {
                size += this.dataPages[state, i].GetReservedSizeInBytes(state, this.dataSize, this.dataPerPage);
            }
            return size;

        }

        [INLINE(256)]
        private void Resize(State* state, uint newLength) {

            {
                var length = _size(newLength);
                if (length >= this.indexPages.Length) {
                    this.indexPages.Resize(ref state->allocator, length);
                }
            }

            {
                var length = _sizeData(newLength, this.dataPerPage);
                if (length >= this.dataPages.Length) {
                    this.dataPages.Resize(ref state->allocator, length);
                }
            }

            this.version = state->allocator.version;
            
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            
        }

        [INLINE(256)]
        public void OnEntityAdd(State* state, uint entityId) {
            this.Resize(state, entityId + 1u);
        }

        [INLINE(256)]
        public void Set(State* state, uint entityId, ushort entityGen, void* data, out bool isNew) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            isNew = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            ref var headIndex = ref page.entToDataIdx[state, entityId];
            if (gen != entityGen) {
                isNew = true;
                if (headIndex == 0u) headIndex = ++page.headIndex;
                page.dataIdxToEnt[state, headIndex - 1u] = entityId;
                gen = entityGen;
            }
            JobUtils.Unlock(ref page.lockIndex);
            
            var globalHeadIndex = _index(headIndex, pageIndex);
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            JobUtils.Lock(ref dataPage.lockIndex);
            byte* ptr = MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            if (data == null) {
                _memclear(ptr, this.dataSize);
            } else {
                _memcpy(data, ptr, this.dataSize);
            }
            JobUtils.Unlock(ref dataPage.lockIndex);

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            
        }

        [INLINE(256)]
        public void Set<T>(State* state, uint entityId, ushort entityGen, in T data, out bool isNew) where T : unmanaged {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            isNew = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            ref var headIndex = ref page.entToDataIdx[state, entityId];
            if (gen != entityGen) {
                isNew = true;
                if (headIndex == 0u) headIndex = ++page.headIndex;
                page.dataIdxToEnt[state, headIndex - 1u] = entityId;
                gen = entityGen;
            }
            JobUtils.Unlock(ref page.lockIndex);
            
            var globalHeadIndex = _index(headIndex, pageIndex);
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            JobUtils.Lock(ref dataPage.lockIndex);
            var vPtr = MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            *(T*)vPtr = data;
            JobUtils.Unlock(ref dataPage.lockIndex);
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            
        }

        [INLINE(256)]
        public bool Remove(State* state, uint entityId, ushort entityGen) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            var res = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            if (gen == entityGen) {
                E.RANGE(page.headIndex, 1u, ENTITIES_PER_PAGE + 1u);
                var targetIdx = page.entToDataIdx[state, entityId];
                E.RANGE(targetIdx, 1u, ENTITIES_PER_PAGE + 1u);
                if (targetIdx - 1u < page.headIndex - 1u) {
                    var moveEntId = page.dataIdxToEnt[state, page.headIndex - 1u];
                    ref var lastIdx = ref page.entToDataIdx[state, moveEntId];
                    var copyIdx = lastIdx;
                    lastIdx = targetIdx;
                    page.dataIdxToEnt[state, targetIdx - 1u] = moveEntId;
                    // move data
                    var globalHeadIndexTarget = _index(targetIdx, pageIndex);
                    var globalHeadIndexCopy = _index(copyIdx, pageIndex);
                    ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndexTarget, this.dataSize, this.dataPerPage, out var pageDataIndex);
                    ref var fromDataPage = ref _pageData(state, in this.dataPages, globalHeadIndexCopy, this.dataSize, this.dataPerPage, out var fromPageDataIndex);
                    if (pageDataIndex == fromPageDataIndex) {
                        JobUtils.Lock(ref dataPage.lockIndex);
                        state->allocator.MemMove(in dataPage.data,
                                                 _dataIndex(globalHeadIndexTarget, pageDataIndex, this.dataPerPage) * this.dataSize,
                                                 in fromDataPage.data,
                                                 _dataIndex(globalHeadIndexCopy, fromPageDataIndex, this.dataPerPage) * this.dataSize,
                                                 this.dataSize);
                        JobUtils.Unlock(ref dataPage.lockIndex);
                        MemoryAllocatorExt.ValidateConsistency(state->allocator);
                    } else {
                        JobUtils.Lock(ref dataPage.lockIndex);
                        JobUtils.Lock(ref fromDataPage.lockIndex);
                        state->allocator.MemCopy(in dataPage.data,
                                                 _dataIndex(globalHeadIndexTarget, pageDataIndex, this.dataPerPage) * this.dataSize,
                                                 in fromDataPage.data,
                                                 _dataIndex(globalHeadIndexCopy, fromPageDataIndex, this.dataPerPage) * this.dataSize,
                                                 this.dataSize);
                        JobUtils.Unlock(ref fromDataPage.lockIndex);
                        JobUtils.Unlock(ref dataPage.lockIndex);
                        MemoryAllocatorExt.ValidateConsistency(state->allocator);
                    }
                }

                page.entToDataIdx[state, entityId] = 0u;
                page.generations[in state->allocator, entityId] = 0;
                --page.headIndex;
                res = true;
                --gen;
            }
            JobUtils.Unlock(ref page.lockIndex);
            MemoryAllocatorExt.ValidateConsistency(state->allocator);

            return res;

        }
        
        [INLINE(256)]
        public byte* Read(State* state, uint entityId, ushort entityGen, out bool exists) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            exists = true;
            ref var gen = ref page.generations[in state->allocator, entityId];
            if (gen != entityGen) {
                exists = false;
                JobUtils.Unlock(ref page.lockIndex);
                return (byte*)0;
            }

            var globalHeadIndex = _index(page.entToDataIdx[in state->allocator, entityId], pageIndex);
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            var ptr =  MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            JobUtils.Unlock(ref page.lockIndex);
            return ptr;

        }

        [INLINE(256)]
        public byte* Read(State* state, uint entityId) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            var globalHeadIndex = _index(page.entToDataIdx[in state->allocator, entityId], pageIndex);
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            var ptr =  MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            JobUtils.Unlock(ref page.lockIndex);
            return ptr;

        }

        [INLINE(256)]
        public byte* Get(State* state, uint entityId, ushort entityGen, out bool isNew) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            isNew = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            ref var headIndex = ref page.entToDataIdx[state, entityId];
            if (gen != entityGen) {
                isNew = true;
                if (headIndex == 0u) headIndex = ++page.headIndex;
                page.dataIdxToEnt[state, headIndex - 1u] = entityId;
                gen = entityGen;
            }
            JobUtils.Unlock(ref page.lockIndex);
            
            var globalHeadIndex = _index(headIndex, pageIndex);
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            JobUtils.Lock(ref dataPage.lockIndex);
            byte* ptr = MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            if (isNew == true) {
                _memclear(ptr, this.dataSize);
            }
            JobUtils.Unlock(ref dataPage.lockIndex);

            MemoryAllocatorExt.ValidateConsistency(state->allocator);

            return ptr;
            
        }

        [INLINE(256)]
        public bool Has(State* state, uint entityId, ushort entityGen) {

            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out _);
            return page.generations[in state->allocator, entityId] == entityGen;
            
        }

    }

    public unsafe partial struct SparseSetUnknownTypeTag {

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.indexPages.BurstMode(in allocator, state);
            for (uint i = 0; i < this.indexPages.Length; ++i) {
                ref var pageIndex = ref this.indexPages[in allocator, i];
                pageIndex.BurstMode(in allocator, state);
            }
        }

        [INLINE(256)]
        private static uint _size(uint capacity) {
            return (capacity - 1u) / ENTITIES_PER_PAGE + 1u;
        }

        [INLINE(256)]
        private static ref IndexPage _pageIndex(State* state, in MemArray<IndexPage> pages, ref uint entityId, out uint pageIndex) {

            pageIndex = entityId / ENTITIES_PER_PAGE;
            ref var page = ref pages[state, pageIndex];
            if (page.generations.isCreated == false) {
                JobUtils.Lock(ref page.lockIndex);
                if (page.generations.isCreated == false) {
                    page.entToDataIdx = new MemArray<uint>(ref state->allocator, ENTITIES_PER_PAGE);
                    page.dataIdxToEnt = new MemArray<uint>(ref state->allocator, ENTITIES_PER_PAGE);
                    page.generations = new MemArray<ushort>(ref state->allocator, ENTITIES_PER_PAGE);
                }
                JobUtils.Unlock(ref page.lockIndex);
            }
            entityId %= ENTITIES_PER_PAGE;
            return ref page;

        }

    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe partial struct SparseSetUnknownTypeTag {

        internal const uint ENTITIES_PER_PAGE = 64u;
        private MemArray<IndexPage> indexPages;
        private uint version;

        [INLINE(256)]
        public SparseSetUnknownTypeTag(State* state, uint capacity, uint entitiesCapacity) {

            this.indexPages = new MemArray<IndexPage>(ref state->allocator, _size(entitiesCapacity), growFactor: 2);
            this.version = state->allocator.version;
            MemoryAllocatorExt.ValidateConsistency(state->allocator);

        }

        public uint GetReservedSizeInBytes(State* state) {

            var size = 0u;
            for (int i = 0; i < this.indexPages.Length; ++i) {
                size += this.indexPages[state, i].GetReservedSizeInBytes(state);
            }
            return size;

        }

        [INLINE(256)]
        private void Resize(State* state, uint newLength) {

            {
                var length = _size(newLength);
                if (length >= this.indexPages.Length) {
                    this.indexPages.Resize(ref state->allocator, length);
                }
            }

            this.version = state->allocator.version;
            
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            
        }

        [INLINE(256)]
        public void OnEntityAdd(State* state, uint entityId) {
            this.Resize(state, entityId + 1u);
        }

        [INLINE(256)]
        public void Set(State* state, uint entityId, ushort entityGen, out bool isNew) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            
            isNew = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            ref var headIndex = ref page.entToDataIdx[state, entityId];
            if (gen != entityGen) {
                isNew = true;
                if (headIndex == 0u) headIndex = ++page.headIndex;
                page.dataIdxToEnt[state, headIndex - 1u] = entityId;
                gen = entityGen;
            }
            
            JobUtils.Unlock(ref page.lockIndex);

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            
        }

        [INLINE(256)]
        public bool Remove(State* state, uint entityId, ushort entityGen) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            var res = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            if (gen == entityGen) {

                E.RANGE(page.headIndex, 1u, ENTITIES_PER_PAGE + 1u);
                var targetIdx = page.entToDataIdx[state, entityId];
                E.RANGE(targetIdx, 1u, ENTITIES_PER_PAGE + 1u);
                if (targetIdx - 1u < page.headIndex - 1u) {
                    var moveEntId = page.dataIdxToEnt[state, page.headIndex - 1u];
                    page.entToDataIdx[state, moveEntId] = targetIdx;
                    page.dataIdxToEnt[state, targetIdx - 1u] = moveEntId;
                }

                page.entToDataIdx[state, entityId] = 0u;
                page.generations[in state->allocator, entityId] = 0;
                --page.headIndex;
                res = true;
                --gen;

            }
            JobUtils.Unlock(ref page.lockIndex);
            MemoryAllocatorExt.ValidateConsistency(state->allocator);

            return res;

        }
        
        [INLINE(256)]
        public byte* Read(State* state, uint entityId, ushort entityGen, out bool exists) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            exists = true;
            ref var gen = ref page.generations[in state->allocator, entityId];
            if (gen != entityGen) {
                exists = false;
                JobUtils.Unlock(ref page.lockIndex);
                return (byte*)0;
            }
            return (byte*)0;

        }

        [INLINE(256)]
        public byte* Get(State* state, uint entityId, ushort entityGen, out bool isNew) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            JobUtils.Lock(ref page.lockIndex);
            
            isNew = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            ref var headIndex = ref page.entToDataIdx[state, entityId];
            if (gen != entityGen) {
                isNew = true;
                if (headIndex == 0u) headIndex = ++page.headIndex;
                page.dataIdxToEnt[state, headIndex - 1u] = entityId;
                gen = entityGen;
            }
            
            JobUtils.Unlock(ref page.lockIndex);

            return (byte*)0;
            
        }

        [INLINE(256)]
        public bool Has(State* state, uint entityId, ushort entityGen) {

            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out _);
            return page.generations[in state->allocator, entityId] == entityGen;
            
        }

    }

}
