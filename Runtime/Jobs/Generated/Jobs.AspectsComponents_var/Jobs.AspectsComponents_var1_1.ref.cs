namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    [JobProducerType(typeof(JobAspectsComponentsExtensions1_1.JobProcess<,,>))]
    public interface IJobFor1Aspects1Components<A0, C0> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0, ref C0 c0);
    }

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions1_1 {
        
        public static JobHandle Schedule<T, A0, C0>(this QueryBuilder builder, in T job = default) where T : struct, IJobFor1Aspects1Components<A0, C0> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase {
            builder.WithAspect<A0>();
            builder.With<C0>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, A0, C0>(in builder.commandBuffer.ptr, builder.isUnsafe, builder.parallelForBatch, builder.scheduleMode, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, A0, C0>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobFor1Aspects1Components<A0, C0> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase {
            return staticQuery.Schedule<T, A0, C0>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, A0, C0>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobFor1Aspects1Components<A0, C0> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state.ptr->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, A0, C0>(in job);
        }

        public static JobHandle Schedule<T, A0, C0>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobFor1Aspects1Components<A0, C0> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase {
            staticQuery.builderDependsOn = job.Schedule<T, A0, C0>(in staticQuery.commandBuffer.ptr, staticQuery.isUnsafe, staticQuery.parallelForBatch, staticQuery.scheduleMode, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }
    
    public static partial class EarlyInit {
        public static void DoAspectsComponents1_1<T, A0, C0>()
                where A0 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase
                where T : struct, IJobFor1Aspects1Components<A0, C0> => JobAspectsComponentsExtensions1_1.JobEarlyInitialize<T, A0, C0>();
    }

    public static unsafe partial class JobAspectsComponentsExtensions1_1 {
        
        public static void JobEarlyInitialize<T, A0, C0>()
            where A0 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor1Aspects1Components<A0, C0> => JobProcess<T, A0, C0>.Initialize();

        public static JobHandle Schedule<T, A0, C0>(this T jobData, in CommandBuffer* buffer, bool unsafeMode, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor1Aspects1Components<A0, C0> {
            
            if (scheduleMode == ScheduleMode.Parallel) {
                
                //dependsOn = new StartParallelJob() {
                //                buffer = buffer,
                //            }.ScheduleSingle(dependsOn);
                            
                if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount(buffer->count);

            }
            
            buffer->sync = false;
            void* data = null;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            data = CompiledJobs<T>.Get(_addressPtr(ref jobData), buffer, unsafeMode, scheduleMode);
            var parameters = new JobsUtility.JobScheduleParameters(data, unsafeMode == true ? JobReflectionUnsafeData<T>.data.Data : JobReflectionData<T>.data.Data, dependsOn, scheduleMode);
            #else
            var dataVal = new JobData<T, A0, C0>() {
                scheduleMode = scheduleMode,
                jobData = jobData,
                buffer = buffer,
                a0 = buffer->state.ptr->aspectsStorage.Initialize<A0>(buffer->state),
                c0 = buffer->state.ptr->components.GetRW<C0>(buffer->state, buffer->worldId),
            };
            data = _addressPtr(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, JobReflectionData<T>.data.Data, dependsOn, scheduleMode);
            #endif
            
            if (scheduleMode == ScheduleMode.Parallel) {
                return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            }
            return JobsUtility.Schedule(ref parameters);
            
        }

        private struct JobData<T, A0, C0>
            where A0 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct {
            public ScheduleMode scheduleMode;
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public A0 a0;
            public RefRW<C0> c0;
        }

        internal struct JobProcess<T, A0, C0>
            where A0 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor1Aspects1Components<A0, C0> {

            [BurstDiscard]
            public static void Initialize() {
                if (JobReflectionData<T>.data.Data == System.IntPtr.Zero) {
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(false), typeof(T), (ExecuteJobFunction)Execute);
                    JobReflectionUnsafeData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(true), typeof(T), (ExecuteJobFunction)Execute);
                    #else
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, A0, C0>), typeof(T), (ExecuteJobFunction)Execute);
                    #endif
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, A0, C0> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, A0, C0> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                if (jobData.scheduleMode == ScheduleMode.Parallel) {
                    var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                    jobInfo.count = jobData.buffer->count;
                    var aspect0 = jobData.a0;
                    while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                        jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                        for (uint i = (uint)begin; i < end; ++i) {
                            jobInfo.index = i;
                            var entId = *(jobData.buffer->entities + i);
                            var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                            var ent = new Ent(entId, gen, jobData.buffer->worldId);
                            aspect0.ent = ent;
                            jobData.jobData.Execute(in jobInfo, in ent, ref aspect0, ref jobData.c0.Get(ent.id, ent.gen));
                        }
                        jobData.buffer->EndForEachRange();
                    }
                } else {
                    var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                    jobInfo.count = jobData.buffer->count;
                    JobUtils.SetCurrentThreadAsSingle(true);
                    var aspect0 = jobData.a0;
                    jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                    for (uint i = 0u; i < jobData.buffer->count; ++i) {
                        jobInfo.index = i;
                        var entId = *(jobData.buffer->entities + i);
                        var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                        var ent = new Ent(entId, gen, jobData.buffer->worldId);
                        aspect0.ent = ent;
                        jobData.jobData.Execute(in jobInfo, in ent, ref aspect0, ref jobData.c0.Get(ent.id, ent.gen));
                    }
                    jobData.buffer->EndForEachRange();
                    JobUtils.SetCurrentThreadAsSingle(false);
                }
                
            }
        }
    }
    
}