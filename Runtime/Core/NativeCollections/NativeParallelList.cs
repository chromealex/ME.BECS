#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.NativeCollections {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using static Cuts;

    public unsafe struct NativeParallelList<T> where T : unmanaged {

        private static readonly uint CACHE_LINE_SIZE = math.max(JobUtils.CacheLineSize / TSize<T>.size, 1u);
        
        [NativeDisableUnsafePtrRestriction]
        public safe_ptr<Unity.Collections.LowLevel.Unsafe.UnsafeList<T>> lists;
        private AllocatorManager.AllocatorHandle allocator;

        public bool isCreated => this.lists.ptr != null;
        
        public readonly uint Length => JobUtils.ThreadsCount;

        [INLINE(256)]
        public NativeParallelList(int capacity, AllocatorManager.AllocatorHandle allocator) {

            this.allocator = allocator;
            var size = TSize<Unity.Collections.LowLevel.Unsafe.UnsafeList<T>>.size;
            this.lists = _makeArray<Unity.Collections.LowLevel.Unsafe.UnsafeList<T>>(JobUtils.ThreadsCount * size * CACHE_LINE_SIZE, allocator.ToAllocator);
            for (uint i = 0u; i < this.Length; ++i) {
                this.lists[i * CACHE_LINE_SIZE] = new UnsafeList<T>(capacity, allocator);
            }
            
        }

        [INLINE(256)]
        public void Dispose() {
            
            for (uint i = 0u; i < this.Length; ++i) {
                this.lists[i * CACHE_LINE_SIZE].Dispose();
            }
            _free(this.lists, this.allocator.ToAllocator);
            
        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle jobHandle) {

            var tempDeps = new NativeArray<JobHandle>((int)this.Length, Allocator.Temp);
            for (uint i = 0u; i < this.Length; ++i) {
                var copy = this.lists[i * CACHE_LINE_SIZE];
                tempDeps[(int)i] = copy.Dispose(jobHandle);
            }
            jobHandle = new DisposeWithAllocatorPtrJob() {
                allocator = this.allocator,
                ptr = this.lists,
            }.Schedule(JobHandle.CombineDependencies(tempDeps));
            return jobHandle;

        }

        public int Count {
            [INLINE(256)]
            get {
                var count = 0;
                for (uint i = 0u; i < this.Length; ++i) {
                    count += this.lists[i * CACHE_LINE_SIZE].Length;
                }

                return count;
            }
        }

        [INLINE(256)]
        public void Add(in T item) {

            var threadItem = (uint)JobsUtility.ThreadIndex;
            ref var arr = ref this.lists[threadItem * CACHE_LINE_SIZE];
            arr.Add(item);

        }

        [INLINE(256)]
        public UnsafeList<T> ToList(Allocator allocator) {

            var count = 0;
            for (uint i = 0u; i < this.Length; ++i) {
                count += this.lists[i * CACHE_LINE_SIZE].Length;
            }
            var targetList = new UnsafeList<T>(count, allocator);
            targetList.Length = count;
            var offset = 0;
            for (uint i = 0u; i < this.Length; ++i) {
                var list = this.lists[i * CACHE_LINE_SIZE];
                _memcpy((safe_ptr)list.Ptr, (safe_ptr)(targetList.Ptr + offset), TSize<T>.size * list.Length);
                offset += list.Length;
            }

            return targetList;

        }

        [INLINE(256)]
        public void Clear() {
            
            for (uint i = 0u; i < this.Length; ++i) {
                var item = this.lists[i * CACHE_LINE_SIZE];
                item.Clear();
                this.lists[i * CACHE_LINE_SIZE] = item;
            }
            
        }

    }

}