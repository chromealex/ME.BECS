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
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, JobHandle h5, JobHandle h6, JobHandle h7) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(7, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(8, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, JobHandle h9) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(9, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(10, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(11, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(12, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12,
                                                    JobHandle h13) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(13, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            arr[12] = h13;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12,
                                                    JobHandle h13, JobHandle h14) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(14, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            arr[12] = h13;
            arr[13] = h14;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12,
                                                    JobHandle h13, JobHandle h14, JobHandle h15) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(15, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            arr[12] = h13;
            arr[13] = h14;
            arr[14] = h15;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12,
                                                    JobHandle h13, JobHandle h14, JobHandle h15, JobHandle h16) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(16, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            arr[12] = h13;
            arr[13] = h14;
            arr[14] = h15;
            arr[15] = h16;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12,
                                                    JobHandle h13, JobHandle h14, JobHandle h15, JobHandle h16,
                                                    JobHandle h17) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(17, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            arr[12] = h13;
            arr[13] = h14;
            arr[14] = h15;
            arr[15] = h16;
            arr[16] = h17;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12,
                                                    JobHandle h13, JobHandle h14, JobHandle h15, JobHandle h16,
                                                    JobHandle h17, JobHandle h18) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(18, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            arr[12] = h13;
            arr[13] = h14;
            arr[14] = h15;
            arr[15] = h16;
            arr[16] = h17;
            arr[17] = h18;
            return JobHandle.CombineDependencies(arr);
        }

        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12,
                                                    JobHandle h13, JobHandle h14, JobHandle h15, JobHandle h16,
                                                    JobHandle h17, JobHandle h18, JobHandle h19) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(19, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            arr[12] = h13;
            arr[13] = h14;
            arr[14] = h15;
            arr[15] = h16;
            arr[16] = h17;
            arr[17] = h18;
            arr[18] = h19;
            return JobHandle.CombineDependencies(arr);
        }
        
        [INLINE(256)]
        public static JobHandle CombineDependencies(JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, 
                                                    JobHandle h5, JobHandle h6, JobHandle h7, JobHandle h8, 
                                                    JobHandle h9, JobHandle h10, JobHandle h11, JobHandle h12,
                                                    JobHandle h13, JobHandle h14, JobHandle h15, JobHandle h16,
                                                    JobHandle h17, JobHandle h18, JobHandle h19, JobHandle h20) {
            var arr = new Unity.Collections.NativeArray<JobHandle>(20, Unity.Collections.Allocator.Temp);
            arr[0] = h1;
            arr[1] = h2;
            arr[2] = h3;
            arr[3] = h4;
            arr[4] = h5;
            arr[5] = h6;
            arr[6] = h7;
            arr[7] = h8;
            arr[8] = h9;
            arr[9] = h10;
            arr[10] = h11;
            arr[11] = h12;
            arr[12] = h13;
            arr[13] = h14;
            arr[14] = h15;
            arr[15] = h16;
            arr[16] = h17;
            arr[17] = h18;
            arr[18] = h19;
            arr[19] = h20;
            return JobHandle.CombineDependencies(arr);
        }
    }

}