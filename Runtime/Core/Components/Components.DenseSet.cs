namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using static Cuts;
    using Unity.Mathematics;
    using Unity.Collections.LowLevel.Unsafe;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DataDenseSet {

        [StructLayout(LayoutKind.Sequential)]
        public struct Page {

            // [ushort-gen][byte-state][byte-align][data]
            public MemPtr entIdToData;
            public LockSpinner lockSpinner;
            public byte isCreated;
            public bool IsCreated => this.isCreated == 1;

            [INLINE(256)]
            public void Lock() {
                this.lockSpinner.Lock();
            }

            [INLINE(256)]
            public void Unlock() {
                this.lockSpinner.Unlock();
            }

            [INLINE(256)]
            public static void Create(ref Page page, State* state, uint dataSize, uint length) {
                var blockSize = _blockSize(dataSize);
                page = new Page() {
                    lockSpinner = page.lockSpinner,
                };
                page.entIdToData = state->allocator.AllocArray(length, blockSize);
                state->allocator.MemClear(page.entIdToData, 0L, length * blockSize);
                page.isCreated = 1;
            }

            public uint GetReservedSizeInBytes(uint dataSize, uint entitiesPerPage) {
                if (this.IsCreated == false) return TSize<Page>.size;
                var blockSize = _blockSize(dataSize);
                return blockSize * entitiesPerPage + TSize<Page>.size;
            }

        }

        private const uint ENTITIES_PER_PAGE = 64u;

        private ReadWriteSpinner readWriteSpinner;
        private readonly uint dataSize;
        private MemArray<Page> dataPages;

        [INLINE(256)]
        private static uint _sizeData(uint capacity) {
            return (uint)math.ceil(capacity / (float)ENTITIES_PER_PAGE);
        }

        [INLINE(256)]
        private static uint _pageIndex(uint entityId) {
            return entityId / ENTITIES_PER_PAGE;
        }

        [INLINE(256)]
        private static uint _dataIndex(uint entityId) {
            return entityId % ENTITIES_PER_PAGE;
        }

        [INLINE(256)]
        private static uint _headerSize() {
            return TSize<ushort>.size + TSize<byte>.size + TSize<byte>.size;
        }

        [INLINE(256)]
        private static byte* _offsetData(byte* block) {
            return block + _headerSize();
        }

        [INLINE(256)]
        private static byte* _offsetState(byte* block) {
            return block + TSize<ushort>.size;
        }

        [INLINE(256)]
        private static ushort* _offsetGen(byte* block) {
            return (ushort*)block;
        }

        [INLINE(256)]
        private static byte* _getBlock(State* state, in Page page, uint entityId, uint dataSize) {
            var dataIndex = _dataIndex(entityId);
            return state->allocator.GetUnsafePtr(in page.entIdToData, _blockSize(dataSize) * dataIndex);
        }

        [INLINE(256)]
        private static uint _blockSize(uint dataSize) {
            return _headerSize() + dataSize;
        }

        [INLINE(256)]
        public DataDenseSet(State* state, uint dataSize, uint entitiesCapacity) {
            this.dataSize = dataSize;
            this.dataPages = new MemArray<Page>(ref state->allocator, _sizeData(entitiesCapacity));
            this.readWriteSpinner = ReadWriteSpinner.Create(state);
            MemoryAllocator.ValidateConsistency(ref state->allocator);
        }
        
        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.dataPages.BurstMode(in allocator, state);
        }
        
        public uint GetReservedSizeInBytes(State* state) {
            var size = 0u;
            for (int i = 0; i < this.dataPages.Length; ++i) {
                size += this.dataPages[state, i].GetReservedSizeInBytes(this.dataSize, ENTITIES_PER_PAGE);
            }
            return size;
        }

        [INLINE(256)]
        private void Resize(State* state, uint entitiesCapacity) {
            var newSize = _sizeData(entitiesCapacity);
            if (newSize > this.dataPages.Length) {
                this.readWriteSpinner.WriteBegin(state);
                if (newSize > this.dataPages.Length) {
                    this.dataPages.Resize(ref state->allocator, newSize, 2);
                }
                this.readWriteSpinner.WriteEnd();
            }
        }
        
        [INLINE(256)]
        public void OnEntityAdd(State* state, uint entityId) {
            this.Resize(state, entityId + 1u);
        }

        [INLINE(256)]
        public bool SetState(State* state, uint entityId, ushort entityGen, bool value) {
            var changed = false;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            ref var page = ref this.dataPages[state, pageIndex];
            var val = _offsetState(_getBlock(state, in page, entityId, this.dataSize));
            if (value == true && *val == 1) {
                // if we want to enable component and it was disabled
                changed = true;
                *val = 0;
            } else if (value == false && *val == 0) {
                // if we want to disable component and it was enabled
                changed = true;
                *val = 1;
            }
            this.readWriteSpinner.ReadEnd(state);
            return changed;
        }

        [INLINE(256)]
        public bool ReadState(State* state, uint entityId, ushort entityGen) {
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            ref var page = ref this.dataPages[state, pageIndex];
            var val = _offsetState(_getBlock(state, in page, entityId, this.dataSize));
            var res = *val == 0 ? true : false;
            this.readWriteSpinner.ReadEnd(state);
            return res;
        }

        [INLINE(256)]
        public bool Set(State* state, uint entityId, ushort entityGen, void* data, out bool changed) {
            changed = false;
            var isNew = false;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            ref var page = ref this.dataPages[state, pageIndex];
            { // create page if not exist
                if (page.IsCreated == false) {
                    page.Lock();
                    if (page.IsCreated == false) {
                        Page.Create(ref page, state, this.dataSize, ENTITIES_PER_PAGE);
                    }
                    page.Unlock();
                }
            }
            var ptr = _getBlock(state, in page, entityId, this.dataSize);
            if (this.dataSize > 0u) { // set data
                if (data == null) {
                    changed = true;
                    _memclear(_offsetData(ptr), this.dataSize);
                } else {
                    changed = true;//_memcmp(data, ptr, this.dataSize) != 0;
                    _memcpy(data, _offsetData(ptr), this.dataSize);
                }
            }
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen != entityGen) {
                    page.Lock();
                    if (*gen != entityGen) {
                        *gen = entityGen;
                        changed = true;
                        isNew = true;
                    }
                    page.Unlock();
                }
            }
            this.readWriteSpinner.ReadEnd(state);

            return isNew;

        }

        [INLINE(256)]
        public byte* Get(State* state, uint entityId, ushort entityGen, bool isReadonly, out bool isNew) {
            isNew = false;
            if (this.dataSize == 0u) return null;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            ref var page = ref this.dataPages[state, pageIndex];
            { // create page if not exist
                if (page.IsCreated == false) {
                    if (isReadonly == true) {
                        this.readWriteSpinner.ReadEnd(state);
                        return null;
                    }
                    page.Lock();
                    if (page.IsCreated == false) {
                        Page.Create(ref page, state, this.dataSize, ENTITIES_PER_PAGE);
                    }
                    page.Unlock();
                }
            }
            var ptr = _getBlock(state, in page, entityId, this.dataSize);
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen != entityGen) {
                    if (isReadonly == true) {
                        this.readWriteSpinner.ReadEnd(state);
                        return null;
                    }
                    page.Lock();
                    if (*gen != entityGen) {
                        *gen = entityGen;
                        isNew = true;
                    }
                    page.Unlock();
                }
            }
            var dataPtr = _offsetData(ptr);
            if (isReadonly == false && isNew == true) { // clear data if not exist
                _memclear(dataPtr, this.dataSize);
            }
            this.readWriteSpinner.ReadEnd(state);

            return dataPtr;
        }

        [INLINE(256)]
        public bool Remove(State* state, uint entityId, ushort entityGen) {
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            ref var page = ref this.dataPages[state, pageIndex];
            if (page.IsCreated == false) {
                this.readWriteSpinner.ReadEnd(state);
                return false;
            }
            
            var ptr = _getBlock(state, in page, entityId, this.dataSize);
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen == entityGen) {
                    var hasRemoved = false;
                    page.Lock();
                    if (*gen == entityGen) {
                        *gen = 0;
                        hasRemoved = true;
                    }
                    page.Unlock();
                    this.readWriteSpinner.ReadEnd(state);
                    return hasRemoved;
                }
            }
            this.readWriteSpinner.ReadEnd(state);
            return false;
        }

        [INLINE(256)]
        public bool Has(State* state, uint entityId, ushort entityGen, bool checkEnabled) {
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = this.dataPages[state, pageIndex];
            if (page.IsCreated == false) {
                this.readWriteSpinner.ReadEnd(state);
                return false;
            }
            var ptr = _getBlock(state, in page, entityId, this.dataSize);
            var gen = *_offsetGen(ptr);
            var disableState = checkEnabled == true ? *_offsetState(ptr) : 0;
            this.readWriteSpinner.ReadEnd(state);
            return gen == entityGen && disableState == 0;
        }

    }

}