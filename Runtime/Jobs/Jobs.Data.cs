namespace ME.BECS {
    
    using Unity.Burst;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary>
    /// Defines the callback signature for compiled job callback.
    /// </summary>
    public unsafe delegate void* CompiledJobCallback(void* jobData, CommandBuffer* buffer, bool unsafeMode, ScheduleFlags scheduleFlags, in JobInfo jobInfo);

    /// <summary>
    /// Provides compiled job execution entry points and metadata.
    /// </summary>
    public unsafe class CompiledJobs<TJob> where TJob : struct {

        private static readonly SharedStatic<FunctionPointer<CompiledJobCallback>> jobDataFunction = SharedStatic<FunctionPointer<CompiledJobCallback>>.GetOrCreate<CompiledJobs<TJob>>();
        private static System.Func<bool, System.Type> getTypeFunction;
        
        /// <summary>
        /// Sets function.
        /// </summary>
        public static void SetFunction(FunctionPointer<CompiledJobCallback> callback, System.Func<bool, System.Type> getType) {
            //if (jobDataFunction.Data.IsCreated == true) throw new System.Exception($"Function is already set for job type {typeof(TJob)}");
            jobDataFunction.Data = callback;
            getTypeFunction = getType;
        }

        /// <summary>
        /// Returns the requested entry from compiled jobs.
        /// </summary>
        public static void* Get(void* jobDataAddr, CommandBuffer* buffer, bool unsafeMode, ScheduleFlags scheduleFlags, in JobInfo jobInfo) {
            return jobDataFunction.Data.Invoke(jobDataAddr, buffer, unsafeMode, scheduleFlags, in jobInfo);
        }

        /// <summary>
        /// Returns job type.
        /// </summary>
        public static System.Type GetJobType(bool unsafeMode) {
            return getTypeFunction?.Invoke(unsafeMode);
        }

    }
    
}