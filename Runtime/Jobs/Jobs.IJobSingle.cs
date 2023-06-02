namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    [JobProducerType(typeof(IJobSingleExtensions.JobProcess<>))]
    public interface IJobSingle {
        void Execute();
    }

    public static unsafe class IJobSingleExtensions {
        
        public static JobHandle ScheduleSingle<T>(this T jobData, JobHandle inputDeps = default) where T : struct, IJobSingle {
            
            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref jobData), JobProcess<T>.Initialize(), inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
            
        }

        public static JobHandle ScheduleSingleByRef<T>(ref this T jobData, JobHandle inputDeps = default) where T : struct, IJobSingle {
            
            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref jobData), JobProcess<T>.Initialize(), inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
            
        }

        internal struct JobProcess<T> where T : struct, IJobSingle {
            
            private static System.IntPtr jobReflectionData;

            public static System.IntPtr Initialize() {
                if (jobReflectionData == System.IntPtr.Zero) {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(T), typeof(T), (ExecuteJobFunction)Execute);
                }
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref T jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public static void Execute(ref T jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                JobUtils.SetCurrentThreadAsSingle(true);
                jobData.Execute();
                JobUtils.SetCurrentThreadAsSingle(false);
                
            }
        }
    }
    
}