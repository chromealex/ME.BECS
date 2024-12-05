namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryAspectScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1>(this QueryBuilder builder, in T job = default) where T : struct, IJobForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
            builder.WithAspect<T0>(); builder.WithAspect<T1>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, T0,T1>(in builder.commandBuffer, builder.isUnsafe, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
            return staticQuery.Schedule<T, T0,T1>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1>(in job);
        }

        public static JobHandle Schedule<T, T0,T1>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1>(in staticQuery.commandBuffer, staticQuery.isUnsafe, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    public static partial class EarlyInit {
        public static void DoAspect<T, T0,T1>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
                where T : struct, IJobForAspects<T0,T1> => JobAspectExtensions.JobEarlyInitialize<T, T0,T1>();
    }

    [JobProducerType(typeof(JobAspectExtensions.JobProcess<,,>))]
    public interface IJobForAspects<T0,T1> : IJobForAspectsBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1);
    }

    public static unsafe partial class JobAspectExtensions {
        
        public static void JobEarlyInitialize<T, T0,T1>()
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1> => JobProcess<T, T0,T1>.Initialize();

        private static System.IntPtr GetReflectionData<T, T0,T1>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T : struct, IJobForAspects<T0,T1> {
            JobProcess<T, T0,T1>.Initialize();
            System.IntPtr reflectionData = JobProcessData<T, T0,T1>.jobReflectionData.Data;
            return reflectionData;
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        private static System.IntPtr GetReflectionUnsafeData<T, T0,T1>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T : struct, IJobForAspects<T0,T1> {
            JobProcess<T, T0,T1>.Initialize();
            System.IntPtr reflectionData = JobProcessUnsafeData<T, T0,T1>.jobReflectionData.Data;
            return reflectionData;
        }
        #endif

        public static JobHandle Schedule<T, T0,T1>(this T jobData, in CommandBuffer* buffer, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1> {
            
            buffer->sync = false;
            void* data = null;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            data = CompiledJobs<T>.Get(ref jobData, buffer, unsafeMode);
            var parameters = new JobsUtility.JobScheduleParameters(data, unsafeMode == true ? GetReflectionUnsafeData<T, T0,T1>() : GetReflectionData<T, T0,T1>(), dependsOn, ScheduleMode.Single);
            #else
            var dataVal = new JobData<T, T0,T1>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->aspectsStorage.Initialize<T0>(buffer->state),c1 = buffer->state->aspectsStorage.Initialize<T1>(buffer->state),
            };
            data = _address(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, GetReflectionData<T, T0,T1>(), dependsOn, ScheduleMode.Single);
            #endif
            
            return JobsUtility.Schedule(ref parameters);
            
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
        
        internal struct JobProcessData<T, T0,T1> {
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcessData<T, T0,T1>>();
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        internal struct JobProcessUnsafeData<T, T0,T1> {
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcessUnsafeData<T, T0,T1>>();
        }
        #endif
        
        internal struct JobProcess<T, T0,T1>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1> {

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
                
                var aspect0 = jobData.c0;var aspect1 = jobData.c1;
                jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                for (uint i = 0u; i < jobData.buffer->count; ++i) {
                    jobInfo.index = i;
                    var entId = *(jobData.buffer->entities + i);
                    var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                    var ent = new Ent(entId, gen, jobData.buffer->worldId);
                    aspect0.ent = ent;aspect1.ent = ent;
                    jobData.jobData.Execute(in jobInfo, in ent, ref aspect0,ref aspect1);
                }
                jobData.buffer->EndForEachRange();
                
                JobUtils.SetCurrentThreadAsSingle(false);
                
            }
        }
    }
    
}