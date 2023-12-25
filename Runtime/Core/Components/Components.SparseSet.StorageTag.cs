namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using static Cuts;

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
            MemoryAllocatorExt.ValidateConsistency(state->allocator);

            return (byte*)0;
            
        }

        [INLINE(256)]
        public bool Has(State* state, uint entityId, ushort entityGen) {

            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var page = ref _pageIndex(state, in this.indexPages, ref entityId, out _);
            return page.generations[in state->allocator, entityId] == entityGen;
            
        }

    }

}