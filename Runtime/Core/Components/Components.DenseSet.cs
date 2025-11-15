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
            public LockSpinner lockSpinnerLocal;
            public volatile byte isCreated;
            
            [INLINE(256)]
            public void Lock(safe_ptr<State> state) {
                this.lockSpinnerLocal.Lock();
            }

            [INLINE(256)]
            public void Unlock(safe_ptr<State> state) {
                this.lockSpinnerLocal.Unlock();
            }

            [INLINE(256)]
            public static void Create(safe_ptr<Page> page, safe_ptr<State> state, uint dataSize, uint length) {
                var blockSize = _blockSize(dataSize);
                *page.ptr = new Page() {
                    lockSpinnerLocal = page.ptr->lockSpinnerLocal,
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

        private struct DoubleBuffer {

            private int active;
            private MemArray<Page> dataPagesA;
            private MemArray<Page> dataPagesB;
            #if ENABLE_BECS_FLAT_QUERIES
            private BitArray bitsA;
            private BitArray bitsB;
            #endif

            public DoubleBuffer(safe_ptr<State> state, uint pages) {
                this.dataPagesA = new MemArray<Page>(ref state.ptr->allocator, pages);
                #if ENABLE_BECS_FLAT_QUERIES
                this.bitsA = new BitArray(ref state.ptr->allocator, pages * ENTITIES_PER_PAGE, ClearOptions.ClearMemory, true);
                this.bitsB = default;
                #endif
                this.dataPagesB = default;
                this.active = 0;
            }

            [INLINE(256)]
            public static ref MemArray<Page> GetTargetPages(ref DoubleBuffer buffer) {
                return ref (buffer.active == 0 ? ref buffer.dataPagesB : ref buffer.dataPagesA);
            }

            [INLINE(256)]
            public static ref MemArray<Page> GetActivePages(ref DoubleBuffer buffer) {
                return ref (buffer.active == 0 ? ref buffer.dataPagesA : ref buffer.dataPagesB);
            }

            #if ENABLE_BECS_FLAT_QUERIES
            [INLINE(256)]
            public static ref BitArray GetTargetBits(ref DoubleBuffer buffer) {
                return ref (buffer.active == 0 ? ref buffer.bitsB : ref buffer.bitsA);
            }

            [INLINE(256)]
            public static ref BitArray GetActiveBits(ref DoubleBuffer buffer) {
                return ref (buffer.active == 0 ? ref buffer.bitsA : ref buffer.bitsB);
            }
            #endif

            [INLINE(256)]
            public static void Swap(ref DoubleBuffer buffer, safe_ptr<State> state, uint newSize) {
                ref var targetPages = ref GetTargetPages(ref buffer);
                targetPages.Resize(ref state.ptr->allocator, newSize, 2);
                targetPages.CopyFrom(ref state.ptr->allocator, in GetActivePages(ref buffer));
                #if ENABLE_BECS_FLAT_QUERIES
                ref var targetBits = ref GetTargetBits(ref buffer);
                targetBits.Resize(ref state.ptr->allocator, newSize * ENTITIES_PER_PAGE, growFactor: 2);
                targetBits.CopyFrom(ref state.ptr->allocator, in GetActiveBits(ref buffer));
                #endif
                System.Threading.Interlocked.Exchange(ref buffer.active, buffer.active == 0 ? 1 : 0);
            }

            [INLINE(256)]
            public void BurstMode(in MemoryAllocator allocator, bool state) {
                this.dataPagesA.BurstMode(in allocator, state);
                this.dataPagesB.BurstMode(in allocator, state);
                #if ENABLE_BECS_FLAT_QUERIES
                this.bitsA.BurstMode(in allocator, state);
                this.bitsB.BurstMode(in allocator, state);
                #endif
            }

            public uint GetReservedSizeInBytes(safe_ptr<State> state, uint dataSize) {
                var size = 0u;
                for (int i = 0; i < this.dataPagesA.Length; ++i) {
                    size += this.dataPagesA[state, i].GetReservedSizeInBytes(dataSize, ENTITIES_PER_PAGE);
                }
                for (int i = 0; i < this.dataPagesB.Length; ++i) {
                    size += this.dataPagesB[state, i].GetReservedSizeInBytes(dataSize, ENTITIES_PER_PAGE);
                }
                return size;
            }

            #if ENABLE_BECS_FLAT_QUERIES
            [INLINE(256)]
            public void CleanUpEntity(safe_ptr<State> state, uint entityId, uint typeId) {
                GetActiveBits(ref this).SetThreaded(state.ptr->allocator, entityId, false);
            }
            #endif

        }

        internal const uint ENTITIES_PER_PAGE = 64u;
        internal const uint ENTITIES_PER_PAGE_MASK = ENTITIES_PER_PAGE - 1u;
        private const int ENTITIES_PER_PAGE_POW = 6;

        private ReadWriteSpinner readWriteSpinner;
        private readonly uint dataSize;
        private DoubleBuffer buffer;

        [INLINE(256)]
        public DataDenseSet(safe_ptr<State> state, uint dataSize, uint entitiesCapacity) {
            var pages = _sizeData(entitiesCapacity);
            this.dataSize = dataSize;
            this.buffer = new DoubleBuffer(state, pages);
            this.readWriteSpinner = ReadWriteSpinner.Create(state);
            MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
        }

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
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.buffer.BurstMode(in allocator, state);
        }
        
        public uint GetReservedSizeInBytes(safe_ptr<State> state) {
            var size = 0u;
            size += this.buffer.GetReservedSizeInBytes(state, this.dataSize);
            return size;
        }

        [INLINE(256)]
        private void Resize(safe_ptr<State> state, uint entitiesCapacity) {
            var newSize = _sizeData(entitiesCapacity);
            ref var activePages = ref DoubleBuffer.GetActivePages(ref this.buffer);
            if (newSize > activePages.Length) {
                this.readWriteSpinner.WriteBegin(state);
                activePages = ref DoubleBuffer.GetActivePages(ref this.buffer);
                if (newSize > activePages.Length) {
                    DoubleBuffer.Swap(ref this.buffer, state, newSize);
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
            this.buffer.CleanUpEntity(state, entityId, typeId);
        }

        [INLINE(256)]
        public BitArray GetBits() {
            return DoubleBuffer.GetActiveBits(ref this.buffer);
        }
        #endif

        [INLINE(256)]
        public bool SetState(safe_ptr<State> state, uint entityId, ushort entityGen, bool value) {
            var changed = false;
            var pageIndex = _pageIndex(entityId);
            var page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var val = _offsetState(_getBlock(state, page, entityId, this.dataSize));
            if ((value == true && *val.ptr == 1) || (value == false && *val.ptr == 0)) {
                var res = (value == true && *val.ptr == 1);
                #if ENABLE_BECS_FLAT_QUERIES
                DoubleBuffer.GetActiveBits(ref this.buffer).SetThreaded(state.ptr->allocator, entityId, res);
                #endif
                changed = true;
                *val.ptr = res == true ? (byte)0 : (byte)1;
            }
            return changed;
        }

        [INLINE(256)]
        public bool ReadState(safe_ptr<State> state, uint entityId, ushort entityGen) {
            var pageIndex = _pageIndex(entityId);
            var page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var val = _offsetState(_getBlock(state, page, entityId, this.dataSize));
            var res = *val.ptr == 0 ? true : false;
            return res;
        }
        
        [INLINE(256)]
        public bool Set(safe_ptr<State> state, uint entityId, ushort entityGen, void* data, out bool changed) {
            changed = false;
            var isNew = false;
            var pageIndex = _pageIndex(entityId);
            var page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var isCreated = page.ptr->isCreated;
            if (Unity.Burst.CompilerServices.Hint.Unlikely(isCreated == 0)) {
                // create page if not exist
                this.readWriteSpinner.ReadBegin(state);
                page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
                page.ptr->Lock(state);
                if (page.ptr->isCreated == 0) {
                    Page.Create(page, state, this.dataSize, ENTITIES_PER_PAGE);
                }
                page.ptr->Unlock(state);
                this.readWriteSpinner.ReadEnd(state);
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            if (this.dataSize > 0u) { // set data
                var src = data != null ? (safe_ptr)data : StaticUtils.zero.Data;
                var dataPtr = _offsetData(ptr);
                changed = (_memcmp(src, dataPtr, this.dataSize) != 0);
                _memcpy(src, dataPtr, this.dataSize);
            }
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen.ptr != entityGen) {
                    *gen.ptr = entityGen;
                    changed = true;
                    isNew = true;
                }
            }

            return isNew;

        }

        [INLINE(256)][Unity.Burst.CompilerServices.SkipLocalsInitAttribute]
        public byte* Get(safe_ptr<State> state, uint entityId, ushort entityGen, out bool isNew, safe_ptr defaultValue) {
            isNew = false;
            if (Unity.Burst.CompilerServices.Hint.Unlikely(this.dataSize == 0u)) return null;
            var pageIndex = _pageIndex(entityId);
            var page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var isCreated = page.ptr->isCreated;
            if (Unity.Burst.CompilerServices.Hint.Unlikely(isCreated == 0)) {
                // create page if not exist
                this.readWriteSpinner.ReadBegin(state);
                page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
                page.ptr->Lock(state);
                if (page.ptr->isCreated == 0) {
                    Page.Create(page, state, this.dataSize, ENTITIES_PER_PAGE);
                }
                page.ptr->Unlock(state);
                this.readWriteSpinner.ReadEnd(state);
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            var dataPtr = _offsetData(ptr);
            // update gen
            var gen = _offsetGen(ptr);
            if (*gen.ptr != entityGen) {
                *gen.ptr = entityGen;
                var src = defaultValue.ptr != null ? defaultValue : StaticUtils.zero.Data;
                _memcpy(src, dataPtr, this.dataSize);
                isNew = true;
            }
            return dataPtr.ptr;
        }

        [INLINE(256)][Unity.Burst.CompilerServices.SkipLocalsInitAttribute]
        public byte* GetOrThrow(safe_ptr<State> state, uint entityId, ushort entityGen, out bool isNew, safe_ptr defaultValue) {
            isNew = false;
            if (Unity.Burst.CompilerServices.Hint.Unlikely(this.dataSize == 0u)) return null;
            var pageIndex = _pageIndex(entityId);
            var page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var isCreated = page.ptr->isCreated;
            if (Unity.Burst.CompilerServices.Hint.Unlikely(isCreated == 0)) {
                return null;
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            var dataPtr = _offsetData(ptr);
            // update gen
            var gen = _offsetGen(ptr);
            if (Unity.Burst.CompilerServices.Hint.Likely(*gen.ptr != entityGen)) {
                *gen.ptr = entityGen;
                var src = defaultValue.ptr != null ? defaultValue : StaticUtils.zero.Data;
                _memcpy(src, dataPtr, this.dataSize);
                isNew = true;
            }
            return dataPtr.ptr;
        }

        [INLINE(256)]
        public byte* Read(safe_ptr<State> state, uint entityId, ushort entityGen, out bool isNew) {
            isNew = false;
            if (Unity.Burst.CompilerServices.Hint.Unlikely(this.dataSize == 0u)) return null;
            var pageIndex = _pageIndex(entityId);
            if (pageIndex >= DoubleBuffer.GetActivePages(ref this.buffer).Length) {
                return null;
            }
            var page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var isCreated = page.ptr->isCreated;
            if (Unity.Burst.CompilerServices.Hint.Unlikely(isCreated == 0)) {
                return null;
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            var gen = _offsetGen(ptr);
            if (*gen.ptr != entityGen) {
                return null;
            }
            var dataPtr = _offsetData(ptr);
            return dataPtr.ptr;
        }

        [INLINE(256)]
        public bool Remove(safe_ptr<State> state, uint entityId, ushort entityGen) {
            var pageIndex = _pageIndex(entityId);
            if (pageIndex >= DoubleBuffer.GetActivePages(ref this.buffer).Length) {
                return false;
            }
            var page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            var isCreated = page.ptr->isCreated;
            if (Unity.Burst.CompilerServices.Hint.Unlikely(isCreated == 0)) {
                return false;
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            { // update gen
                var gen = _offsetGen(ptr);
                if (*gen.ptr == entityGen) {
                    *gen.ptr = 0;
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        public bool Has(safe_ptr<State> state, uint entityId, ushort entityGen, bool checkEnabled) {
            var pageIndex = _pageIndex(entityId);
            if (pageIndex >= DoubleBuffer.GetActivePages(ref this.buffer).Length) {
                return false;
            }
            var page = (safe_ptr<Page>)DoubleBuffer.GetActivePages(ref this.buffer).GetUnsafePtr(in state.ptr->allocator) + pageIndex;
            if (page.ptr->isCreated == 0) {
                return false;
            }
            var ptr = _getBlock(state, page, entityId, this.dataSize);
            var gen = *_offsetGen(ptr).ptr;
            var disableState = checkEnabled == true ? *_offsetState(ptr).ptr : (byte)0;
            return gen == entityGen && disableState == 0;
        }

        #if ENABLE_BECS_FLAT_QUERIES
        [INLINE(256)]
        public void SetBit(safe_ptr<State> state, uint entityId, bool value, uint typeId) {
            DoubleBuffer.GetActiveBits(ref this.buffer).SetThreaded(state.ptr->allocator, entityId, value);
        }
        #endif

    }

}