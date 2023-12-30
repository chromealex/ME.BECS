namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using static Cuts;

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
        private static ref IndexPage _pageIndex(State* state, in MemArray<IndexPage> pages, ref uint entityId, out uint pageIndex, bool isReadonly = false) {

            pageIndex = entityId / ENTITIES_PER_PAGE;
            ref var page = ref pages[state, pageIndex];
            if (page.generations.isCreated == false) {
                if (isReadonly == true) return ref page;
                JobUtils.Lock(ref page.lockIndex);
                if (page.generations.isCreated == false) {
                    page.entToDataIdx = new MemArray<uint>(ref state->allocator, ENTITIES_PER_PAGE);
                    page.dataIdxToEnt = new MemArray<uint>(ref state->allocator, ENTITIES_PER_PAGE);
                    page.states = new MemArray<byte>(ref state->allocator, ENTITIES_PER_PAGE);
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
        private MemArray<DataPage> dataPages;
        private readonly uint dataSize;
        private readonly uint dataPerPage;
        
        private ReadWriteSpinner spinnerIndexPage;
        private ReadWriteSpinner spinnerDataPage;

        [INLINE(256)]
        public SparseSetUnknownType(State* state, uint dataSize, uint capacity, uint entitiesCapacity) {

            this.dataSize = dataSize;
            if (this.dataSize == 0u) dataSize = 1u;
            this.dataPerPage = PAGE_SIZE_BYTES_PER_PAGE / dataSize;
            this.indexPages = new MemArray<IndexPage>(ref state->allocator, _size(entitiesCapacity), growFactor: 2);
            this.dataPages = new MemArray<DataPage>(ref state->allocator, _sizeData(entitiesCapacity, this.dataPerPage), growFactor: 2);
            this.spinnerIndexPage = default;
            this.spinnerDataPage = default;
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
                    this.spinnerIndexPage.WhiteBegin();
                    this.indexPages.Resize(ref state->allocator, length);
                    this.spinnerIndexPage.WhiteEnd();
                }
            }

            {
                var length = _sizeData(newLength, this.dataPerPage);
                if (length >= this.dataPages.Length) {
                    this.spinnerDataPage.WhiteBegin();
                    this.dataPages.Resize(ref state->allocator, length);
                    this.spinnerDataPage.WhiteEnd();
                }
            }

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            
        }

        [INLINE(256)]
        public void OnEntityAdd(State* state, uint entityId) {
            this.Resize(state, entityId + 1u);
        }

        [INLINE(256)]
        public void Set(State* state, uint entityId, ushort entityGen, void* data, out bool isNew) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            this.spinnerIndexPage.ReadBegin();
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            this.spinnerIndexPage.ReadEnd();
            isNew = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            ref var headIndex = ref page.entToDataIdx[state, entityId];
            if (gen != entityGen) {
                JobUtils.Lock(ref page.lockIndex);
                isNew = true;
                if (headIndex == 0u) headIndex = ++page.headIndex;
                page.dataIdxToEnt[state, headIndex - 1u] = entityId;
                gen = entityGen;
                JobUtils.Unlock(ref page.lockIndex);
            }
            
            var globalHeadIndex = _index(headIndex, pageIndex);
            this.spinnerDataPage.ReadBegin();
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            this.spinnerDataPage.ReadEnd();
            byte* ptr = MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            if (data == null) {
                _memclear(ptr, this.dataSize);
            } else {
                _memcpy(data, ptr, this.dataSize);
            }
            
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            
        }

        [INLINE(256)]
        public void Set<T>(State* state, uint entityId, ushort entityGen, in T data, out bool isNew) where T : unmanaged {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            this.spinnerIndexPage.ReadBegin();
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            this.spinnerIndexPage.ReadEnd();
            isNew = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            ref var headIndex = ref page.entToDataIdx[state, entityId];
            if (gen != entityGen) {
                JobUtils.Lock(ref page.lockIndex);
                isNew = true;
                if (headIndex == 0u) headIndex = ++page.headIndex;
                page.dataIdxToEnt[state, headIndex - 1u] = entityId;
                gen = entityGen;
                JobUtils.Unlock(ref page.lockIndex);
            }
            
            var globalHeadIndex = _index(headIndex, pageIndex);
            this.spinnerDataPage.ReadBegin();
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            this.spinnerDataPage.ReadEnd();
            var vPtr = MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            *(T*)vPtr = data;
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            
        }

        [INLINE(256)]
        public bool Remove(State* state, uint entityId, ushort entityGen) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            this.spinnerIndexPage.ReadBegin();
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            this.spinnerIndexPage.ReadEnd();
            var res = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            if (gen == entityGen) {
                JobUtils.Lock(ref page.lockIndex);
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
                JobUtils.Unlock(ref page.lockIndex);
            }
            MemoryAllocatorExt.ValidateConsistency(state->allocator);

            return res;

        }
        
        [INLINE(256)]
        public bool SetState(State* state, uint entityId, ushort entityGen, bool value) {

            var result = false;
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            this.spinnerIndexPage.ReadBegin();
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out _);
            this.spinnerIndexPage.ReadEnd();
            ref var val = ref page.states[in state->allocator, entityId];
            if (value == true && val == 1) {
                // if we want to enable component and it was disabled
                result = true;
                val = 0;
            } else if (value == false && val == 0) {
                // if we want to disable component and it was enabled
                result = true;
                val = 1;
            }
            
            return result;

        }

        [INLINE(256)]
        public byte* Read(State* state, uint entityId, ushort entityGen, out bool exists) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            exists = true;
            this.spinnerIndexPage.ReadBegin();
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex, true);
            this.spinnerIndexPage.ReadEnd();
            if (page.IsCreated == false || page.generations[in state->allocator, entityId] != entityGen) {
                exists = false;
                return (byte*)0;
            }

            var globalHeadIndex = _index(page.entToDataIdx[in state->allocator, entityId], pageIndex);
            this.spinnerDataPage.ReadBegin();
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            this.spinnerDataPage.ReadEnd();
            var ptr =  MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            return ptr;

        }

        [INLINE(256)]
        public byte* Read(State* state, uint entityId) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            this.spinnerIndexPage.ReadBegin();
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex, true);
            this.spinnerIndexPage.ReadEnd();
            if (page.IsCreated == false) {
                return (byte*)0;
            }
            var globalHeadIndex = _index(page.entToDataIdx[in state->allocator, entityId], pageIndex);
            this.spinnerDataPage.ReadBegin();
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            this.spinnerDataPage.ReadEnd();
            var ptr =  MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            return ptr;

        }

        [INLINE(256)]
        public byte* Get(State* state, uint entityId, ushort entityGen, out bool isNew) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            this.spinnerIndexPage.ReadBegin();
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out var pageIndex);
            this.spinnerIndexPage.ReadEnd();
            isNew = false;
            ref var gen = ref page.generations[in state->allocator, entityId];
            ref var headIndex = ref page.entToDataIdx[state, entityId];
            if (gen != entityGen) {
                JobUtils.Lock(ref page.lockIndex);
                isNew = true;
                if (headIndex == 0u) headIndex = ++page.headIndex;
                page.dataIdxToEnt[state, headIndex - 1u] = entityId;
                gen = entityGen;
                JobUtils.Unlock(ref page.lockIndex);
            }
            
            var globalHeadIndex = _index(headIndex, pageIndex);
            this.spinnerDataPage.ReadBegin();
            ref var dataPage = ref _pageData(state, in this.dataPages, globalHeadIndex, this.dataSize, this.dataPerPage, out var pageDataIndex);
            this.spinnerDataPage.ReadEnd();
            byte* ptr = MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataPage.data, _dataIndex(globalHeadIndex, pageDataIndex, this.dataPerPage) * this.dataSize);
            if (isNew == true) {
                _memclear(ptr, this.dataSize);
            }

            MemoryAllocatorExt.ValidateConsistency(state->allocator);

            return ptr;
            
        }

        [INLINE(256)]
        public bool Has(State* state, uint entityId, ushort entityGen) {

            this.spinnerIndexPage.ReadBegin();
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out _, true);
            this.spinnerIndexPage.ReadEnd();
            if (page.IsCreated == false) {
                return false;
            }
            var result = page.generations[in state->allocator, entityId] == entityGen && page.states[in state->allocator, entityId] == 0;
            return result;

        }

    }

}