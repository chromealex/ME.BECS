namespace ME.BECS {
    
    using Unity.Burst;
    using Unity.Jobs.LowLevel.Unsafe;

    public unsafe delegate void* CompiledJobCallback(void* jobData, CommandBuffer* buffer, bool unsafeMode, ScheduleFlags scheduleFlags);

    public unsafe class CompiledJobs<TJob> where TJob : struct {

        private static readonly SharedStatic<FunctionPointer<CompiledJobCallback>> jobDataFunction = SharedStatic<FunctionPointer<CompiledJobCallback>>.GetOrCreate<CompiledJobs<TJob>>();
        private static System.Func<bool, System.Type> getTypeFunction;
        
        public static void SetFunction(FunctionPointer<CompiledJobCallback> callback, System.Func<bool, System.Type> getType) {
            //if (jobDataFunction.Data.IsCreated == true) throw new System.Exception($"Function is already set for job type {typeof(TJob)}");
            jobDataFunction.Data = callback;
            getTypeFunction = getType;
        }

        public static void* Get(void* jobDataAddr, CommandBuffer* buffer, bool unsafeMode, ScheduleFlags scheduleFlags) {
            return jobDataFunction.Data.Invoke(jobDataAddr, buffer, unsafeMode, scheduleFlags);
        }

        public static System.Type GetJobType(bool unsafeMode) {
            return getTypeFunction?.Invoke(unsafeMode);
        }

    }
    
}