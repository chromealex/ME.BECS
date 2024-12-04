namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1>(this QueryBuilder builder, in T job = default) where T : struct, IJobComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
            builder.With<T0>(); builder.With<T1>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, T0,T1>(in builder.commandBuffer, builder.isUnsafe, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
            return staticQuery.Schedule<T, T0,T1>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1>(in job);
        }

        public static JobHandle Schedule<T, T0,T1>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1>(in staticQuery.commandBuffer, staticQuery.isUnsafe, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    public static partial class EarlyInit {
        public static void DoComponents<T, T0,T1>()
                where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
                where T : struct, IJobComponents<T0,T1> => JobComponentsExtensions.JobEarlyInitialize<T, T0,T1>();
    }

    [JobProducerType(typeof(JobComponentsExtensions.JobProcess<,,>))]
    public interface IJobComponents<T0,T1> : IJobComponentsBase where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1);
    }

    public static unsafe partial class JobComponentsExtensions {
        
        public static void JobEarlyInitialize<T, T0,T1>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1> => JobProcess<T, T0,T1>.Initialize();

        private static System.IntPtr GetReflectionData<T, T0,T1>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1> {
            JobProcess<T, T0,T1>.Initialize();
            System.IntPtr reflectionData = JobProcessData<T, T0,T1>.jobReflectionData.Data;
            return reflectionData;
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        private static System.IntPtr GetReflectionUnsafeData<T, T0,T1>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1> {
            JobProcess<T, T0,T1>.Initialize();
            System.IntPtr reflectionData = JobProcessUnsafeData<T, T0,T1>.jobReflectionData.Data;
            return reflectionData;
        }
        #endif

        public static JobHandle Schedule<T, T0,T1>(this T jobData, in CommandBuffer* buffer, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1> {
            
            buffer->sync = false;
            void* data = null;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            data = CompiledJobs<T>.Get(ref jobData, buffer, unsafeMode);
            var parameters = new JobsUtility.JobScheduleParameters(data, unsafeMode == true ? GetReflectionUnsafeData<T, T0,T1>() : GetReflectionData<T, T0,T1>(), dependsOn, ScheduleMode.Single);
            #else
            var dataVal = new JobData<T, T0,T1>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->components.GetRW<T0>(buffer->state, buffer->worldId),c1 = buffer->state->components.GetRW<T1>(buffer->state, buffer->worldId),
            };
            data = _address(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, GetReflectionData<T, T0,T1>(), dependsOn, ScheduleMode.Single);
            #endif
            
            return JobsUtility.Schedule(ref parameters);
            
        }

        private struct JobData<T, T0,T1>
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public RefRW<T0> c0;public RefRW<T1> c1;
        }

        internal struct JobProcessData<T, T0,T1> {
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcessData<T, T0,T1>>();
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        internal struct JobProcessUnsafeData<T, T0,T1> {
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcessUnsafeData<T, T0,T1>>();
        }
        #endif

        internal struct JobProcess<T, T0,T1>
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobComponents<T0,T1> {

            [BurstDiscard]
            public static void Initialize() {
                if (JobProcessData<T, T0,T1>.jobReflectionData.Data == System.IntPtr.Zero) {
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    JobProcessData<T, T0,T1>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(false), typeof(T), (ExecuteJobFunction)Execute);
                    JobProcessUnsafeData<T, T0,T1>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(true), typeof(T), (ExecuteJobFunction)Execute);
                    #else
                    JobProcessData<T, T0,T1>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1>), typeof(T), (ExecuteJobFunction)Execute);
                    #endif
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
            
                var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                jobInfo.count = jobData.buffer->count;
                
                JobUtils.SetCurrentThreadAsSingle(true);
                
                jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                for (uint i = 0u; i < jobData.buffer->count; ++i) {
                    jobInfo.index = i;
                    var entId = *(jobData.buffer->entities + i);
                    var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                    var ent = new Ent(entId, gen, jobData.buffer->worldId);
                    jobData.jobData.Execute(in jobInfo, in ent, ref jobData.c0.Get(ent.id, ent.gen),ref jobData.c1.Get(ent.id, ent.gen));
                }
                jobData.buffer->EndForEachRange();
                
                JobUtils.SetCurrentThreadAsSingle(false);
                
            }
        }
    }
    
}