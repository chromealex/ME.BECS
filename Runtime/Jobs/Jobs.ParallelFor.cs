namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    [JobProducerType(typeof(ICommandBufferJobParallelForExtensions.JobProcess<>))]
    public interface IJobParallelForCommandBuffer {
        void Execute(in CommandBufferJobParallel commandBuffer);
    }

    public static unsafe class ICommandBufferJobParallelForExtensions {
        
        public static JobHandle Schedule<T>(this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, JobHandle inputDeps = default) where T : struct, IJobParallelForCommandBuffer {

            if (innerLoopBatchCount == 0u) innerLoopBatchCount = 64u;
            
            buffer->sync = false;
            var data = new JobData<T> {
                jobData = jobData,
                buffer = buffer,
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), JobProcess<T>.Initialize(), inputDeps, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            
        }

        public static JobHandle ScheduleByRef<T>(ref this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, JobHandle inputDeps = default) where T : struct, IJobParallelForCommandBuffer {

            if (innerLoopBatchCount == 0u) innerLoopBatchCount = 64u;
            
            buffer->sync = false;
            var data = new JobData<T> {
                jobData = jobData,
                buffer = buffer,
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), JobProcess<T>.Initialize(), inputDeps, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            
        }

        private struct JobData<T> where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
        }

        internal struct JobProcess<T> where T : struct, IJobParallelForCommandBuffer {

            private static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T>>();

            public static System.IntPtr Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T>), typeof(T), (ExecuteJobFunction)Execute);
                }
                return jobReflectionData.Data;
            }

            private delegate void ExecuteJobFunction(ref JobData<T> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        var buffer = new CommandBufferJobParallel(jobData.buffer, i);
                        jobData.jobData.Execute(in buffer);
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    
}