#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [IgnoreProfiler]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DataDenseSet {

        [IgnoreProfiler]
        [StructLayout(LayoutKind.Sequential)]
        public struct Page {

            public const int SIZE = MemPtr.SIZE + LockSpinner.SIZE + sizeof(byte);

            // [ushort-gen][byte-state][byte-align][data]
            public MemPtr entIdToData;
            public LockSpinner lockSpinner;
            public volatile byte isCreated;
            
            [INLINE(256)]
            public void Lock() {
                this.lockSpinner.Lock();
            }

            [INLINE(256)]
            public void Unlock() {
                this.lockSpinner.Unlock();
            }

            [INLINE(256)]
            public static void Create(safe_ptr<Page> page, safe_ptr<State> state, uint dataSize, uint length) {
                var blockSize = _blockSize(dataSize);
                *page.ptr = new Page() {
                    lockSpinner = page.ptr->lockSpinner,
                };
                page.ptr->entIdToData = state.ptr->allocator.AllocArray(length, blockSize);
                state.ptr->allocator.MemClear(page.ptr->entIdToData, 0L, length * blockSize);
                page.ptr->isCreated = 1;
            }

            public uint GetReservedSizeInBytes(uint dataSize, uint entitiesPerPage) {
                if (this.isCreated == 0) return SIZE;
                var blockSize = _blockSize(dataSize);
                return blockSize * entitiesPerPage + SIZE;
            }

        }

        internal const uint ENTITIES_PER_PAGE = 64u;
        internal const uint ENTITIES_PER_PAGE_MASK = ENTITIES_PER_PAGE - 1u;
        private const int ENTITIES_PER_PAGE_POW = 6;

        private ReadWriteSpinner readWriteSpinner;
        private readonly uint dataSize;
        private MemArray<Page> dataPages;
        #if ENABLE_BECS_FLAT_QUERIES
        internal BitArray bits;
        #endif

        [INLINE(256)]
        private static uint _sizeData(uint capacity) {
            return (capacity + ENTITIES_PER_PAGE_MASK) / ENTITIES_PER_PAGE;
        }

        [INLINE(256)]
        private static uint _pageIndex(uint entityId) {
            return entityId >> ENTITIES_PER_PAGE_POW;
        }

        [INLINE(256)]
        private static uint _dataIndex(uint entityId) {
            return entityId & ENTITIES_PER_PAGE_MASK;
        }

        [INLINE(256)]
        private static uint _headerSize() {
            return TSize_ushort.size + TSize_byte.size + TSize_byte.size;
        }

        [INLINE(256)]
        private static safe_ptr _offsetData(safe_ptr<byte> block) {
            return block + _headerSize();
        }

        [INLINE(256)]
        private static safe_ptr _offsetState(safe_ptr<byte> block) {
            return block + TSize_ushort.size;
        }

        [INLINE(256)]
        private static safe_ptr<ushort> _offsetGen(safe_ptr<byte> block) {
            return block.Cast<ushort>();
        }

        [INLINE(256)]
        private static safe_ptr _getBlock(safe_ptr<State> state, safe_ptr<Page> page, uint entityId, uint dataSize) {
            var dataIndex = _dataIndex(entityId);
            return state.ptr->allocator.GetUnsafePtr(in page.ptr->entIdToData, _blockSize(dataSize) * dataIndex);
        }

        [INLINE(256)]
        private static uint _blockSize(uint dataSize) {
            return _headerSize() + dataSize;
        }

        [INLINE(256)]
        public DataDenseSet(safe_ptr<State> state, uint dataSize, uint entitiesCapacity) {
            var pages = _sizeData(entitiesCapacity);
            this.dataSize = dataSize;
            this.dataPages = new MemArray<Page>(ref state.ptr->allocator, pages);
            this.readWriteSpinner = ReadWriteSpinner.Create(state);
            #if ENABLE_BECS_FLAT_QUERIES
            this.bits = new BitArray(ref state.ptr->allocator, pages * ENTITIES_PER_PAGE, ClearOptions.ClearMemory, true);
            #endif
            MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
        }
        
        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.dataPages.BurstMode(in allocator, state);
        }
        
        public uint GetReservedSizeInBytes(safe_ptr<State> state) {
            var size = 0u;
            for (int i = 0; i < this.dataPages.Length; ++i) {
                size += this.dataPages[state, i].GetReservedSizeInBytes(this.dataSize, ENTITIES_PER_PAGE);
            }
            return size;
        }

        [INLINE(256)]
        private void Resize(safe_ptr<State> state, uint entitiesCapacity) {
            var newSize = _sizeData(entitiesCapacity);
            if (newSize > this.dataPages.Length) {
                this.readWriteSpinner.WriteBegin(state);
                if (newSize > this.dataPages.Length) {
                    #if ENABLE_BECS_FLAT_QUERIES
                    this.bits.Resize(ref state.ptr->allocator, newSize * ENTITIES_PER_PAGE, growFactor: 2);
                    #endif
                    this.dataPages.Resize(ref state.ptr->allocator, newSize, 2);
                }
                this.readWriteSpinner.WriteEnd();
            }
        }
        
        [INLINE(256)]
        public void OnEntityAdd(safe_ptr<State> state, uint entityId) {
            this.Resize(state, entityId + 1u);
        }

        #if ENABLE_BECS_FLAT_QUERIES
        [INLINE(256)]
        public void CleanUpEntity(safe_ptr<State> state, uint entityId, uint typeId) {
            this.readWriteSpinner.ReadBegin(state);
            this.bits.SetThreaded(state.ptr->allocator, entityId, false);
            this.readWriteSpinner.ReadEnd(state);
        }
        #endif

        [INLINE(256)]
        public bool SetState(safe_ptr<State> state, uint entityId, ushort entityGen, bool value) {
            var changed = false;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = (safe_ptr<Page>)this.dataPages.GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var val = _offsetState(_getBlock(state, page, entityId, this.dataSize));
            if ((value == true && *val.ptr == 1) || (value == false && *val.ptr == 0)) {
                page.ptr->Lock();
                if (value == true && *val.ptr == 1) {
                    // if we want to enable component and it was disabled
                    #if ENABLE_BECS_FLAT_QUERIES
                    this.bits.SetThreaded(state.ptr->allocator, entityId, true);
                    #endif
                    changed = true;
                    *val.ptr = 0;
                } else if (value == false && *val.ptr == 0) {
                    // if we want to disable component and it was enabled
                    #if ENABLE_BECS_FLAT_QUERIES
                    this.bits.SetThreaded(state.ptr->allocator, entityId, false);
                    #endif
                    changed = true;
                    *val.ptr = 1;
                }
                page.ptr->Unlock();
            }
            this.readWriteSpinner.ReadEnd(state);
            return changed;
        }

        [INLINE(256)]
        public bool ReadState(safe_ptr<State> state, uint entityId, ushort entityGen) {
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = (safe_ptr<Page>)this.dataPages.GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var val = _offsetState(_getBlock(state, page, entityId, this.dataSize));
            var res = *val.ptr == 0 ? true : false;
            this.readWriteSpinner.ReadEnd(state);
            return res;
        }

        [INLINE(256)]
        public bool Set(safe_ptr<State> state, uint entityId, ushort entityGen, void* data, out bool changed) {
            changed = false;
            var isNew = false;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = (safe_ptr<Page>)this.dataPages.GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            { // create page if not exist
                if (page.ptr->isCreated == 0) {
                    page.ptr->Lock();
                    if (page.ptr->isCreated == 0) {
                        Page.Create(page, state, this.dataSize, ENTITIES_PER_PAGE);
                    }
                    page.ptr->Unlock();
                }
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            if (this.dataSize > 0u) { // set data
                if (data == null) {
                    changed = true;
                    _memclear(_offsetData(ptr), this.dataSize);
                } else {
                    changed = true;//_memcmp(data, ptr, this.dataSize) != 0;
                    _memcpy((safe_ptr)data, _offsetData(ptr), this.dataSize);
                }
            }
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen.ptr != entityGen) {
                    page.ptr->Lock();
                    if (*gen.ptr != entityGen) {
                        *gen.ptr = entityGen;
                        changed = true;
                        isNew = true;
                    }
                    page.ptr->Unlock();
                }
            }
            this.readWriteSpinner.ReadEnd(state);

            return isNew;

        }

        [INLINE(256)]
        public byte* Get(safe_ptr<State> state, uint entityId, ushort entityGen, out bool isNew, safe_ptr defaultValue) {
            isNew = false;
            if (this.dataSize == 0u) return null;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = (safe_ptr<Page>)this.dataPages.GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            { // create page if not exist
                if (page.ptr->isCreated == 0) {
                    page.ptr->Lock();
                    if (page.ptr->isCreated == 0) {
                        Page.Create(page, state, this.dataSize, ENTITIES_PER_PAGE);
                    }
                    page.ptr->Unlock();
                }
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            var dataPtr = _offsetData(ptr);
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen.ptr != entityGen) {
                    page.ptr->Lock();
                    if (*gen.ptr != entityGen) {
                        *gen.ptr = entityGen;
                        if (this.dataSize > 0u) {
                            if (defaultValue.ptr != null) {
                                _memcpy(defaultValue, dataPtr, this.dataSize);
                            } else {
                                _memclear(dataPtr, this.dataSize);
                            }
                        }
                        isNew = true;
                    }
                    page.ptr->Unlock();
                }
            }
            this.readWriteSpinner.ReadEnd(state);

            return dataPtr.ptr;
        }

        [INLINE(256)]
        public byte* Read(safe_ptr<State> state, uint entityId, ushort entityGen, out bool isNew) {
            isNew = false;
            if (this.dataSize == 0u) return null;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = (safe_ptr<Page>)this.dataPages.GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            { // create page if not exist
                if (page.ptr->isCreated == 0) {
                    this.readWriteSpinner.ReadEnd(state);
                    return null;
                }
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            var dataPtr = _offsetData(ptr);
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen.ptr != entityGen) {
                    this.readWriteSpinner.ReadEnd(state);
                    return null;
                }
            }
            this.readWriteSpinner.ReadEnd(state);

            return dataPtr.ptr;
        }

        [INLINE(256)]
        public bool Remove(safe_ptr<State> state, uint entityId, ushort entityGen) {
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = (safe_ptr<Page>)this.dataPages.GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            if (page.ptr->isCreated == 0) {
                this.readWriteSpinner.ReadEnd(state);
                return false;
            }
            
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen.ptr == entityGen) {
                    var hasRemoved = false;
                    page.ptr->Lock();
                    if (*gen.ptr == entityGen) {
                        *gen.ptr = 0;
                        hasRemoved = true;
                    }
                    page.ptr->Unlock();
                    this.readWriteSpinner.ReadEnd(state);
                    return hasRemoved;
                }
            }
            this.readWriteSpinner.ReadEnd(state);
            return false;
        }

        [INLINE(256)]
        public bool Has(safe_ptr<State> state, uint entityId, ushort entityGen, bool checkEnabled) {
            var pageIndex = _pageIndex(entityId);
            if (pageIndex >= this.dataPages.Length) {
                return false;
            }
            this.readWriteSpinner.ReadBegin(state);
            var page = (safe_ptr<Page>)this.dataPages.GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            if (page.ptr->isCreated == 0) {
                this.readWriteSpinner.ReadEnd(state);
                return false;
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            var gen = *_offsetGen(ptr).ptr;
            var disableState = checkEnabled == true ? *_offsetState(ptr).ptr : (byte)0;
            this.readWriteSpinner.ReadEnd(state);
            return gen == entityGen && disableState == 0;
        }

        #if ENABLE_BECS_FLAT_QUERIES
        [INLINE(256)]
        public void SetBit(safe_ptr<State> state, uint entityId, bool value, uint typeId) {
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = (safe_ptr<Page>)this.dataPages.GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            page.ptr->Lock();
            this.bits.SetThreaded(state.ptr->allocator, entityId, value);
            page.ptr->Unlock();
            this.readWriteSpinner.ReadEnd(state);
        }
        #endif

    }

}