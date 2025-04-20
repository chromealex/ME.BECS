namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    [JobProducerType(typeof(JobAspectsComponentsExtensions3_4.JobProcess<,,,,,,,>))]
    public interface IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0,ref A1 a1,ref A2 a2, ref C0 c0,ref C1 c1,ref C2 c2,ref C3 c3);
    }

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions3_4 {
        
        public static JobHandle Schedule<T, A0,A1,A2, C0,C1,C2,C3>(this QueryBuilder builder, in T job = default) where T : struct, IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
            builder.WithAspect<A0>(); builder.WithAspect<A1>(); builder.WithAspect<A2>();
            builder.With<C0>(); builder.With<C1>(); builder.With<C2>(); builder.With<C3>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, A0,A1,A2, C0,C1,C2,C3>(builder.commandBuffer.ptr, builder.isUnsafe, builder.isReadonly, builder.parallelForBatch, builder.scheduleMode, builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, A0,A1,A2, C0,C1,C2,C3>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
            return staticQuery.Schedule<T, A0,A1,A2, C0,C1,C2,C3>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, A0,A1,A2, C0,C1,C2,C3>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state.ptr->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, A0,A1,A2, C0,C1,C2,C3>(in job);
        }

        public static JobHandle Schedule<T, A0,A1,A2, C0,C1,C2,C3>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
            staticQuery.builderDependsOn = job.Schedule<T, A0,A1,A2, C0,C1,C2,C3>(staticQuery.commandBuffer.ptr, staticQuery.isUnsafe, staticQuery.isReadonly, staticQuery.parallelForBatch, staticQuery.scheduleMode, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }
    
    public static partial class EarlyInit {
        public static void DoAspectsComponents3_4<T, A0,A1,A2, C0,C1,C2,C3>()
                where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
                where T : struct, IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> => JobAspectsComponentsExtensions3_4.JobEarlyInitialize<T, A0,A1,A2, C0,C1,C2,C3>();
    }

    public static unsafe partial class JobAspectsComponentsExtensions3_4 {
        
        public static void JobEarlyInitialize<T, A0,A1,A2, C0,C1,C2,C3>()
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> => JobProcess<T, A0,A1,A2, C0,C1,C2,C3>.Initialize();

        [CodeGeneratorIgnore]
        public static JobHandle Schedule<T, A0,A1,A2, C0,C1,C2,C3>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> {
            
            buffer->sync = true;
            var flags = ScheduleFlags.Single;
            if (scheduleMode == ScheduleMode.Parallel) {
                
                flags |= ScheduleFlags.Parallel;
                
                buffer->sync = false;
                //dependsOn = new StartParallelJob() {
                //                buffer = buffer,
                //            }.ScheduleSingle(dependsOn);
                            
                if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount(buffer->count);

            }
            
            if (isReadonly == true) flags |= ScheduleFlags.IsReadonly;
              
            void* data = null;
            var reflectionData = JobReflectionData<T>.data.Data;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            if (unsafeMode == true) {
                reflectionData = JobReflectionUnsafeData<T>.data.Data;
            }
            #endif
            
            E.IS_NULL(reflectionData, "Job is not created. Make sure the job is public.");
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            data = CompiledJobs<T>.Get(_addressPtr(ref jobData), buffer, unsafeMode, flags);
            var parameters = new JobsUtility.JobScheduleParameters(data, reflectionData, dependsOn, scheduleMode);
            #else
            var dataVal = new JobData<T, A0,A1,A2, C0,C1,C2,C3>() {
                scheduleFlags = flags,
                jobData = jobData,
                buffer = buffer,
                a0 = buffer->state.ptr->aspectsStorage.Initialize<A0>(buffer->state),a1 = buffer->state.ptr->aspectsStorage.Initialize<A1>(buffer->state),a2 = buffer->state.ptr->aspectsStorage.Initialize<A2>(buffer->state),
                c0 = buffer->state.ptr->components.GetRW<C0>(buffer->state, buffer->worldId),c1 = buffer->state.ptr->components.GetRW<C1>(buffer->state, buffer->worldId),c2 = buffer->state.ptr->components.GetRW<C2>(buffer->state, buffer->worldId),c3 = buffer->state.ptr->components.GetRW<C3>(buffer->state, buffer->worldId),
            };
            data = _addressPtr(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, reflectionData, dependsOn, scheduleMode);
            #endif
            
            if (scheduleMode == ScheduleMode.Parallel) {
                return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            }
            return JobsUtility.Schedule(ref parameters);
            
        }

        private struct JobData<T, A0,A1,A2, C0,C1,C2,C3>
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct {
            public ScheduleFlags scheduleFlags;
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public A0 a0;public A1 a1;public A2 a2;
            public RefRW<C0> c0;public RefRW<C1> c1;public RefRW<C2> c2;public RefRW<C3> c3;
        }

        internal struct JobProcess<T, A0,A1,A2, C0,C1,C2,C3>
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobFor3Aspects4Components<A0,A1,A2, C0,C1,C2,C3> {

            [BurstDiscard]
            public static void Initialize() {
                if (JobReflectionData<T>.data.Data == System.IntPtr.Zero) {
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(false), typeof(T), (ExecuteJobFunction)Execute);
                    JobReflectionUnsafeData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(true), typeof(T), (ExecuteJobFunction)Execute);
                    #else
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, A0,A1,A2, C0,C1,C2,C3>), typeof(T), (ExecuteJobFunction)Execute);
                    #endif
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, A0,A1,A2, C0,C1,C2,C3> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, A0,A1,A2, C0,C1,C2,C3> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                jobInfo.count = jobData.buffer->count;
                var aspect0 = jobData.a0;var aspect1 = jobData.a1;var aspect2 = jobData.a2;
                
                if ((jobData.scheduleFlags & ScheduleFlags.IsReadonly) != 0) {
                    if ((jobData.scheduleFlags & ScheduleFlags.Parallel) != 0) {
                        while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                            jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                            for (uint i = (uint)begin; i < end; ++i) {
                                jobInfo.index = i;
                                var entId = *(jobData.buffer->entities + i);
                                var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                                var ent = new Ent(entId, gen, jobData.buffer->worldId);
                                aspect0.ent = ent;aspect1.ent = ent;aspect2.ent = ent;
                                jobData.jobData.Execute(in jobInfo, in ent, ref aspect0,ref aspect1,ref aspect2, ref jobData.c0.GetReadonly(ent.id, ent.gen),ref jobData.c1.GetReadonly(ent.id, ent.gen),ref jobData.c2.GetReadonly(ent.id, ent.gen),ref jobData.c3.GetReadonly(ent.id, ent.gen));
                            }
                            jobData.buffer->EndForEachRange();
                        }
                    } else {
                        JobUtils.SetCurrentThreadAsSingle(true);
                        jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                        for (uint i = 0u; i < jobData.buffer->count; ++i) {
                            jobInfo.index = i;
                            var entId = *(jobData.buffer->entities + i);
                            var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                            var ent = new Ent(entId, gen, jobData.buffer->worldId);
                            aspect0.ent = ent;aspect1.ent = ent;aspect2.ent = ent;
                            jobData.jobData.Execute(in jobInfo, in ent, ref aspect0,ref aspect1,ref aspect2, ref jobData.c0.GetReadonly(ent.id, ent.gen),ref jobData.c1.GetReadonly(ent.id, ent.gen),ref jobData.c2.GetReadonly(ent.id, ent.gen),ref jobData.c3.GetReadonly(ent.id, ent.gen));
                        }
                        jobData.buffer->EndForEachRange();
                        JobUtils.SetCurrentThreadAsSingle(false);
                    }
                } else {
                    if ((jobData.scheduleFlags & ScheduleFlags.Parallel) != 0) {
                        while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                            jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                            for (uint i = (uint)begin; i < end; ++i) {
                                jobInfo.index = i;
                                var entId = *(jobData.buffer->entities + i);
                                var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                                var ent = new Ent(entId, gen, jobData.buffer->worldId);
                                aspect0.ent = ent;aspect1.ent = ent;aspect2.ent = ent;
                                jobData.jobData.Execute(in jobInfo, in ent, ref aspect0,ref aspect1,ref aspect2, ref jobData.c0.Get(ent.id, ent.gen),ref jobData.c1.Get(ent.id, ent.gen),ref jobData.c2.Get(ent.id, ent.gen),ref jobData.c3.Get(ent.id, ent.gen));
                            }
                            jobData.buffer->EndForEachRange();
                        }
                    } else {
                        JobUtils.SetCurrentThreadAsSingle(true);
                        jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                        for (uint i = 0u; i < jobData.buffer->count; ++i) {
                            jobInfo.index = i;
                            var entId = *(jobData.buffer->entities + i);
                            var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                            var ent = new Ent(entId, gen, jobData.buffer->worldId);
                            aspect0.ent = ent;aspect1.ent = ent;aspect2.ent = ent;
                            jobData.jobData.Execute(in jobInfo, in ent, ref aspect0,ref aspect1,ref aspect2, ref jobData.c0.Get(ent.id, ent.gen),ref jobData.c1.Get(ent.id, ent.gen),ref jobData.c2.Get(ent.id, ent.gen),ref jobData.c3.Get(ent.id, ent.gen));
                        }
                        jobData.buffer->EndForEachRange();
                        JobUtils.SetCurrentThreadAsSingle(false);
                    }
                }
                
            }
        }
    }
    
}