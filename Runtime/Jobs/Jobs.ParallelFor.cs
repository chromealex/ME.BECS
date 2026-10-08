namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// Defines the operations required by job parallel for command buffer.
    /// </summary>
    [JobProducerType(typeof(ICommandBufferJobParallelForExtensions.JobProcess<>))]
    public interface IJobParallelForCommandBuffer {
        /// <summary>
        /// Processes i job parallel for command buffer using the supplied job inputs.
        /// </summary>
        void Execute(in CommandBufferJobParallel commandBuffer);
    }

    /// <summary>
    /// Provides extension operations for i command buffer job parallel for.
    /// </summary>
    public static unsafe class ICommandBufferJobParallelForExtensions {
        
        /// <summary>
        /// Registers job reflection data before scheduling.
        /// </summary>
        public static void EarlyJobInit<T>() where T : struct, IJobParallelForCommandBuffer => ICommandBufferJobParallelForExtensions.JobProcess<T>.Initialize();

        private static System.IntPtr GetReflectionData<T>() where T : struct, IJobParallelForCommandBuffer {
            ICommandBufferJobParallelForExtensions.JobProcess<T>.Initialize();
            System.IntPtr reflectionData = ICommandBufferJobParallelForExtensions.JobProcess<T>.jobReflectionData.Data;
            return reflectionData;
        }

        /// <summary>
        /// Schedules the supplied job after its input dependency and returns a handle to the resulting work.
        /// </summary>
        public static JobHandle Schedule<T>(this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, JobHandle inputDeps = default) where T : struct, IJobParallelForCommandBuffer {

            if (innerLoopBatchCount == 0u) innerLoopBatchCount = 64u;
            
            buffer->sync = false;
            var data = new JobData<T> {
                jobData = jobData,
                buffer = buffer,
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_addressPtr(ref data), GetReflectionData<T>(), inputDeps, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            
        }

        /// <summary>
        /// Schedules by ref.
        /// </summary>
        public static JobHandle ScheduleByRef<T>(ref this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, JobHandle inputDeps = default) where T : struct, IJobParallelForCommandBuffer {

            if (innerLoopBatchCount == 0u) innerLoopBatchCount = 64u;
            
            buffer->sync = false;
            var data = new JobData<T> {
                jobData = jobData,
                buffer = buffer,
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_addressPtr(ref data), GetReflectionData<T>(), inputDeps, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            
        }

        private struct JobData<T> where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
        }

        internal struct JobProcess<T> where T : struct, IJobParallelForCommandBuffer {

            public static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T>>();

            [Unity.Burst.BurstDiscardAttribute]
            public static void Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T>), typeof(T), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        var buffer = new CommandBufferJobParallel((safe_ptr)jobData.buffer, i);
                        jobData.jobData.Execute(in buffer);
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    
}