namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    [JobProducerType(typeof(JobAspectExtensions.JobProcess<,,,,,>))]
    public interface IJobForAspects<T0,T1,T2,T3,T4> : IJobForAspectsBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4);
    }

    public static unsafe partial class QueryAspectScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this QueryBuilder builder, in T job = default) where T : struct, IJobForAspects<T0,T1,T2,T3,T4> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
            builder.WithAspect<T0>(); builder.WithAspect<T1>(); builder.WithAspect<T2>(); builder.WithAspect<T3>(); builder.WithAspect<T4>();
            builder.commandBuffer.ptr->SetBuilder(ref builder);
            builder.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4>(builder.commandBuffer.ptr, builder.isUnsafe, builder.isReadonly, builder.parallelForBatch, builder.scheduleMode, builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobForAspects<T0,T1,T2,T3,T4> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
            return staticQuery.Schedule<T, T0,T1,T2,T3,T4>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobForAspects<T0,T1,T2,T3,T4> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state.ptr->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1,T2,T3,T4>(in job);
        }

        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobForAspects<T0,T1,T2,T3,T4> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4>(staticQuery.commandBuffer.ptr, staticQuery.isUnsafe, staticQuery.isReadonly, staticQuery.parallelForBatch, staticQuery.scheduleMode, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    public static partial class EarlyInit {
        public static void DoAspect<T, T0,T1,T2,T3,T4>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
                where T : struct, IJobForAspects<T0,T1,T2,T3,T4> => JobAspectExtensions.JobEarlyInitialize<T, T0,T1,T2,T3,T4>();
    }

    public static unsafe partial class JobAspectExtensions {
        
        public static void JobEarlyInitialize<T, T0,T1,T2,T3,T4>()
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1,T2,T3,T4> => JobProcess<T, T0,T1,T2,T3,T4>.Initialize();

        [CodeGeneratorIgnore]
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1,T2,T3,T4> {
            
            var jobInfo = JobInfo.Create(buffer->worldId);
            dependsOn = JobStaticInfo<T>.SchedulePatch(ref jobInfo, buffer, scheduleMode, dependsOn);
            
            buffer->sync = true;
            var flags = ScheduleFlags.Single;
            if (scheduleMode == ScheduleMode.Parallel) {
                
                flags |= ScheduleFlags.Parallel;
                
                buffer->sync = false;
                            
                if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount<T>(buffer->count);

            }
            
            if (isReadonly == true) flags |= ScheduleFlags.IsReadonly;
            
            void* data = null;
            var reflectionData = JobReflectionData<T>.data.Data;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            if (unsafeMode == true) {
                reflectionData = JobReflectionUnsafeData<T>.data.Data;
            }
            #endif
            
            JobInject<T>.Patch(ref jobData, buffer->worldId);
            
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            E.IS_NULL(reflectionData, "Job is not created. Make sure the job is public.");
            data = CompiledJobs<T>.Get(_addressPtr(ref jobData), buffer, unsafeMode, flags, in jobInfo);
            var parameters = new JobsUtility.JobScheduleParameters(data, reflectionData, dependsOn, scheduleMode);
            #else
            var dataVal = new JobData<T, T0,T1,T2,T3,T4>() {
                scheduleFlags = flags,
                jobData = jobData,
                jobInfo = jobInfo,
                buffer = buffer,
                a0 = buffer->state.ptr->aspectsStorage.Initialize<T0>(buffer->state),a1 = buffer->state.ptr->aspectsStorage.Initialize<T1>(buffer->state),a2 = buffer->state.ptr->aspectsStorage.Initialize<T2>(buffer->state),a3 = buffer->state.ptr->aspectsStorage.Initialize<T3>(buffer->state),a4 = buffer->state.ptr->aspectsStorage.Initialize<T4>(buffer->state),
            };
            data = _addressPtr(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, reflectionData, dependsOn, scheduleMode);
            #endif
            
            if (scheduleMode == ScheduleMode.Parallel) {
                return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            }
            return JobsUtility.Schedule(ref parameters);
            
        }

        private struct JobData<T, T0,T1,T2,T3,T4>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
            where T : struct {
            public ScheduleFlags scheduleFlags;
            public JobInfo jobInfo;
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public T0 a0;public T1 a1;public T2 a2;public T3 a3;public T4 a4;
        }
        
        internal struct JobProcess<T, T0,T1,T2,T3,T4>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1,T2,T3,T4> {

            [BurstDiscard]
            public static void Initialize() {
                if (JobReflectionData<T>.data.Data == System.IntPtr.Zero) {
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(false), typeof(T), (ExecuteJobFunction)Execute);
                    JobReflectionUnsafeData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(true), typeof(T), (ExecuteJobFunction)Execute);
                    #else
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1,T2,T3,T4>), typeof(T), (ExecuteJobFunction)Execute);
                    #endif
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1,T2,T3,T4> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1,T2,T3,T4> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
                
                var jobInfo = jobData.jobInfo;
                jobInfo.CreateLocalCounter();
                jobInfo.count = jobData.buffer->count;
                var aspect0 = jobData.a0;var aspect1 = jobData.a1;var aspect2 = jobData.a2;var aspect3 = jobData.a3;var aspect4 = jobData.a4;
                
                JobStaticInfo<T>.lastCount = jobInfo.count;
                
                if ((jobData.scheduleFlags & ScheduleFlags.Parallel) != 0) {
                    while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                        jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                        for (uint i = (uint)begin; i < end; ++i) {
                            jobInfo.index = i;
                            jobInfo.ResetLocalCounter();
                            var entId = *(jobData.buffer->entities + i);
                            var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                            var ent = new Ent(entId, gen, jobData.buffer->worldId);
                            aspect0.ent = ent;aspect1.ent = ent;aspect2.ent = ent;aspect3.ent = ent;aspect4.ent = ent;
                            jobData.jobData.Execute(in jobInfo, in ent, ref aspect0,ref aspect1,ref aspect2,ref aspect3,ref aspect4);
                        }
                        jobData.buffer->EndForEachRange();
                    }
                    Batches.ApplyThread(jobData.buffer->state);
                } else {
                    jobData.buffer->SetEntities(jobData.buffer);
                    jobInfo.count = jobData.buffer->count;
                    JobUtils.SetCurrentThreadAsSingle(true);
                    jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                    for (uint i = 0u; i < jobData.buffer->count; ++i) {
                        jobInfo.index = i;
                        jobInfo.ResetLocalCounter();
                        var entId = *(jobData.buffer->entities + i);
                        var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                        var ent = new Ent(entId, gen, jobData.buffer->worldId);
                        aspect0.ent = ent;aspect1.ent = ent;aspect2.ent = ent;aspect3.ent = ent;aspect4.ent = ent;
                        jobData.jobData.Execute(in jobInfo, in ent, ref aspect0,ref aspect1,ref aspect2,ref aspect3,ref aspect4);
                    }
                    jobData.buffer->EndForEachRange();
                    JobUtils.SetCurrentThreadAsSingle(false);
                    Batches.ApplyThread(jobData.buffer->state);
                }
                
            }
        }
    }
    
}