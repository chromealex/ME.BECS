namespace ME.BECS {
    
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    public unsafe delegate void* CompiledJobCallback(void* jobData, CommandBuffer* buffer, bool unsafeMode);

    public unsafe class CompiledJobs<TJob> where TJob : struct {

        public static readonly SharedStatic<FunctionPointer<CompiledJobCallback>> jobDataFunction = SharedStatic<FunctionPointer<CompiledJobCallback>>.GetOrCreate<CompiledJobs<TJob>>();
        private static System.Func<bool, System.Type> getTypeFunction;
        
        public static void SetFunction(FunctionPointer<CompiledJobCallback> callback, System.Func<bool, System.Type> getType) {
            jobDataFunction.Data = callback;
            getTypeFunction = getType;
        }

        public static void* Get(ref TJob jobData, CommandBuffer* buffer, bool unsafeMode) {
            return jobDataFunction.Data.Invoke(_address(ref jobData), buffer, unsafeMode);
        }

        public static System.Type GetJobType(bool unsafeMode) {
            return getTypeFunction?.Invoke(unsafeMode);
        }

    }
    
}