//#define MEMORY_ALLOCATOR_BOUNDS_CHECK
//#define LOGS_ENABLED
//#define BURST

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
using BURST = Unity.Burst.BurstCompileAttribute;

namespace ME.BECS {
    
    using math = Unity.Mathematics.math;
    using static Cuts;

    public enum ClearOptions {

        ClearMemory,
        UninitializedMemory,

    }

    public readonly struct MemPtr {

        public static readonly MemPtr Invalid = new MemPtr(0u, 0u);
        
        public readonly uint zoneId;
        public readonly uint offset;

        [INLINE(256)]
        public MemPtr(uint zoneId, uint offset) {
            this.zoneId = zoneId;
            this.offset = offset;
        }

        [INLINE(256)]
        public bool IsValid() => this.zoneId >= 0 && this.offset > 0;

        [INLINE(256)]
        public static bool operator ==(in MemPtr m1, in MemPtr m2) {
            return m1.zoneId == m2.zoneId && m1.offset == m2.offset;
        }

        [INLINE(256)]
        public static bool operator !=(in MemPtr m1, in MemPtr m2) {
            return !(m1 == m2);
        }

        [INLINE(256)]
        public bool Equals(MemPtr other) {
            return this.zoneId == other.zoneId && this.offset == other.offset;
        }

        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is MemPtr other && this.Equals(other);
        }

        [INLINE(256)]
        public override int GetHashCode() {
            return HashCode.Combine(this.zoneId, this.offset);
        }

        [INLINE(256)]
        public long AsLong() {
            var index = (long)this.zoneId << 32;
            var offset = (long)this.offset;
            return index | offset;
        }

    }
    
    public struct TSize<T> where T : struct {

        public static readonly uint size = (uint)_sizeOf<T>();
        public static readonly int sizeInt = _sizeOf<T>();

    }

    public struct TAlign<T> where T : struct {

        public static readonly uint align = (uint)_alignOf<T>();
        public static readonly int alignInt = _alignOf<T>();

    }

    public unsafe struct MemAllocatorPtr {

        internal MemPtr ptr;

        [INLINE(256)]
        public long AsLong() => this.ptr.AsLong();

        [INLINE(256)]
        public bool IsValid() {
            return this.ptr.IsValid();
        }

        [INLINE(256)]
        public ref T As<T>(in MemoryAllocator allocator) where T : unmanaged {

            return ref allocator.Ref<T>(this.ptr);

        }

        [INLINE(256)]
        public T* AsPtr<T>(in MemoryAllocator allocator, uint offset = 0u) where T : unmanaged {

            return (T*)MemoryAllocatorExt.GetUnsafePtr(in allocator, this.ptr, offset);

        }

        [INLINE(256)]
        public void Set<T>(ref MemoryAllocator allocator, in T data) where T : unmanaged {

            this.ptr = allocator.Alloc<T>();
            allocator.Ref<T>(this.ptr) = data;

        }

        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, void* data, uint dataSize) {

            this.ptr = MemoryAllocatorExt.Alloc(ref allocator, dataSize, out var ptr);
            if (data != null) {
                _memcpy(data, ptr, dataSize);
            } else {
                _memclear(ptr, dataSize);
            }

        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            allocator.Free(this.ptr);
            this = default;

        }

    }

    #if BURST
    [BURST(CompileSynchronously = true)]
    #endif
    public static unsafe class MemoryAllocatorExt {

        [INLINE(256)]
        public static byte* GetUnsafePtr(in MemoryAllocator allocator, in MemPtr ptr) {

            //var zoneIndex = ptr >> 32;
            //var offset = (ptr & MemoryAllocator.OFFSET_MASK);

            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (ptr.zoneId < allocator.zonesListCount && allocator.zonesList[ptr.zoneId] != null && allocator.zonesList[ptr.zoneId]->size < ptr.offset) {
                throw new System.Exception();
            }
            #endif

            return (byte*)allocator.zonesList[ptr.zoneId] + ptr.offset;
        }

        [INLINE(256)]
        public static byte* GetUnsafePtr(in MemoryAllocator allocator, in MemPtr ptr, uint offset) {

            return (byte*)allocator.zonesList[ptr.zoneId] + ptr.offset + offset;
        }

        [INLINE(256)]
        public static byte* GetUnsafePtr(in MemoryAllocator allocator, in MemPtr ptr, long offset) {

            return (byte*)allocator.zonesList[ptr.zoneId] + ptr.offset + offset;
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static MemPtr ReAlloc(ref MemoryAllocator allocator, in MemPtr ptr, int size) {

            return MemoryAllocatorExt.ReAlloc(ref allocator, ptr, size, out _);

        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static MemPtr ReAlloc(ref MemoryAllocator allocator, in MemPtr ptr, int size, out void* voidPtr) {

            ValidateThread(allocator);
            ValidateConsistency(allocator);

            if (ptr.IsValid() == false) return MemoryAllocatorExt.Alloc(ref allocator, size, out voidPtr);

            JobUtils.Lock(ref allocator.lockIndex);

            voidPtr = MemoryAllocatorExt.GetUnsafePtr(in allocator, ptr);
            var block = (MemoryAllocator.MemBlock*)((byte*)voidPtr - TSize<MemoryAllocator.MemBlock>.size);
            var blockSize = block->size;
            var blockDataSize = blockSize - TSize<MemoryAllocator.MemBlock>.sizeInt;
            if (blockDataSize > size) {
                JobUtils.Unlock(ref allocator.lockIndex);
                return ptr;
            }

            if (blockDataSize < 0) {
                JobUtils.Unlock(ref allocator.lockIndex);
                throw new System.Exception();
            }

            {
                var zone = allocator.zonesList[ptr.zoneId];
                var nextBlock = block->next.Ptr(zone);
                var requiredSize = size - blockDataSize;
                // next block is free and its size is enough for current size
                if (nextBlock != null &&
                    nextBlock->state == MemoryAllocator.BLOCK_STATE_FREE &&
                    nextBlock->size - TSize<MemoryAllocator.MemBlock>.sizeInt > requiredSize) {
                    // mark current block as free
                    // freePrev is false because it must not collapse block with previous one
                    // [!] may be we need to add case, which move data on collapse
                    if (MemoryAllocator.ZmFree(zone, (byte*)block + TSize<MemoryAllocator.MemBlock>.size, freePrev: false) == false) {
                        // Something went wrong
                        JobUtils.Unlock(ref allocator.lockIndex);
                        throw new System.Exception();
                    }
                    // alloc block again
                    var newPtr = MemoryAllocator.ZmAlloc(zone, block, size + TSize<MemoryAllocator.MemBlock>.sizeInt);
                    {
                        var memPtr = allocator.GetSafePtr(newPtr, ptr.zoneId);
                        if (memPtr != ptr) {
                            // Something went wrong
                            JobUtils.Unlock(ref allocator.lockIndex);
                            throw new System.Exception();
                        }
                    }
                    voidPtr = newPtr;
                    JobUtils.Unlock(ref allocator.lockIndex);
                    return ptr;
                }
            }
            
            JobUtils.Unlock(ref allocator.lockIndex);

            {
                var newPtr = MemoryAllocatorExt.Alloc(ref allocator, size, out voidPtr);
                allocator.MemMove(newPtr, 0, ptr, 0, blockDataSize);
                allocator.Free(ptr);

                return newPtr;
            }

        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static MemPtr Alloc(ref MemoryAllocator allocator, long size) {

            return MemoryAllocatorExt.Alloc(ref allocator, size, out _);

        }

        [System.Diagnostics.ConditionalAttribute(COND.ALLOCATOR_VALIDATION)]
        [Unity.Burst.BurstDiscardAttribute]
        public static void ValidateConsistency(MemoryAllocator allocator) {

            for (int i = 0; i < allocator.zonesListCount; ++i) {
                var zone = allocator.zonesList[i];
                if (zone == null) {
                    continue;
                }

                if (MemoryAllocator.ZmCheckHeap(zone) == false) {
                    throw new System.Exception();
                }
            }
            
        }

        [System.Diagnostics.ConditionalAttribute(COND.EXCEPTIONS_ALLOCATOR)]
        [Unity.Burst.BurstDiscardAttribute]
        public static void ValidateThread(MemoryAllocator allocator) {

            // Do not check thread
            /*if (System.Threading.Thread.CurrentThread.ManagedThreadId != allocator.threadId) {
                throw new System.Exception();
            }*/
            
        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static MemPtr Alloc(ref MemoryAllocator allocator, long size, out void* ptr) {

            ValidateThread(allocator);
            ValidateConsistency(allocator);

            JobUtils.Lock(ref allocator.lockIndex);

            for (uint i = 0u; i < allocator.zonesListCount; ++i) {
                var zone = allocator.zonesList[i];

                if (zone == null) continue;

                ptr = MemoryAllocator.ZmMalloc(zone, (int)size);

                if (ptr != null) {
                    var memPtr = allocator.GetSafePtr(ptr, i);
                    #if LOGS_ENABLED
                    MemoryAllocator.LogAdd(memPtr, size);
                    #endif
                    
                    JobUtils.Unlock(ref allocator.lockIndex);

                    return memPtr;
                }
            }

            {
                var zone = MemoryAllocator.ZmCreateZone((int)math.max(size, MemoryAllocator.MIN_ZONE_SIZE));
                var zoneIndex = allocator.AddZone(zone);

                ptr = MemoryAllocator.ZmMalloc(zone, (int)size);

                var memPtr = allocator.GetSafePtr(ptr, zoneIndex);
                #if LOGS_ENABLED
                MemoryAllocator.LogAdd(memPtr, size);
                #endif
                
                JobUtils.Unlock(ref allocator.lockIndex);

                return memPtr;
            }

        }

        #if BURST
        [BURST(CompileSynchronously = true)]
        #endif
        [INLINE(256)]
        public static bool Free(this ref MemoryAllocator allocator, in MemPtr ptr) {

            ValidateThread(allocator);
            ValidateConsistency(allocator);

            if (ptr.IsValid() == false) return false;

            JobUtils.Lock(ref allocator.lockIndex);

            var zoneIndex = ptr.zoneId; //ptr >> 32;

            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (zoneIndex >= allocator.zonesListCount || allocator.zonesList[zoneIndex] == null || allocator.zonesList[zoneIndex]->size < (ptr & MemoryAllocator.OFFSET_MASK)) {
                throw new OutOfBoundsException();
            }
            #endif

            var zone = allocator.zonesList[zoneIndex];

            #if LOGS_ENABLED
            if (startLog == true) {
                MemoryAllocator.LogRemove(ptr);
            }
            #endif

            var success = false;
            if (zone != null) {
                
                success = MemoryAllocator.ZmFree(zone, MemoryAllocatorExt.GetUnsafePtr(in allocator, ptr));

                if (MemoryAllocator.IsEmptyZone(zone) == true) {
                    MemoryAllocator.ZmFreeZone(zone);
                    allocator.zonesList[zoneIndex] = null;
                }
            }

            JobUtils.Unlock(ref allocator.lockIndex);

            return success;
        }

    }

    public unsafe partial struct MemoryAllocator : IDisposable {

        public LockSpinner lockIndex;
        public int threadId;
        
        #if LOGS_ENABLED && UNITY_EDITOR
        [Unity.Burst.BurstDiscardAttribute]
        public static void LogAdd(in MemPtr memPtr, long size) {
            if (startLog == true) {
                var str = "ALLOC: " + memPtr + ", SIZE: " + size;
                strList.Add(memPtr, str + "\n" + UnityEngine.StackTraceUtility.ExtractStackTrace());
            }
        }

        [Unity.Burst.BurstDiscardAttribute]
        public static void LogRemove(in MemPtr memPtr) {
            strList.Remove(memPtr);
        }

        public static bool startLog;
        public static System.Collections.Generic.Dictionary<MemPtr, string> strList = new System.Collections.Generic.Dictionary<MemPtr, string>();
        [UnityEditor.MenuItem("ME.ECS/Debug/Allocator: Start Log")]
        public static void StartLog() {
            startLog = true;
        }
        
        [UnityEditor.MenuItem("ME.ECS/Debug/Allocator: End Log")]
        public static void EndLog() {
            startLog = false;
            MemoryAllocator.strList.Clear();
        }
        
        [UnityEditor.MenuItem("ME.ECS/Debug/Allocator: Print Log")]
        public static void PrintLog() {
            foreach (var item in MemoryAllocator.strList) {
                Logger.Core.Log(item.Key + "\n" + item.Value);
            }
        }
        #endif

        public const long OFFSET_MASK = 0xFFFFFFFF;
        public const long MIN_ZONE_SIZE = 512 * 1024;//128 * 1024;
        public const int MIN_ZONE_SIZE_IN_KB = (int)(MemoryAllocator.MIN_ZONE_SIZE / 1024);
        private const int MIN_ZONES_LIST_CAPACITY = 20;

        [NativeDisableUnsafePtrRestriction]
        public MemZone** zonesList;
        public uint zonesListCount;
        internal uint zonesListCapacity;
        internal long maxSize;
        public ushort version;
        
        public bool isValid => this.zonesList != null;

        [INLINE(256)]
        public readonly int GetReservedSize() {

            var size = 0;
            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                if (zone != null) {
                    size += zone->size;
                }
            }

            return size;

        }

        [INLINE(256)]
        public readonly int GetUsedSize() {

            var size = 0;
            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                if (zone != null) {
                    size += zone->size;
                    size -= MemoryAllocator.GetZmFreeMemory(zone);
                }
            }

            return size;

        }

        [INLINE(256)]
        public readonly int GetFreeSize() {

            var size = 0;
            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                if (zone != null) {
                    size += MemoryAllocator.GetZmFreeMemory(zone);
                }
            }

            return size;

        }

        /// 
        /// Constructors
        /// 
        [INLINE(256)]
        public MemoryAllocator Initialize(long initialSize, long maxSize = -1L) {

            if (maxSize < initialSize) maxSize = initialSize;
            
            this.AddZone(MemoryAllocator.ZmCreateZone((int)math.max(initialSize, MemoryAllocator.MIN_ZONE_SIZE)));
            this.maxSize = maxSize;
            this.threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            this.version = 1;

            return this;
        }

        [INLINE(256)]
        public void Dispose() {

            this.FreeZones();
            
            if (this.zonesList != null) {
                _free(this.zonesList, Constants.ALLOCATOR_PERSISTENT);
				this.zonesList = null;
			}

            this.zonesListCapacity = 0;
            this.maxSize = default;

        }

        [INLINE(256)]
        public void CopyFrom(in MemoryAllocator other) {

            if (other.zonesList == null && this.zonesList == null) {
                
            } else if (other.zonesList == null && this.zonesList != null) {
                this.FreeZones();
            } else {
	    
		        var areEquals = true;
                
                if (this.zonesListCount < other.zonesListCount) {

                    for (uint i = this.zonesListCount; i < other.zonesListCount; ++i) {
                        var otherZone = other.zonesList[i];
                        if (otherZone == null) {
                            this.AddZone(null, false);
                        } else {
                            var zone = MemoryAllocator.ZmCreateZone(otherZone->size);
                            this.AddZone(zone, false);
                        }
                    }
                        
                }
                
                if (this.zonesListCount == other.zonesListCount) {

                    for (int i = 0; i < other.zonesListCount; ++i) {
                        ref var curZone = ref this.zonesList[i];
                        var otherZone = other.zonesList[i];
                        {
                            if (curZone == null && otherZone == null) continue;
                            
                            if (curZone == null) {
                                curZone = MemoryAllocator.ZmCreateZone(otherZone->size);
                                _memcpy(otherZone, curZone, otherZone->size);
                            } else if (otherZone == null) {
                                MemoryAllocator.ZmFreeZone(curZone);
                                curZone = null;
                            } else {
                                // resize zone
                                curZone = MemoryAllocator.ZmReallocZone(curZone, otherZone->size);
                                _memcpy(otherZone, curZone, otherZone->size);
                            }
                        }
                    }

                } else {

                    areEquals = false;
                    
                }

                if (areEquals == false) {
		    
                    this.FreeZones();

		            for (int i = 0; i < other.zonesListCount; i++) {
		                var otherZone = other.zonesList[i];

                        if (otherZone != null) {
                            var zone = MemoryAllocator.ZmCreateZone(otherZone->size);
                            _memcpy(otherZone, zone, otherZone->size);
                            this.AddZone(zone, false);
                        } else {
                            this.AddZone(null, false);
                        }

                    }
                    
                }

            }

            this.version = other.version;
            ++this.version;
            this.threadId = other.threadId;
            this.maxSize = other.maxSize;
	    
        }

        [INLINE(256)]
        public void CopyFromComplete(in MemoryAllocator other, int index) {
            
            // We must be sure that source allocator has the same structure and size as current
            // So we must call CopyFromPrepare() first
            var curZone = this.zonesList[index];
            var otherZone = other.zonesList[index];
            {
                if (curZone == null && otherZone == null) return;
                {
                    _memcpy(otherZone, curZone, otherZone->size);
                }
            }
            
        }

        [INLINE(256)]
        public void CopyFromPrepare(in MemoryAllocator other) {

            if (other.zonesList == null && this.zonesList == null) {
                
            } else if (other.zonesList == null && this.zonesList != null) {
                this.FreeZones();
            } else {
	    
		        var areEquals = true;
                
                if (this.zonesListCount < other.zonesListCount) {

                    for (uint i = this.zonesListCount; i < other.zonesListCount; ++i) {
                        var otherZone = other.zonesList[i];
                        if (otherZone == null) {
                            this.AddZone(null, false);
                        } else {
                            var zone = MemoryAllocator.ZmCreateZone(otherZone->size);
                            this.AddZone(zone, false);
                        }
                    }
                        
                }
                
                if (this.zonesListCount == other.zonesListCount) {

                    for (int i = 0; i < other.zonesListCount; ++i) {
                        ref var curZone = ref this.zonesList[i];
                        var otherZone = other.zonesList[i];
                        {
                            if (curZone == null && otherZone == null) continue;
                            
                            if (curZone == null) {
                                curZone = MemoryAllocator.ZmCreateZone(otherZone->size);
                            } else if (otherZone == null) {
                                MemoryAllocator.ZmFreeZone(curZone);
                                curZone = null;
                            } else {
                                // resize zone
                                curZone = MemoryAllocator.ZmReallocZone(curZone, otherZone->size);
                            }
                        }
                    }

                } else {

                    areEquals = false;
                    
                }

                if (areEquals == false) {
		    
                    this.FreeZones();

		            for (int i = 0; i < other.zonesListCount; i++) {
		                var otherZone = other.zonesList[i];
                        if (otherZone != null) {
                            var zone = MemoryAllocator.ZmCreateZoneEmpty(otherZone->size);
                            this.AddZone(zone, false);
                        } else {
                            this.AddZone(null, false);
                        }

                    }

                }

            }

            this.version = other.version;
            ++this.version;
            this.threadId = other.threadId;
            this.maxSize = other.maxSize;

        }

        [INLINE(256)]
        private void FreeZones() {
            if (this.zonesListCount > 0 && this.zonesList != null) {
                for (int i = 0; i < this.zonesListCount; i++) {
                    var zone = this.zonesList[i];
                    if (zone != null) {
                        MemoryAllocator.ZmFreeZone(zone);
                    }
                }
            }

            this.zonesListCount = 0;
        }

        [INLINE(256)]
        internal uint AddZone(MemZone* zone, bool lookUpNull = true) {

            if (lookUpNull == true) {

                for (uint i = 0u; i < this.zonesListCount; ++i) {
                    if (this.zonesList[i] == null) {
                        this.zonesList[i] = zone;
                        return i;
                    }
                }

            }

            if (this.zonesListCapacity <= this.zonesListCount) {
                
                var capacity = math.max(MemoryAllocator.MIN_ZONES_LIST_CAPACITY, this.zonesListCapacity * 2);
                var list = (MemZone**)_make(capacity * (uint)sizeof(MemZone*), TAlign<System.IntPtr>.alignInt, Constants.ALLOCATOR_PERSISTENT);

                if (this.zonesList != null) {
                    _memcpy(this.zonesList, list, (uint)sizeof(MemZone*) * this.zonesListCount);
                    /*for (int i = 0; i < this.zonesListCount; i++) {
                        list[i] = this.zonesList[i];
                    }*/
                    _free(this.zonesList, Constants.ALLOCATOR_PERSISTENT);
                }
                
                this.zonesList = list;
                this.zonesListCapacity = capacity;
                
            }

            this.zonesList[this.zonesListCount++] = zone;

            return this.zonesListCount - 1u;
        }

        /// 
        /// Base
        ///
        
        [INLINE(256)]
        public readonly ref T Ref<T>(in MemPtr ptr) where T : unmanaged {
            return ref *(T*)MemoryAllocatorExt.GetUnsafePtr(in this, ptr);
        }

        [INLINE(256)]
        public readonly ref T Ref<T>(MemPtr ptr) where T : unmanaged {
            return ref *(T*)MemoryAllocatorExt.GetUnsafePtr(in this, ptr);
        }

        [INLINE(256)]
        public MemPtr AllocData<T>(T data) where T : unmanaged {
            var ptr = this.Alloc<T>();
            this.Ref<T>(ptr) = data;
            return ptr;
        }

        [INLINE(256)]
        public MemPtr Alloc<T>() where T : struct {
            var size = TSize<T>.size;
            var alignOf = TAlign<T>.align;
            return MemoryAllocatorExt.Alloc(ref this, size + alignOf);
        }

        [INLINE(256)]
        public readonly void MemCopy(in MemPtr dest, long destOffset, in MemPtr source, long sourceOffset, long length) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            var destZoneIndex = dest >> 32;
            var sourceZoneIndex = source >> 32;
            var destMaxOffset = (dest & MemoryAllocator.OFFSET_MASK) + destOffset + length;
            var sourceMaxOffset = (source & MemoryAllocator.OFFSET_MASK) + sourceOffset + length;
            
            if (destZoneIndex >= this.zonesListCount || sourceZoneIndex >= this.zonesListCount) {
                throw new System.Exception();
            }
            
            if (this.zonesList[destZoneIndex]->size < destMaxOffset || this.zonesList[sourceZoneIndex]->size < sourceMaxOffset) {
                throw new System.Exception();
            }
            #endif
            
            _memcpy(MemoryAllocatorExt.GetUnsafePtr(in this, source, sourceOffset), MemoryAllocatorExt.GetUnsafePtr(in this, dest, destOffset), length);
            
        }

        [INLINE(256)]
        public readonly void MemMove(in MemPtr dest, long destOffset, in MemPtr source, long sourceOffset, long length) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            var destZoneIndex = dest >> 32;
            var sourceZoneIndex = source >> 32;
            var destMaxOffset = (dest & MemoryAllocator.OFFSET_MASK) + destOffset + length;
            var sourceMaxOffset = (source & MemoryAllocator.OFFSET_MASK) + sourceOffset + length;
            
            if (destZoneIndex >= this.zonesListCount || sourceZoneIndex >= this.zonesListCount) {
                throw new System.Exception();
            }
            
            if (this.zonesList[destZoneIndex]->size < destMaxOffset || this.zonesList[sourceZoneIndex]->size < sourceMaxOffset) {
                throw new System.Exception();
            }
            #endif
            
            _memmove(MemoryAllocatorExt.GetUnsafePtr(in this, source, sourceOffset), MemoryAllocatorExt.GetUnsafePtr(in this, dest, destOffset), length);
            
        }

        [INLINE(256)]
        public readonly void MemClear(in MemPtr dest, long destOffset, long length) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            var zoneIndex = dest >> 32;
            
            if (zoneIndex >= this.zonesListCount || this.zonesList[zoneIndex]->size < ((dest & MemoryAllocator.OFFSET_MASK) + destOffset + length)) {
                throw new OutOfBoundsException();
            }
            #endif

            _memclear(MemoryAllocatorExt.GetUnsafePtr(in this, dest, destOffset), length);
            
        }

        [INLINE(256)]
        public void Prepare(long size) {

            for (int i = 0; i < this.zonesListCount; i++) {
                var zone = this.zonesList[i];
                
                if (zone == null) continue;

                if (MemoryAllocator.ZmHasFreeBlock(zone, (int)size) == true) {
                    return;
                }
            }
 
            this.AddZone(MemoryAllocator.ZmCreateZone((int)math.max(size, MemoryAllocator.MIN_ZONE_SIZE)));
            
        }

        [INLINE(256)]
        internal readonly MemPtr GetSafePtr(void* ptr, uint zoneIndex) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (zoneIndex < this.zonesListCount && this.zonesList[zoneIndex] != null) {
                throw new OutOfBoundsException();
            }
            #endif
            
            //var index = (long)zoneIndex << 32;
            //var offset = ((byte*)ptr - (byte*)this.zonesList[zoneIndex]);

            // index | offset
            return new MemPtr(zoneIndex, (uint)((byte*)ptr - (byte*)this.zonesList[zoneIndex]));
        }

        /// 
        /// Arrays
        /// 
        [INLINE(256)]
        public readonly MemPtr RefArrayPtr<T>(in MemPtr ptr, int index) where T : unmanaged {
            var size = TSize<T>.size;
            return new MemPtr(ptr.zoneId, ptr.offset + (uint)index * size);
        }
        
        [INLINE(256)]
        public readonly MemPtr RefArrayPtr<T>(in MemPtr ptr, uint index) where T : unmanaged {
            var size = TSize<T>.size;
            return new MemPtr(ptr.zoneId, ptr.offset + index * size);
        }
        
        [INLINE(256)]
        public readonly ref T RefArray<T>(in MemPtr ptr, int index) where T : unmanaged {
            var size = TSize<T>.size;
            return ref *(T*)MemoryAllocatorExt.GetUnsafePtr(in this, in ptr, index * size);
        }

        [INLINE(256)]
        public readonly ref T RefArray<T>(MemPtr ptr, int index) where T : unmanaged {
            var size = TSize<T>.size;
            return ref *(T*)MemoryAllocatorExt.GetUnsafePtr(in this, in ptr, index * size);
        }

        [INLINE(256)]
        public readonly ref T RefArray<T>(in MemPtr ptr, uint index) where T : unmanaged {
            var size = TSize<T>.size;
            return ref *(T*)MemoryAllocatorExt.GetUnsafePtr(in this, in ptr, index * size);
        }

        [INLINE(256)]
        public readonly ref T RefArray<T>(MemPtr ptr, uint index) where T : unmanaged {
            var size = TSize<T>.size;
            return ref *(T*)MemoryAllocatorExt.GetUnsafePtr(in this, in ptr, index * size);
        }

        [INLINE(256)]
        public MemPtr ReAllocArray<T>(in MemPtr ptr, int newLength) where T : unmanaged {
            var size = TSize<T>.size;
            return MemoryAllocatorExt.ReAlloc(ref this, in ptr, (int)(size * newLength));
        }

        [INLINE(256)]
        public MemPtr ReAllocArray<T>(in MemPtr ptr, uint newLength) where T : unmanaged {
            var size = TSize<T>.size;
            return MemoryAllocatorExt.ReAlloc(ref this, in ptr, (int)(size * newLength));
        }

        [INLINE(256)]
        public MemPtr ReAllocArray<T>(in MemPtr memPtr, uint newLength, out T* ptr) where T : unmanaged {
            var size = TSize<T>.size;
            var newPtr = MemoryAllocatorExt.ReAlloc(ref this, in memPtr, (int)(size * newLength), out var voidPtr);
            ptr = (T*)voidPtr;
            return newPtr;
        }

        [INLINE(256)]
        public MemPtr ReAllocArray(uint elementSizeOf, in MemPtr ptr, uint newLength) {
            return MemoryAllocatorExt.ReAlloc(ref this, ptr, (int)(elementSizeOf * newLength));
        }

        [INLINE(256)]
        public MemPtr ReAllocArray(uint elementSizeOf, in MemPtr ptr, uint newLength, out void* voidPtr) {
            return MemoryAllocatorExt.ReAlloc(ref this, in ptr, (int)(elementSizeOf * newLength), out voidPtr);
        }

        [INLINE(256)]
        public MemPtr AllocArray<T>(int length) where T : struct {
            var size = TSize<T>.size;
            return MemoryAllocatorExt.Alloc(ref this, size * length);
        }

        [INLINE(256)]
        public MemPtr AllocArray<T>(uint length) where T : struct {
            var size = TSize<T>.size;
            return MemoryAllocatorExt.Alloc(ref this, size * length);
        }

        [INLINE(256)]
        public MemPtr AllocArray(int length, int sizeOf) {
            var size = sizeOf;
            return MemoryAllocatorExt.Alloc(ref this, size * length);
        }

        [INLINE(256)]
        public MemPtr AllocArray<T>(uint length, out T* ptr) where T : unmanaged {
            var size = TSize<T>.size;
            var memPtr = MemoryAllocatorExt.Alloc(ref this, size * length, out var voidPtr);
            ptr = (T*)voidPtr;
            return memPtr;
        }

        public void Deserialize(ref StreamBufferReader stream) {

            var allocator = new MemoryAllocator();
            stream.Read(ref allocator.version);
            stream.Read(ref allocator.maxSize);
            stream.Read(ref allocator.zonesListCount);
            
            allocator.zonesListCapacity = allocator.zonesListCount;
            allocator.zonesList = (MemoryAllocator.MemZone**)_make(allocator.zonesListCount * (uint)sizeof(MemoryAllocator.MemZone*), TAlign<System.IntPtr>.alignInt, Constants.ALLOCATOR_PERSISTENT);

            for (int i = 0; i < allocator.zonesListCount; ++i) {

                var length = 0;
                stream.Read(ref length);
                if (length == 0) continue;

                var zone = MemoryAllocator.ZmCreateZone(length);

                allocator.zonesList[i] = zone;
                var readSize = length;
                var zn = (byte*)zone;
                stream.Read(ref zn, (uint)readSize);

            }

            this = allocator;

        }

        public readonly void Serialize(ref StreamBufferWriter stream) {
            
            stream.Write(this.version);
            stream.Write(this.maxSize);
            stream.Write(this.zonesListCount);

            for (int i = 0; i < this.zonesListCount; ++i) {
                var zone = this.zonesList[i];

                stream.Write(zone->size);

                if (zone->size == 0) continue;

                var writeSize = zone->size;
                stream.Write((byte*)zone, (uint)writeSize);
                //System.Runtime.InteropServices.Marshal.Copy((System.IntPtr)zone, buffer, pos, writeSize);
            }
        }

    }

}
