namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryAspectParallelScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1>(this QueryBuilder builder, in T job = default) where T : struct, IJobParallelForAspect<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
            builder.WithAspect<T0>(); builder.WithAspect<T1>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, T0,T1>(in builder.commandBuffer, builder.parallelForBatch, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobParallelForAspect<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
            return staticQuery.Schedule<T, T0,T1>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobParallelForAspect<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1>(in job);
        }

        public static JobHandle Schedule<T, T0,T1>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobParallelForAspect<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1>(in staticQuery.commandBuffer, staticQuery.parallelForBatch, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    public static partial class EarlyInit {
        public static void DoParallelForAspect<T, T0,T1>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
                where T : struct, IJobParallelForAspect<T0,T1> => JobParallelForAspectExtensions.JobEarlyInitialize<T, T0,T1>();
    }

    [JobProducerType(typeof(JobParallelForAspectExtensions.JobProcess<,,>))]
    public interface IJobParallelForAspect<T0,T1> : IJobParallelForAspectBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, ref T0 c0,ref T1 c1);
    }

    public static unsafe partial class JobParallelForAspectExtensions {
        
        public static void JobEarlyInitialize<T, T0,T1>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T : struct, IJobParallelForAspect<T0,T1> => JobProcess<T, T0,T1>.Initialize();
        
        private static System.IntPtr GetReflectionData<T, T0,T1>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T : struct, IJobParallelForAspect<T0,T1> {
            JobProcess<T, T0,T1>.Initialize();
            System.IntPtr reflectionData = JobProcess<T, T0,T1>.jobReflectionData.Data;
            return reflectionData;
        }

        public static JobHandle Schedule<T, T0,T1>(this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct, IJobParallelForAspect<T0,T1> {
            
            dependsOn = new StartParallelJob() {
                            buffer = buffer,
                        }.ScheduleSingle(dependsOn);
                        
            if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount(buffer->count);

            buffer->sync = false;
            var data = new JobData<T, T0,T1>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->aspectsStorage.Initialize<T0>(buffer->state),c1 = buffer->state->aspectsStorage.Initialize<T1>(buffer->state),
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_address(ref data), GetReflectionData<T, T0,T1>(), dependsOn, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);

        }

        private struct JobData<T, T0,T1>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public T0 c0;public T1 c1;
        }

        internal struct JobProcess<T, T0,T1>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct, IJobParallelForAspect<T0,T1> {

            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T, T0,T1>>();

            [BurstDiscard]
            public static void Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1>), typeof(T), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                jobInfo.count = jobData.buffer->count;
                var aspect0 = jobData.c0;var aspect1 = jobData.c1;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        jobInfo.index = i;
                        var entId = *(jobData.buffer->entities + i);
                        var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                        aspect0.ent = new Ent(entId, gen, jobData.buffer->worldId);aspect1.ent = new Ent(entId, gen, jobData.buffer->worldId);
                        jobData.jobData.Execute(in jobInfo, ref aspect0,ref aspect1);
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    
}