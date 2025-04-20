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
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using static Cuts;

    public unsafe struct NativeParallelList<T> : IIsCreated where T : unmanaged {

        private static readonly uint CACHE_LINE_SIZE = _align(TSize<UnsafeList<T>>.size, JobUtils.CacheLineSize);
        
        public safe_ptr lists;
        private AllocatorManager.AllocatorHandle allocator;

        public bool IsCreated => this.lists.ptr != null;
        
        public readonly uint Length => JobUtils.ThreadsCount;

        [INLINE(256)]
        public NativeParallelList(int capacity, AllocatorManager.AllocatorHandle allocator) {

            this = default;
            this.allocator = allocator;
            this.lists = _make(CACHE_LINE_SIZE * this.Length, TAlign<UnsafeList<T>>.alignInt, allocator.ToAllocator);
            for (uint i = 0u; i < this.Length; ++i) {
                *(UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr = new UnsafeList<T>(capacity, allocator);
            }
            
        }

        [INLINE(256)]
        public void Dispose() {
            
            for (uint i = 0u; i < this.Length; ++i) {
                ((UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr)->Dispose();
            }
            _free(this.lists, this.allocator.ToAllocator);
            
        }

        [BURST]
        public struct DisposeJob : IJob {

            public NativeParallelList<T> list;

            public void Execute() {

                this.list.Dispose();

            }

        }
        
        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle jobHandle) {

            return new DisposeJob() {
                list = this,
            }.Schedule(jobHandle);
            
        }

        public int Count {
            [INLINE(256)]
            get {
                var count = 0;
                for (uint i = 0u; i < this.Length; ++i) {
                    count += ((UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr)->Length;
                }

                return count;
            }
        }

        [INLINE(256)]
        public void Add(in T item) {

            ref var arr = ref *((UnsafeList<T>*)(this.lists + JobUtils.ThreadIndex * CACHE_LINE_SIZE).ptr);
            arr.Add(item);

        }

        [INLINE(256)]
        public UnsafeList<T> ToList(Allocator allocator) {

            var count = 0;
            for (uint i = 0u; i < this.Length; ++i) {
                count += ((UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr)->Length;
            }
            var targetList = new UnsafeList<T>(count, allocator);
            targetList.Length = count;
            var offset = 0;
            for (uint i = 0u; i < this.Length; ++i) {
                var list = *((UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr);
                if (list.IsCreated == false || list.Length == 0u) continue;
                _memcpy((safe_ptr)list.Ptr, (safe_ptr)(targetList.Ptr + offset), TSize<T>.size * list.Length);
                offset += list.Length;
            }

            return targetList;

        }

        [INLINE(256)]
        public void Clear() {
            
            for (uint i = 0u; i < this.Length; ++i) {
                var item = *((UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr);
                item.Clear();
                *((UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr) = item;
            }
            
        }

    }

}