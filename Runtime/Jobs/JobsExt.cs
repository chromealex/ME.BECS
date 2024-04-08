namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Jobs;

    public static class JobsExt {

        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2) {
            return JobHandle.CombineDependencies(h1, h2);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3) {
            return JobHandle.CombineDependencies(h1, h2, h3);
        }

        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(4, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            return JobHandle.CombineDependencies(arr);
        }

        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, JobHandle h5) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(5, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            return JobHandle.CombineDependencies(arr);
        }

        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, JobHandle h5, JobHandle h6) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(6, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            return JobHandle.CombineDependencies(arr);
        }

    }

}