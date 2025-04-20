namespace ME.BECS.Trees {

    using Unity.Jobs;
    using Unity.Collections;
    using System.Collections.Generic;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;

    public static unsafe class SortJobExt {

        /// <summary>
        /// Returns a job which will sort this list using a custom comparison.
        /// </summary>
        /// <remarks>This method does not schedule the job. Scheduling the job is left to you.</remarks>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="TU">The type of the comparer.</typeparam>
        /// <param name="list">The list to sort.</param>
        /// <param name="comp">The comparison function used to determine the relative order of the elements.</param>
        /// <returns>A job for sorting this list.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(NativeSortExtension.DefaultComparer<int>) })]
        public static unsafe SortJobDefer<T, TU> SortJobDefer<T, TU>(this NativeList<T> list, TU comp)
            where T : unmanaged
            where TU : IComparer<T> {
            return SortJobDefer((T*)list.GetUnsafeList()->Ptr, comp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(NativeSortExtension.DefaultComparer<int>) })]
        public static unsafe SortJobDefer<T, TU> SortJobDefer<T, TU>(T* array, TU comp)
            where T : unmanaged
            where TU : IComparer<T> {
            return new SortJobDefer<T, TU>() { data = array, comp = comp };
        }

        public static void CalculateSegmentCount(int count, ME.BECS.NativeCollections.DeferJobCounter* segmentCount) {
            segmentCount->count = (count + 1023) / 1024;
            //int maxThreadCount = JobsUtility.ThreadIndexCount;
            //var workerCount = math.max(1, maxThreadCount);
            //var workerSegmentCount = segmentCount / workerCount;
        }

    }

    /// <summary>
    /// Returned by the `SortJob` methods of <see cref="Unity.Collections.NativeSortExtension"/>. Call `Schedule` to schedule the sorting.
    /// </summary>
    /// <typeparam name="T">The type of the elements to sort.</typeparam>
    /// <typeparam name="TU">The type of the comparer.</typeparam>
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(NativeSortExtension.DefaultComparer<int>) })]
    public unsafe struct SortJobDefer<T, TU>
        where T : unmanaged
        where TU : IComparer<T> {

        /// <summary>
        /// The data to sort.
        /// </summary>
        public T* data;

        /// <summary>
        /// Comparison function.
        /// </summary>
        public TU comp;

        [BurstCompile]
        private struct SegmentSort : IJobParallelForDefer {

            [NativeDisableUnsafePtrRestriction]
            public T* data;
            public TU comp;

            [NativeDisableUnsafePtrRestriction]
            public int* length;
            public int segmentWidth;

            public void Execute(int index) {
                var startIndex = index * this.segmentWidth;
                var segmentLength = *this.length - startIndex < this.segmentWidth ? *this.length - startIndex : this.segmentWidth;
                NativeSortExtension.Sort(this.data + startIndex, segmentLength, this.comp);
            }

        }

        [BurstCompile]
        private struct SegmentSortMerge : IJob {

            [NativeDisableUnsafePtrRestriction]
            public T* data;
            public TU comp;

            [NativeDisableUnsafePtrRestriction]
            public int* length;
            public int segmentWidth;

            public void Execute() {
                var segmentCount = (*this.length + (this.segmentWidth - 1)) / this.segmentWidth;
                var segmentIndex = stackalloc int[segmentCount];

                var resultCopy = (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * *this.length, 16, Allocator.Temp);

                for (var sortIndex = 0; sortIndex < *this.length; sortIndex++) {
                    // find next best
                    var bestSegmentIndex = -1;
                    var bestValue = default(T);

                    for (var i = 0; i < segmentCount; i++) {
                        var startIndex = i * this.segmentWidth;
                        var offset = segmentIndex[i];
                        var segmentLength = *this.length - startIndex < this.segmentWidth ? *this.length - startIndex : this.segmentWidth;
                        if (offset == segmentLength) {
                            continue;
                        }

                        var nextValue = this.data[startIndex + offset];
                        if (bestSegmentIndex != -1) {
                            if (this.comp.Compare(nextValue, bestValue) > 0) {
                                continue;
                            }
                        }

                        bestValue = nextValue;
                        bestSegmentIndex = i;
                    }

                    segmentIndex[bestSegmentIndex]++;
                    resultCopy[sortIndex] = bestValue;
                }

                UnsafeUtility.MemCpy(this.data, resultCopy, UnsafeUtility.SizeOf<T>() * *this.length);
            }

        }

        /// <summary>
        /// Schedules this job.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="inputDeps">Handle of a job to depend upon.</param>
        /// <returns>The handle of this newly scheduled job.</returns>
        public JobHandle Schedule(ME.BECS.NativeCollections.DeferJobCounter* count, ME.BECS.NativeCollections.DeferJobCounter* segmentCount, JobHandle inputDeps = default) {
            //if (Length == 0) return inputDeps;
            //var segmentCount = (Length + 1023) / 1024;

            //int maxThreadCount = JobsUtility.ThreadIndexCount;
            //var workerCount = math.max(1, maxThreadCount);
            //var workerSegmentCount = segmentCount / workerCount;
            var segmentSortJob = new SegmentSort { data = this.data, comp = this.comp, length = &count->count, segmentWidth = 1024 };
            var segmentSortJobHandle = segmentSortJob.Schedule(&segmentCount->count, 64, inputDeps);
            var segmentSortMergeJob = new SegmentSortMerge { data = this.data, comp = this.comp, length = &count->count, segmentWidth = 1024 };
            var segmentSortMergeJobHandle = segmentSortMergeJob.Schedule(segmentSortJobHandle);
            return segmentSortMergeJobHandle;
        }

    }

}