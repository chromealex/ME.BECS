namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryScheduleExtensions {
        
        public static JobHandle ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(this QueryBuilder builder, in T job) where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent {
            builder.With<T0>(); builder.With<T1>(); builder.With<T2>(); builder.With<T3>(); builder.With<T4>(); builder.With<T5>(); builder.With<T6>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(in builder.commandBuffer, builder.parallelForBatch, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent {
            return staticQuery.ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(in job);
        }

        public static JobHandle ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent {
            staticQuery.builderDependsOn = job.ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(in staticQuery.commandBuffer, staticQuery.parallelForBatch, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }
    
    public static partial class EarlyInit {
        public static void DoParallelForComponents<T, T0,T1,T2,T3,T4,T5,T6>()
                where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent
                where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> => JobParallelForComponentsExtensions.JobEarlyInitialize<T, T0,T1,T2,T3,T4,T5,T6>();
    }

    [JobProducerType(typeof(JobParallelForComponentsExtensions.JobProcess<,,,,,,,>))]
    public interface IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> : IJobParallelForComponentsBase where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent {
        void Execute(in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4,ref T5 c5,ref T6 c6);
    }

    public static unsafe partial class JobParallelForComponentsExtensions {
        
        public static void JobEarlyInitialize<T, T0,T1,T2,T3,T4,T5,T6>()
            where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent
            where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> => JobProcess<T, T0,T1,T2,T3,T4,T5,T6>.Initialize();

        private static System.IntPtr GetReflectionData<T, T0,T1,T2,T3,T4,T5,T6>()
            where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent
            where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> {
            JobProcess<T, T0,T1,T2,T3,T4,T5,T6>.Initialize();
            System.IntPtr reflectionData = JobProcess<T, T0,T1,T2,T3,T4,T5,T6>.jobReflectionData.Data;
            return reflectionData;
        }

        public static JobHandle ScheduleParallelFor<T, T0,T1,T2,T3,T4,T5,T6>(this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent
            where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> {
            
            if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount(buffer->count);

            buffer->sync = false;
            var data = new JobData<T, T0,T1,T2,T3,T4,T5,T6>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->components.GetRW<T0>(buffer->state, buffer->worldId),c1 = buffer->state->components.GetRW<T1>(buffer->state, buffer->worldId),c2 = buffer->state->components.GetRW<T2>(buffer->state, buffer->worldId),c3 = buffer->state->components.GetRW<T3>(buffer->state, buffer->worldId),c4 = buffer->state->components.GetRW<T4>(buffer->state, buffer->worldId),c5 = buffer->state->components.GetRW<T5>(buffer->state, buffer->worldId),c6 = buffer->state->components.GetRW<T6>(buffer->state, buffer->worldId),
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_address(ref data), GetReflectionData<T, T0,T1,T2,T3,T4,T5,T6>(), dependsOn, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);

        }

        private struct JobData<T, T0,T1,T2,T3,T4,T5,T6>
            where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public RefRW<T0> c0;public RefRW<T1> c1;public RefRW<T2> c2;public RefRW<T3> c3;public RefRW<T4> c4;public RefRW<T5> c5;public RefRW<T6> c6;
        }

        internal struct JobProcess<T, T0,T1,T2,T3,T4,T5,T6>
            where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent
            where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4,T5,T6> {

            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T, T0,T1,T2,T3,T4,T5,T6>>();

            [BurstDiscard]
            public static System.IntPtr Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1,T2,T3,T4,T5,T6>), typeof(T), (ExecuteJobFunction)Execute);
                }
                return jobReflectionData.Data;
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1,T2,T3,T4,T5,T6> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1,T2,T3,T4,T5,T6> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        var entId = *(jobData.buffer->entities + i);
                        var gen = jobData.buffer->state->entities.GetGeneration(jobData.buffer->state, entId);
                        var ent = new Ent(entId, gen, jobData.buffer->worldId);
                        jobData.jobData.Execute(in ent, ref jobData.c0.Get(entId, gen),ref jobData.c1.Get(entId, gen),ref jobData.c2.Get(entId, gen),ref jobData.c3.Get(entId, gen),ref jobData.c4.Get(entId, gen),ref jobData.c5.Get(entId, gen),ref jobData.c6.Get(entId, gen));
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    
}