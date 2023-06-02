namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    [JobProducerType(typeof(ICommandBufferJobExtensions.JobProcess<>))]
    public interface IJobCommandBuffer {
        void Execute(in CommandBufferJob commandBuffer);
    }

    public static unsafe class ICommandBufferJobExtensions {
        
        public static JobHandle Schedule<T>(this T jobData, in CommandBuffer* buffer, JobHandle inputDeps = default) where T : struct, IJobCommandBuffer {
            
            buffer->sync = true;
            var data = new JobData<T> {
                jobData = jobData,
                buffer = buffer,
            };

            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), JobProcess<T>.Initialize(), inputDeps, ScheduleMode.Parallel);
            return JobsUtility.Schedule(ref parameters);
            
        }

        public static JobHandle ScheduleByRef<T>(ref this T jobData, in CommandBuffer* buffer, JobHandle inputDeps = default) where T : struct, IJobCommandBuffer {
            
            buffer->sync = true;
            var data = new JobData<T> {
                jobData = jobData,
                buffer = buffer,
            };

            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), JobProcess<T>.Initialize(), inputDeps, ScheduleMode.Parallel);
            return JobsUtility.Schedule(ref parameters);
            
        }

        internal struct JobData<T> where T : struct {
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
        }

        internal struct JobProcess<T> where T : struct, IJobCommandBuffer {
            
            private static System.IntPtr jobReflectionData;

            public static System.IntPtr Initialize() {
                if (jobReflectionData == System.IntPtr.Zero) {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobData<T>), typeof(T), (ExecuteJobFunction)Execute);
                }
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobData<T> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public static void Execute(ref JobData<T> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                JobUtils.SetCurrentThreadAsSingle(true);
                
                jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                for (int i = 0; i < jobData.buffer->count; ++i) {
                    var entId = jobData.buffer->entities[i];
                    var entGen = jobData.buffer->state->entities.GetGeneration(jobData.buffer->state, entId);
                    var commandBuffer = new CommandBufferJob(entId, entGen, jobData.buffer);
                    jobData.jobData.Execute(in commandBuffer);
                }
                jobData.buffer->EndForEachRange();
                
                JobUtils.SetCurrentThreadAsSingle(false);
                
            }
        }
    }
    
}