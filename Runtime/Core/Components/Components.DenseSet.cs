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

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DataDenseSet {

        [StructLayout(LayoutKind.Sequential)]
        public struct Page {

            // [ushort-gen][byte-state][byte-align][data]
            public MemPtr entIdToData;
            public LockSpinner lockSpinner;
            public volatile byte isCreated;
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
            public static void Create(ref Page page, safe_ptr<State> state, uint dataSize, uint length) {
                var blockSize = _blockSize(dataSize);
                page = new Page() {
                    lockSpinner = page.lockSpinner,
                };
                page.entIdToData = state.ptr->allocator.AllocArray(length, blockSize);
                state.ptr->allocator.MemClear(page.entIdToData, 0L, length * blockSize);
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
        private static safe_ptr _offsetData(safe_ptr<byte> block) {
            return block + _headerSize();
        }

        [INLINE(256)]
        private static safe_ptr _offsetState(safe_ptr<byte> block) {
            return block + TSize<ushort>.size;
        }

        [INLINE(256)]
        private static safe_ptr<ushort> _offsetGen(safe_ptr<byte> block) {
            return block.Cast<ushort>();
        }

        [INLINE(256)]
        private static safe_ptr _getBlock(safe_ptr<State> state, in Page page, uint entityId, uint dataSize) {
            var dataIndex = _dataIndex(entityId);
            return state.ptr->allocator.GetUnsafePtr(in page.entIdToData, _blockSize(dataSize) * dataIndex);
        }

        [INLINE(256)]
        private static uint _blockSize(uint dataSize) {
            return _headerSize() + dataSize;
        }

        [INLINE(256)]
        public DataDenseSet(safe_ptr<State> state, uint dataSize, uint entitiesCapacity) {
            this.dataSize = dataSize;
            this.dataPages = new MemArray<Page>(ref state.ptr->allocator, _sizeData(entitiesCapacity));
            this.readWriteSpinner = ReadWriteSpinner.Create(state);
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
                    this.dataPages.Resize(ref state.ptr->allocator, newSize, 2);
                }
                this.readWriteSpinner.WriteEnd();
            }
        }
        
        [INLINE(256)]
        public void OnEntityAdd(safe_ptr<State> state, uint entityId) {
            this.Resize(state, entityId + 1u);
        }

        [INLINE(256)]
        public bool SetState(safe_ptr<State> state, uint entityId, ushort entityGen, bool value) {
            var changed = false;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            ref var page = ref this.dataPages[state, pageIndex];
            var val = _offsetState(_getBlock(state, in page, entityId, this.dataSize));
            if ((value == true && *val.ptr == 1) || (value == false && *val.ptr == 0)) {
                page.Lock();
                if (value == true && *val.ptr == 1) {
                    // if we want to enable component and it was disabled
                    changed = true;
                    *val.ptr = 0;
                } else if (value == false && *val.ptr == 0) {
                    // if we want to disable component and it was enabled
                    changed = true;
                    *val.ptr = 1;
                }
                page.Unlock();
            }
            this.readWriteSpinner.ReadEnd(state);
            return changed;
        }

        [INLINE(256)]
        public bool ReadState(safe_ptr<State> state, uint entityId, ushort entityGen) {
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            ref var page = ref this.dataPages[state, pageIndex];
            var val = _offsetState(_getBlock(state, in page, entityId, this.dataSize));
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
            ref var page = ref this.dataPages[state, pageIndex];
            { // create page if not exist
                if (page.isCreated == 0) {
                    page.Lock();
                    if (page.isCreated == 0) {
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
                    _memcpy((safe_ptr)data, _offsetData(ptr), this.dataSize);
                }
            }
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen.ptr != entityGen) {
                    page.Lock();
                    if (*gen.ptr != entityGen) {
                        *gen.ptr = entityGen;
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
        public byte* Get(safe_ptr<State> state, uint entityId, ushort entityGen, bool isReadonly, out bool isNew, safe_ptr defaultValue) {
            isNew = false;
            if (this.dataSize == 0u) return null;
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            ref var page = ref this.dataPages[state, pageIndex];
            { // create page if not exist
                if (page.isCreated == 0) {
                    if (isReadonly == true) {
                        this.readWriteSpinner.ReadEnd(state);
                        return null;
                    }
                    page.Lock();
                    if (page.isCreated == 0) {
                        Page.Create(ref page, state, this.dataSize, ENTITIES_PER_PAGE);
                    }
                    page.Unlock();
                }
            }
            var ptr = _getBlock(state, in page, entityId, this.dataSize);
            var dataPtr = _offsetData(ptr);
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen.ptr != entityGen) {
                    if (isReadonly == true) {
                        this.readWriteSpinner.ReadEnd(state);
                        return null;
                    }
                    page.Lock();
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
                    page.Unlock();
                }
            }
            this.readWriteSpinner.ReadEnd(state);

            return dataPtr.ptr;
        }

        [INLINE(256)]
        public bool Remove(safe_ptr<State> state, uint entityId, ushort entityGen) {
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
                if (*gen.ptr == entityGen) {
                    var hasRemoved = false;
                    page.Lock();
                    if (*gen.ptr == entityGen) {
                        *gen.ptr = 0;
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
        public bool Has(safe_ptr<State> state, uint entityId, ushort entityGen, bool checkEnabled) {
            var pageIndex = _pageIndex(entityId);
            this.readWriteSpinner.ReadBegin(state);
            var page = this.dataPages[state, pageIndex];
            if (page.IsCreated == false) {
                this.readWriteSpinner.ReadEnd(state);
                return false;
            }
            var ptr = _getBlock(state, in page, entityId, this.dataSize);
            var gen = *_offsetGen(ptr).ptr;
            var disableState = checkEnabled == true ? *_offsetState(ptr).ptr : (byte)0;
            this.readWriteSpinner.ReadEnd(state);
            return gen == entityGen && disableState == 0;
        }

    }

}