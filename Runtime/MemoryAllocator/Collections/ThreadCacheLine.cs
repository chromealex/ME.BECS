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
    using Unity.Jobs;
    using static Cuts;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayThreadCacheLineProxy<>))]
    public unsafe struct ThreadCacheLine<T> : IIsCreated where T : unmanaged {

        private static readonly uint CACHE_LINE_SIZE = _align(TSize<T>.size, JobUtils.CacheLineSize);

        private readonly safe_ptr arrPtr;
        private readonly Unity.Collections.Allocator allocator;
        public static uint Length => JobUtils.ThreadsCount;
        public uint Count => Length;

        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.arrPtr.ptr != null;
        }

        [INLINE(256)]
        public ThreadCacheLine(Unity.Collections.Allocator allocator, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            this.allocator = allocator;
            var memPtr = _make(CACHE_LINE_SIZE * Length, TAlign<T>.alignInt, allocator);
            if (clearOptions == ClearOptions.ClearMemory) {
                _memclear(memPtr, CACHE_LINE_SIZE * Length);
            }
            
            this.arrPtr = memPtr;

        }

        [INLINE(256)]
        public void Dispose() {

            E.IS_CREATED(this);

            if (this.arrPtr.ptr != null) {
                _free(this.arrPtr, this.allocator);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeWithAllocatorPtrJob() {
                ptr = this.arrPtr,
                allocator = this.allocator,
            }.Schedule(inputDeps);
            
            return jobHandle;

        }

        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr() {

            return this.arrPtr;

        }
        
        public readonly ref T this[uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, Length);
                return ref *(T*)((safe_ptr<byte>)this.GetUnsafePtr() + index * CACHE_LINE_SIZE).ptr;
            }
        }

        [INLINE(256)]
        public void Clear() {

            _memclear(this.arrPtr, Length * CACHE_LINE_SIZE);

        }

        public uint GetReservedSizeInBytes() {

            return Length * CACHE_LINE_SIZE;

        }

    }

}