namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1,T2,T3>(this QueryBuilder builder, in T job = default) where T : struct, IJobComponents<T0,T1,T2,T3> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase {
            builder.With<T0>(); builder.With<T1>(); builder.With<T2>(); builder.With<T3>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, T0,T1,T2,T3>(in builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobComponents<T0,T1,T2,T3> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase {
            return staticQuery.Schedule<T, T0,T1,T2,T3>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobComponents<T0,T1,T2,T3> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1,T2,T3>(in job);
        }

        public static JobHandle Schedule<T, T0,T1,T2,T3>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobComponents<T0,T1,T2,T3> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1,T2,T3>(in staticQuery.commandBuffer, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    public static partial class EarlyInit {
        public static void DoComponents<T, T0,T1,T2,T3>()
                where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase
                where T : struct, IJobComponents<T0,T1,T2,T3> => JobComponentsExtensions.JobEarlyInitialize<T, T0,T1,T2,T3>();
    }

    [JobProducerType(typeof(JobComponentsExtensions.JobProcess<,,,,>))]
    public interface IJobComponents<T0,T1,T2,T3> : IJobComponentsBase where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3);
    }

    public static unsafe partial class JobComponentsExtensions {
        
        public static void JobEarlyInitialize<T, T0,T1,T2,T3>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1,T2,T3> => JobProcess<T, T0,T1,T2,T3>.Initialize();

        private static System.IntPtr GetReflectionData<T, T0,T1,T2,T3>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1,T2,T3> {
            JobProcess<T, T0,T1,T2,T3>.Initialize();
            System.IntPtr reflectionData = JobProcess<T, T0,T1,T2,T3>.jobReflectionData.Data;
            return reflectionData;
        }

        public static JobHandle Schedule<T, T0,T1,T2,T3>(this T jobData, in CommandBuffer* buffer, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1,T2,T3> {
            
            buffer->sync = false;
            var data = new JobData<T, T0,T1,T2,T3>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->components.GetRW<T0>(buffer->state, buffer->worldId),c1 = buffer->state->components.GetRW<T1>(buffer->state, buffer->worldId),c2 = buffer->state->components.GetRW<T2>(buffer->state, buffer->worldId),c3 = buffer->state->components.GetRW<T3>(buffer->state, buffer->worldId),
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_address(ref data), GetReflectionData<T, T0,T1,T2,T3>(), dependsOn, ScheduleMode.Parallel);
            return JobsUtility.Schedule(ref parameters);

        }

        private struct JobData<T, T0,T1,T2,T3>
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public RefRW<T0> c0;public RefRW<T1> c1;public RefRW<T2> c2;public RefRW<T3> c3;
        }

        internal struct JobProcess<T, T0,T1,T2,T3>
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1,T2,T3> {

            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T, T0,T1,T2,T3>>();

            [BurstDiscard]
            public static void Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1,T2,T3>), typeof(T), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1,T2,T3> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1,T2,T3> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
            
                var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                jobInfo.count = jobData.buffer->count;
                
                JobUtils.SetCurrentThreadAsSingle(true);
                
                jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                for (uint i = 0u; i < jobData.buffer->count; ++i) {
                    jobInfo.index = i;
                    var entId = *(jobData.buffer->entities + i);
                    var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                    var ent = new Ent(entId, gen, jobData.buffer->worldId);
                    jobData.jobData.Execute(in jobInfo, in ent, ref jobData.c0.Get(ent.id, ent.gen),ref jobData.c1.Get(ent.id, ent.gen),ref jobData.c2.Get(ent.id, ent.gen),ref jobData.c3.Get(ent.id, ent.gen));
                }
                jobData.buffer->EndForEachRange();
                
                JobUtils.SetCurrentThreadAsSingle(false);
                
            }
        }
    }
    
}