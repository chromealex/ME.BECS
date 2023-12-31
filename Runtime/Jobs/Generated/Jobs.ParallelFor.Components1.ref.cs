namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryScheduleExtensions {
        
        public static JobHandle ScheduleParallelFor<T, T0>(this QueryBuilder builder, in T job) where T : struct, IJobParallelForComponents<T0> where T0 : unmanaged, IComponent {
            builder.With<T0>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.ScheduleParallelFor<T, T0>(in builder.commandBuffer, builder.parallelForBatch, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle ScheduleParallelFor<T, T0>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobParallelForComponents<T0> where T0 : unmanaged, IComponent {
            return staticQuery.ScheduleParallelFor<T, T0>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle ScheduleParallelFor<T, T0>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobParallelForComponents<T0> where T0 : unmanaged, IComponent {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.ScheduleParallelFor<T, T0>(in job);
        }

        public static JobHandle ScheduleParallelFor<T, T0>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobParallelForComponents<T0> where T0 : unmanaged, IComponent {
            staticQuery.builderDependsOn = job.ScheduleParallelFor<T, T0>(in staticQuery.commandBuffer, staticQuery.parallelForBatch, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }
    
    public static partial class EarlyInit {
        public static void DoParallelForComponents<T, T0>()
                where T0 : unmanaged, IComponent
                where T : struct, IJobParallelForComponents<T0> => JobParallelForComponentsExtensions.JobEarlyInitialize<T, T0>();
    }

    [JobProducerType(typeof(JobParallelForComponentsExtensions.JobProcess<,>))]
    public interface IJobParallelForComponents<T0> : IJobParallelForComponentsBase where T0 : unmanaged, IComponent {
        void Execute(in Ent ent, ref T0 c0);
    }

    public static unsafe partial class JobParallelForComponentsExtensions {
        
        public static void JobEarlyInitialize<T, T0>()
            where T0 : unmanaged, IComponent
            where T : struct, IJobParallelForComponents<T0> => JobProcess<T, T0>.Initialize();

        private static System.IntPtr GetReflectionData<T, T0>()
            where T0 : unmanaged, IComponent
            where T : struct, IJobParallelForComponents<T0> {
            JobProcess<T, T0>.Initialize();
            System.IntPtr reflectionData = JobProcess<T, T0>.jobReflectionData.Data;
            return reflectionData;
        }

        public static JobHandle ScheduleParallelFor<T, T0>(this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponent
            where T : struct, IJobParallelForComponents<T0> {
            
            if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount(buffer->count);

            buffer->sync = false;
            var data = new JobData<T, T0>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->components.GetRW<T0>(buffer->state, buffer->worldId),
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_address(ref data), GetReflectionData<T, T0>(), dependsOn, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);

        }

        private struct JobData<T, T0>
            where T0 : unmanaged, IComponent
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public RefRW<T0> c0;
        }

        internal struct JobProcess<T, T0>
            where T0 : unmanaged, IComponent
            where T : struct, IJobParallelForComponents<T0> {

            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T, T0>>();

            [BurstDiscard]
            public static void Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0>), typeof(T), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        var entId = *(jobData.buffer->entities + i);
                        var gen = jobData.buffer->state->entities.GetGeneration(jobData.buffer->state, entId);
                        var ent = new Ent(entId, gen, jobData.buffer->worldId);
                        jobData.jobData.Execute(in ent, ref jobData.c0.Get(entId, gen));
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    
}