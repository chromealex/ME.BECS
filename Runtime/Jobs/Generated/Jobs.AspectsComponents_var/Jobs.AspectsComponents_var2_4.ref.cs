namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions {
        
        public static JobHandle Schedule<T, A0,A1, C0,C1,C2,C3>(this QueryBuilder builder, in T job = default) where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
            builder.WithAspect<A0>(); builder.WithAspect<A1>();
            builder.With<C0>(); builder.With<C1>(); builder.With<C2>(); builder.With<C3>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, A0,A1, C0,C1,C2,C3>(in builder.commandBuffer, builder.isUnsafe, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, A0,A1, C0,C1,C2,C3>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
            return staticQuery.Schedule<T, A0,A1, C0,C1,C2,C3>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, A0,A1, C0,C1,C2,C3>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, A0,A1, C0,C1,C2,C3>(in job);
        }

        public static JobHandle Schedule<T, A0,A1, C0,C1,C2,C3>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
            staticQuery.builderDependsOn = job.Schedule<T, A0,A1, C0,C1,C2,C3>(in staticQuery.commandBuffer, staticQuery.isUnsafe, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }
    
    public static partial class EarlyInit {
        public static void DoAspectsComponents<T, A0,A1, C0,C1,C2,C3>()
                where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
                where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> => JobAspectsComponentsExtensions.JobEarlyInitialize<T, A0,A1, C0,C1,C2,C3>();
    }

    [JobProducerType(typeof(JobAspectsComponentsExtensions.JobProcess<,,,,,,>))]
    public interface IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0,ref A1 a1, ref C0 c0,ref C1 c1,ref C2 c2,ref C3 c3);
    }

    public static unsafe partial class JobAspectsComponentsExtensions {
        
        public static void JobEarlyInitialize<T, A0,A1, C0,C1,C2,C3>()
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> => JobProcess<T, A0,A1, C0,C1,C2,C3>.Initialize();

        private static System.IntPtr GetReflectionData<T, A0,A1, C0,C1,C2,C3>()
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> {
            JobProcess<T, A0,A1, C0,C1,C2,C3>.Initialize();
            System.IntPtr reflectionData = JobProcessData<T, A0,A1, C0,C1,C2,C3>.jobReflectionData.Data;
            return reflectionData;
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        private static System.IntPtr GetReflectionUnsafeData<T, A0,A1, C0,C1,C2,C3>()
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> {
            JobProcess<T, A0,A1, C0,C1,C2,C3>.Initialize();
            System.IntPtr reflectionData = JobProcessUnsafeData<T, A0,A1, C0,C1,C2,C3>.jobReflectionData.Data;
            return reflectionData;
        }
        #endif

        public static JobHandle Schedule<T, A0,A1, C0,C1,C2,C3>(this T jobData, in CommandBuffer* buffer, bool unsafeMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> {
            
            buffer->sync = false;
            void* data = null;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            data = CompiledJobs<T>.Get(ref jobData, buffer, unsafeMode);
            var parameters = new JobsUtility.JobScheduleParameters(data, unsafeMode == true ? GetReflectionUnsafeData<T, A0,A1, C0,C1,C2,C3>() : GetReflectionData<T, A0,A1, C0,C1,C2,C3>(), dependsOn, ScheduleMode.Single);
            #else
            var dataVal = new JobData<T, A0,A1, C0,C1,C2,C3>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->aspectsStorage.Initialize<A0>(buffer->state),c1 = buffer->state->aspectsStorage.Initialize<A1>(buffer->state),
                c0 = buffer->state->components.GetRW<C0>(buffer->state, buffer->worldId),c1 = buffer->state->components.GetRW<C1>(buffer->state, buffer->worldId),c2 = buffer->state->components.GetRW<C2>(buffer->state, buffer->worldId),c3 = buffer->state->components.GetRW<C3>(buffer->state, buffer->worldId),
            };
            data = _address(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, GetReflectionData<T, A0,A1, C0,C1,C2,C3>(), dependsOn, ScheduleMode.Single);
            #endif
            
            return JobsUtility.Schedule(ref parameters);
            
        }

        private struct JobData<T, A0,A1, C0,C1,C2,C3>
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public A0 a0;public A1 a1;
            public RefRW<C0> c0;public RefRW<C1> c1;public RefRW<C2> c2;public RefRW<C3> c3;
        }

        internal struct JobProcessData<T, A0,A1, C0,C1,C2,C3> {
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcessData<T, A0,A1, C0,C1,C2,C3>>();
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        internal struct JobProcessUnsafeData<T, A0,A1, C0,C1,C2,C3> {
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcessUnsafeData<T, A0,A1, C0,C1,C2,C3>>();
        }
        #endif

        internal struct JobProcess<T, A0,A1, C0,C1,C2,C3>
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobForAspectsComponents<A0,A1, C0,C1,C2,C3> {

            [BurstDiscard]
            public static void Initialize() {
                if (JobProcessData<T, A0,A1, C0,C1,C2,C3>.jobReflectionData.Data == System.IntPtr.Zero) {
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    JobProcessData<T, A0,A1, C0,C1,C2,C3>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(false), typeof(T), (ExecuteJobFunction)Execute);
                    JobProcessUnsafeData<T, A0,A1, C0,C1,C2,C3>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(true), typeof(T), (ExecuteJobFunction)Execute);
                    #else
                    JobProcessData<T, A0,A1, C0,C1,C2,C3>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, A0,A1, C0,C1,C2,C3>), typeof(T), (ExecuteJobFunction)Execute);
                    #endif
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, A0,A1, C0,C1,C2,C3> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, A0,A1, C0,C1,C2,C3> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                jobInfo.count = jobData.buffer->count;
                var aspect0 = jobData.a0;var aspect1 = jobData.a1;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        jobInfo.index = i;
                        var entId = *(jobData.buffer->entities + i);
                        var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                        var ent = new Ent(entId, gen, jobData.buffer->worldId);
                        aspect0.ent = new Ent(entId, gen, jobData.buffer->worldId);aspect1.ent = new Ent(entId, gen, jobData.buffer->worldId);
                        jobData.jobData.Execute(in jobInfo, in ent, ref aspect0,ref aspect1, ref jobData.c0.Get(ent.id, ent.gen),ref jobData.c1.Get(ent.id, ent.gen),ref jobData.c2.Get(ent.id, ent.gen),ref jobData.c3.Get(ent.id, ent.gen));
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    
}