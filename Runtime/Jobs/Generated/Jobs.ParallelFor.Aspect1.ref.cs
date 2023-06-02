namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    public static unsafe partial class QueryAspectScheduleExtensions {
        
        public static JobHandle ScheduleParallelFor<T, T0>(this QueryBuilder builder, in T job) where T : struct, IJobParallelForAspect<T0> where T0 : unmanaged, IAspect {
            builder.WithAspect<T0>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.ScheduleParallelFor<T, T0>(in builder.commandBuffer, builder.parallelForBatch, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle ScheduleParallelFor<T, T0>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobParallelForAspect<T0> where T0 : unmanaged, IAspect {
            return staticQuery.ScheduleParallelFor<T, T0>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle ScheduleParallelFor<T, T0>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobParallelForAspect<T0> where T0 : unmanaged, IAspect {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.ScheduleParallelFor<T, T0>(in job);
        }

        public static JobHandle ScheduleParallelFor<T, T0>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobParallelForAspect<T0> where T0 : unmanaged, IAspect {
            staticQuery.builderDependsOn = job.ScheduleParallelFor<T, T0>(in staticQuery.commandBuffer, staticQuery.parallelForBatch, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    [JobProducerType(typeof(JobParallelForAspectExtensions_1.JobProcess<,>))]
    public interface IJobParallelForAspect<T0> where T0 : unmanaged, IAspect {
        void Execute(ref T0 c0);
    }

    public static unsafe partial class JobParallelForAspectExtensions_1 {
        
        public static JobHandle ScheduleParallelFor<T, T0>(this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect
            where T : struct, IJobParallelForAspect<T0> {
            
            if (innerLoopBatchCount == 0u) innerLoopBatchCount = 64u;

            buffer->sync = false;
            var data = new JobData<T, T0>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->aspectsStorage.Initialize<T0>(buffer->state),
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), JobProcess<T, T0>.Initialize(), dependsOn, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);

        }

        private struct JobData<T, T0>
            where T0 : unmanaged, IAspect
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public T0 c0;
        }

        internal struct JobProcess<T, T0>
            where T0 : unmanaged, IAspect
            where T : struct, IJobParallelForAspect<T0> {

            private static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T, T0>>();

            public static System.IntPtr Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0>), typeof(T), (ExecuteJobFunction)Execute);
                }
                return jobReflectionData.Data;
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        var entId = *(jobData.buffer->entities + i);
                        var gen = jobData.buffer->state->entities.GetGeneration(jobData.buffer->state, entId);
                        jobData.c0.ent = new Ent(entId, gen, jobData.buffer->worldId);
                        jobData.jobData.Execute(ref jobData.c0);
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    
}