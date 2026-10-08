namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    /// <summary>
    /// Defines the operations required by job command buffer.
    /// </summary>
    [JobProducerType(typeof(ICommandBufferJobExtensions.JobProcess<>))]
    public interface IJobCommandBuffer {
        /// <summary>
        /// Processes i job command buffer using the supplied job inputs.
        /// </summary>
        void Execute(in CommandBufferJob commandBuffer);
    }

    /// <summary>
    /// Provides extension operations for i command buffer job.
    /// </summary>
    public static unsafe class ICommandBufferJobExtensions {
        
        /// <summary>
        /// Registers job reflection data before the job is scheduled.
        /// </summary>
        public static void JobEarlyInit<T>() where T : struct, IJobCommandBuffer => JobProcess<T>.Initialize();

        private static System.IntPtr GetReflectionData<T>()
            where T : struct, IJobCommandBuffer {
            JobProcess<T>.Initialize();
            System.IntPtr reflectionData = JobProcess<T>.jobReflectionData.Data;
            return reflectionData;
        }
        
        /// <summary>
        /// Schedules the supplied job after its input dependency and returns a handle to the resulting work.
        /// </summary>
        public static JobHandle Schedule<T>(this T jobData, in CommandBuffer* buffer, JobHandle inputDeps = default) where T : struct, IJobCommandBuffer {
            
            buffer->sync = true;
            var data = new JobData<T> {
                jobData = jobData,
                buffer = buffer,
            };

            var parameters = new JobsUtility.JobScheduleParameters(_addressPtr(ref data), GetReflectionData<T>(), inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
            
        }

        /// <summary>
        /// Schedules by ref.
        /// </summary>
        public static JobHandle ScheduleByRef<T>(ref this T jobData, in CommandBuffer* buffer, JobHandle inputDeps = default) where T : struct, IJobCommandBuffer {
            
            buffer->sync = true;
            var data = new JobData<T> {
                jobData = jobData,
                buffer = buffer,
            };

            var parameters = new JobsUtility.JobScheduleParameters(_addressPtr(ref data), GetReflectionData<T>(), inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
            
        }

        internal struct JobData<T> where T : struct {
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
        }

        internal struct JobProcess<T> where T : struct, IJobCommandBuffer {
            
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T>>();

            [BurstDiscard]
            public static void Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T>), typeof(T), (ExecuteJobFunction)Execute);
                }
            }

            public delegate void ExecuteJobFunction(ref JobData<T> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public static void Execute(ref JobData<T> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                JobUtils.SetCurrentThreadAsSingle(true);
                
                jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                for (int i = 0; i < jobData.buffer->count; ++i) {
                    var entId = jobData.buffer->entities[i];
                    var entGen = Ents.GetGeneration(jobData.buffer->state, entId);
                    var commandBuffer = new CommandBufferJob(entId, entGen, (safe_ptr)jobData.buffer);
                    jobData.jobData.Execute(in commandBuffer);
                }
                jobData.buffer->EndForEachRange();
                
                JobUtils.SetCurrentThreadAsSingle(false);
                
            }
        }
    }
    
}