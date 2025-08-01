namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    [JobProducerType(typeof(JobParallelForAspectExtensions.JobProcess<,>))]
    [System.Obsolete("IJobParallelForAspects is deprecated, use .AsParallel() API instead.")]
    public interface IJobParallelForAspects<T0> : IJobParallelForAspectsBase where T0 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0);
    }

    #pragma warning disable
    public static unsafe partial class QueryAspectParallelScheduleExtensions {
        
        public static JobHandle Schedule<T, T0>(this QueryBuilder builder, in T job = default) where T : struct, IJobParallelForAspects<T0> where T0 : unmanaged, IAspect {
            builder.WithAspect<T0>();
            builder.commandBuffer.ptr->SetBuilder(ref builder);
            builder.builderDependsOn = job.Schedule<T, T0>(builder.commandBuffer.ptr, builder.parallelForBatch, builder.isUnsafe, builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobParallelForAspects<T0> where T0 : unmanaged, IAspect {
            return staticQuery.Schedule<T, T0>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobParallelForAspects<T0> where T0 : unmanaged, IAspect {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state.ptr->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0>(in job);
        }

        public static JobHandle Schedule<T, T0>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobParallelForAspects<T0> where T0 : unmanaged, IAspect {
            staticQuery.builderDependsOn = job.Schedule<T, T0>(staticQuery.commandBuffer.ptr, staticQuery.parallelForBatch, staticQuery.isUnsafe, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    public static partial class EarlyInit {
        public static void DoParallelForAspect<T, T0>()
                where T0 : unmanaged, IAspect
                where T : struct, IJobParallelForAspects<T0> => JobParallelForAspectExtensions.JobEarlyInitialize<T, T0>();
    }
    #pragma warning restore

    #pragma warning disable
    public static unsafe partial class JobParallelForAspectExtensions {
        
        public static void JobEarlyInitialize<T, T0>() where T0 : unmanaged, IAspect where T : struct, IJobParallelForAspects<T0> => JobProcess<T, T0>.Initialize();
        
        [CodeGeneratorIgnore]
        public static JobHandle Schedule<T, T0>(this T jobData, CommandBuffer* buffer, uint innerLoopBatchCount, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect
            where T : struct, IJobParallelForAspects<T0> {
            
            var jobInfo = JobInfo.Create(buffer->worldId);
            dependsOn = JobStaticInfo<T>.SchedulePatch(ref jobInfo, buffer, ScheduleMode.Parallel, dependsOn);
            
            if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount<T>(buffer->count);

            JobInject<T>.Patch(ref jobData, buffer->worldId);
            
            buffer->sync = false;
            void* data = null;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            data = CompiledJobs<T>.Get(_addressPtr(ref jobData), buffer, unsafeMode, ScheduleFlags.Parallel, in jobInfo);
            var parameters = new JobsUtility.JobScheduleParameters(data, unsafeMode == true ? JobReflectionUnsafeData<T>.data.Data : JobReflectionData<T>.data.Data, dependsOn, ScheduleMode.Parallel);
            #else
            var dataVal = new JobData<T, T0>() {
                scheduleMode = ScheduleMode.Parallel,
                jobData = jobData,
                jobInfo = jobInfo,
                buffer = buffer,
                a0 = buffer->state.ptr->aspectsStorage.Initialize<T0>(buffer->state),
            };
            data = _addressPtr(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, JobReflectionData<T>.data.Data, dependsOn, ScheduleMode.Parallel);
            #endif
            
            return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);

        }

        private struct JobData<T, T0>
            where T0 : unmanaged, IAspect
            where T : struct {
            public ScheduleMode scheduleMode;
            public JobInfo jobInfo;
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public T0 a0;
        }

        internal struct JobProcess<T, T0>
            where T0 : unmanaged, IAspect
            where T : struct, IJobParallelForAspects<T0> {

            [BurstDiscard]
            public static void Initialize() {
                if (JobReflectionData<T>.data.Data == System.IntPtr.Zero) {
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(false), typeof(T), (ExecuteJobFunction)Execute);
                    JobReflectionUnsafeData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(true), typeof(T), (ExecuteJobFunction)Execute);
                    #else
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0>), typeof(T), (ExecuteJobFunction)Execute);
                    #endif
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                var jobInfo = jobData.jobInfo;
                jobInfo.CreateLocalCounter();
                jobInfo.count = jobData.buffer->count;
                var aspect0 = jobData.a0;
                
                JobStaticInfo<T>.lastCount = jobInfo.count;
                
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                    
                    jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                    for (uint i = (uint)begin; i < end; ++i) {
                        jobInfo.index = i;
                        jobInfo.ResetLocalCounter();
                        var entId = *(jobData.buffer->entities + i);
                        var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                        var ent = new Ent(entId, gen, jobData.buffer->worldId);
                        aspect0.ent = ent;
                        jobData.jobData.Execute(in jobInfo, in ent, ref aspect0);
                    }
                    jobData.buffer->EndForEachRange();
                    
                }

            }
        }
    }
    #pragma warning restore
    
}