namespace ME.BECS.Jobs {

    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;
    using static Cuts;

    #if !(UNITY_EDITOR || ENABLE_PROFILER || DEVELOPMENT_BUILD)
    /// <summary>
    /// Defines the operations required by job main thread.
    /// </summary>
    [JobProducerType(typeof(JobMainThreadExtensions.JobProcess<>))]
    #endif
    /// <summary>
    /// This type of job <b>trying</b> to run Execute method on main-thread
    /// </summary>
    /// <case name="editor">In editor: dependsOn will be complete</case>
    /// <case name="build">In build: Schedule will be called in parallel mode as much as it can</case>
    public interface IJobMainThread {
        /// <summary>
        /// Processes i job main thread using the supplied job inputs.
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// Provides extension operations for job main thread.
    /// </summary>
    public static unsafe class JobMainThreadExtensions {

        #if !(UNITY_EDITOR || ENABLE_PROFILER || DEVELOPMENT_BUILD)
        /// <summary>
        /// Registers job reflection data before the job is scheduled.
        /// </summary>
        public static void JobEarlyInit<T>() where T : struct, IJobMainThread => JobProcess<T>.Initialize();

        private static System.IntPtr GetReflectionData<T>()
            where T : struct, IJobMainThread {
            JobProcess<T>.Initialize();
            System.IntPtr reflectionData = JobProcess<T>.jobReflectionData.Data;
            return reflectionData;
        }
        #endif

        /// <summary>
        /// Schedules the supplied job after its input dependency and returns a handle to the resulting work.
        /// </summary>
        public static JobHandle Schedule<T>(this T jobData, JobHandle dependsOn = default)
            where T : struct, IJobMainThread {
            
            #if UNITY_EDITOR || ENABLE_PROFILER || DEVELOPMENT_BUILD
            dependsOn.Complete();
            jobData.Execute();
            return dependsOn;
            #else
            var data = new JobData<T>() {
                jobData = jobData,
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_addressPtr(ref data), GetReflectionData<T>(), dependsOn, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelFor(ref parameters, JobsUtility.MaxJobThreadCount, 1);
            #endif

        }

        #if !(UNITY_EDITOR || ENABLE_PROFILER || DEVELOPMENT_BUILD)
        private struct JobData<T>
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
        }

        internal struct JobProcess<T>
            where T : struct, IJobMainThread {

            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T>>();

            [BurstDiscard]
            public static void Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T>), typeof(T), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                if (JobsUtility.ThreadIndex != 0) return;
                jobData.jobData.Execute();

            }
        }
        #endif
    }

}