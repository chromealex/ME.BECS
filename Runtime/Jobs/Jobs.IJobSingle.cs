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
        
        public static void EarlyJobInit<T>() where T : struct, IJobSingle => IJobSingleExtensions.JobProcess<T>.Initialize();

        private static System.IntPtr GetReflectionData<T>() where T : struct, IJobSingle {
            IJobSingleExtensions.JobProcess<T>.Initialize();
            System.IntPtr reflectionData = IJobSingleExtensions.JobProcess<T>.jobReflectionData.Data;
            return reflectionData;
        }

        public static JobHandle ScheduleSingle<T>(this T jobData, JobHandle inputDeps = default) where T : struct, IJobSingle {
            
            var parameters = new JobsUtility.JobScheduleParameters(_addressPtr(ref jobData), GetReflectionData<T>(), inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
            
        }

        public static JobHandle ScheduleSingleByRef<T>(ref this T jobData, JobHandle inputDeps = default) where T : struct, IJobSingle {
            
            var parameters = new JobsUtility.JobScheduleParameters(_addressPtr(ref jobData), GetReflectionData<T>(), inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
            
        }

        internal struct JobProcess<T> where T : struct, IJobSingle {
            
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<IJobSingleExtensions.JobProcess<T>>();

            [Unity.Burst.BurstDiscardAttribute]
            public static void Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(T), (object)new ExecuteJobFunction(Execute));
                }
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