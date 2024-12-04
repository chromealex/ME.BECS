namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryAspectParallelScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this QueryBuilder builder, in T job = default) where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
            builder.WithAspect<T0>(); builder.WithAspect<T1>(); builder.WithAspect<T2>(); builder.WithAspect<T3>(); builder.WithAspect<T4>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4>(in builder.commandBuffer, builder.parallelForBatch, builder.isUnsafe, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
            return staticQuery.Schedule<T, T0,T1,T2,T3,T4>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1,T2,T3,T4>(in job);
        }

        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4>(in staticQuery.commandBuffer, staticQuery.parallelForBatch, staticQuery.isUnsafe, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    public static partial class EarlyInit {
        public static void DoParallelForAspect<T, T0,T1,T2,T3,T4>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
                where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> => JobParallelForAspectExtensions.JobEarlyInitialize<T, T0,T1,T2,T3,T4>();
    }

    [JobProducerType(typeof(JobParallelForAspectExtensions.JobProcess<,,,,,>))]
    public interface IJobParallelForAspect<T0,T1,T2,T3,T4> : IJobParallelForAspectBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4);
    }

    public static unsafe partial class JobParallelForAspectExtensions {
        
        public static void JobEarlyInitialize<T, T0,T1,T2,T3,T4>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> => JobProcess<T, T0,T1,T2,T3,T4>.Initialize();
        
        private static System.IntPtr GetReflectionData<T, T0,T1,T2,T3,T4>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> {
            JobProcess<T, T0,T1,T2,T3,T4>.Initialize();
            System.IntPtr reflectionData = JobProcessData<T, T0,T1,T2,T3,T4>.jobReflectionData.Data;
            return reflectionData;
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        private static System.IntPtr GetReflectionUnsafeData<T, T0,T1,T2,T3,T4>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> {
            JobProcess<T, T0,T1,T2,T3,T4>.Initialize();
            System.IntPtr reflectionData = JobProcessUnsafeData<T, T0,T1,T2,T3,T4>.jobReflectionData.Data;
            return reflectionData;
        }
        #endif

        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
            where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> {
            
            //dependsOn = new StartParallelJob() {
            //                buffer = buffer,
            //            }.ScheduleSingle(dependsOn);
                        
            if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount(buffer->count);

            buffer->sync = false;
            void* data = null;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            data = CompiledJobs<T>.Get(ref jobData, buffer, unsafeMode);
            var parameters = new JobsUtility.JobScheduleParameters(data, unsafeMode == true ? GetReflectionUnsafeData<T, T0,T1,T2,T3,T4>() : GetReflectionData<T, T0,T1,T2,T3,T4>(), dependsOn, ScheduleMode.Parallel);
            #else
            var dataVal = new JobData<T, T0,T1,T2,T3,T4>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->aspectsStorage.Initialize<T0>(buffer->state),c1 = buffer->state->aspectsStorage.Initialize<T1>(buffer->state),c2 = buffer->state->aspectsStorage.Initialize<T2>(buffer->state),c3 = buffer->state->aspectsStorage.Initialize<T3>(buffer->state),c4 = buffer->state->aspectsStorage.Initialize<T4>(buffer->state),
            };
            data = _address(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, GetReflectionData<T, T0,T1,T2,T3,T4>(), dependsOn, ScheduleMode.Parallel);
            #endif
            
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);

        }

        private struct JobData<T, T0,T1,T2,T3,T4>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public T0 c0;public T1 c1;public T2 c2;public T3 c3;public T4 c4;
        }

        internal struct JobProcessData<T, T0,T1,T2,T3,T4> {
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcessData<T, T0,T1,T2,T3,T4>>();
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        internal struct JobProcessUnsafeData<T, T0,T1,T2,T3,T4> {
            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcessUnsafeData<T, T0,T1,T2,T3,T4>>();
        }
        #endif

        internal struct JobProcess<T, T0,T1,T2,T3,T4>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
            where T : struct, IJobParallelForAspect<T0,T1,T2,T3,T4> {

            [BurstDiscard]
            public static void Initialize() {
                if (JobProcessData<T, T0,T1,T2,T3,T4>.jobReflectionData.Data == System.IntPtr.Zero) {
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    JobProcessData<T, T0,T1,T2,T3,T4>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(false), typeof(T), (ExecuteJobFunction)Execute);
                    JobProcessUnsafeData<T, T0,T1,T2,T3,T4>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(true), typeof(T), (ExecuteJobFunction)Execute);
                    #else
                    JobProcessData<T, T0,T1,T2,T3,T4>.jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1,T2,T3,T4>), typeof(T), (ExecuteJobFunction)Execute);
                    #endif
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1,T2,T3,T4> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1,T2,T3,T4> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                jobInfo.count = jobData.buffer->count;
                var aspect0 = jobData.c0;var aspect1 = jobData.c1;var aspect2 = jobData.c2;var aspect3 = jobData.c3;var aspect4 = jobData.c4;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        jobInfo.index = i;
                        var entId = *(jobData.buffer->entities + i);
                        var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                        aspect0.ent = new Ent(entId, gen, jobData.buffer->worldId);aspect1.ent = new Ent(entId, gen, jobData.buffer->worldId);aspect2.ent = new Ent(entId, gen, jobData.buffer->worldId);aspect3.ent = new Ent(entId, gen, jobData.buffer->worldId);aspect4.ent = new Ent(entId, gen, jobData.buffer->worldId);
                        jobData.jobData.Execute(in jobInfo, ref aspect0,ref aspect1,ref aspect2,ref aspect3,ref aspect4);
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    
}