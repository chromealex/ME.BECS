#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.NativeCollections {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using static Cuts;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary>
    /// Stores entries written through per-thread native buffers.
    /// </summary>
    public unsafe partial struct NativeParallelList<T> : IIsCreated where T : unmanaged {

        private static readonly uint CACHE_LINE_SIZE = _align(TSize<UnsafeList<T>>.size, JobUtils.CacheLineSize);
        
        /// <summary>
        /// Lists used by <c>NativeParallelList</c>.
        /// </summary>
        public safe_ptr lists;
        private AllocatorManager.AllocatorHandle allocator;

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.lists.ptr != null;
        
        /// <summary>
        /// Number of elements exposed by this value.
        /// </summary>
        public readonly uint Length => JobUtils.ThreadsCount;

        /// <summary>
        /// Initializes <c>NativeParallelList</c> from the supplied capacity, allocator.
        /// </summary>
        [INLINE(256)]
        public NativeParallelList(int capacity, AllocatorManager.AllocatorHandle allocator) {

            this = default;
            this.allocator = allocator;
            this.lists = _make(CACHE_LINE_SIZE * this.Length, TAlign<UnsafeList<T>>.alignInt, allocator.ToAllocator);
            for (uint i = 0u; i < this.Length; ++i) {
                *(UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr = new UnsafeList<T>(capacity, allocator);
            }
            
        }

        /// <summary>
        /// Releases the resources owned by this native parallel list instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            
            for (uint i = 0u; i < this.Length; ++i) {
                ((UnsafeList<T>*)(this.lists + i * CACHE_LINE_SIZE).ptr)->Dispose();
            }
            _free(this.lists, this.allocator.ToAllocator);
            
        }

        /// <summary>
        /// Executes dispose work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct DisposeJob : IJob {

            /// <summary>
            /// List storage used by this instance.
            /// </summary>
            public NativeParallelList<T> list;

            /// <summary>
            /// Processes dispose using the supplied job inputs.
            /// </summary>
            public void Execute() {

                this.list.Dispose();

            }

        }
        
        /// <summary>
        /// Schedules release of the owned storage after the supplied dependency and returns the disposal handle.
        /// </summary>
        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle jobHandle) {

            return new DisposeJob() {
                list = this,
            }.Schedule(jobHandle);
            
        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
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

        /// <summary>
        /// Returns thread list.
        /// </summary>
        [INLINE(256)]
        public readonly ref UnsafeList<T> GetThreadList() {

            return ref *((UnsafeList<T>*)(this.lists + (uint)JobsUtility.ThreadIndex * CACHE_LINE_SIZE).ptr);

        }

        /// <summary>
        /// Adds the supplied entry to native parallel list.
        /// </summary>
        [INLINE(256)]
        public void Add(in T item) {

            ref var arr = ref this.GetThreadList();
            arr.Add(item);

        }

        /// <summary>
        /// Converts the value to list.
        /// </summary>
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

        /// <summary>
        /// Clears the current native parallel list contents.
        /// </summary>
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
