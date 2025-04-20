namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    [JobProducerType(typeof(JobComponentsExtensions.JobProcess<,,,,,,,>))]
    public interface IJobForComponents<T0,T1,T2,T3,T4,T5,T6> : IJobForComponentsBase where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4,ref T5 c5,ref T6 c6);
    }

    public static unsafe partial class QueryScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this QueryBuilder builder, in T job = default) where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase {
            builder.With<T0>(); builder.With<T1>(); builder.With<T2>(); builder.With<T3>(); builder.With<T4>(); builder.With<T5>(); builder.With<T6>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4,T5,T6>(builder.commandBuffer.ptr, builder.isUnsafe, builder.isReadonly, builder.parallelForBatch, builder.scheduleMode, builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase {
            return staticQuery.Schedule<T, T0,T1,T2,T3,T4,T5,T6>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state.ptr->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1,T2,T3,T4,T5,T6>(in job);
        }

        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4,T5,T6>(staticQuery.commandBuffer.ptr, staticQuery.isUnsafe, staticQuery.isReadonly, staticQuery.parallelForBatch, staticQuery.scheduleMode, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    public static partial class EarlyInit {
        public static void DoComponents<T, T0,T1,T2,T3,T4,T5,T6>()
                where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase
                where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5,T6> => JobComponentsExtensions.JobEarlyInitialize<T, T0,T1,T2,T3,T4,T5,T6>();
    }

    public static unsafe partial class JobComponentsExtensions {
        
        public static void JobEarlyInitialize<T, T0,T1,T2,T3,T4,T5,T6>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase
            where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5,T6> => JobProcess<T, T0,T1,T2,T3,T4,T5,T6>.Initialize();

        [CodeGeneratorIgnore]
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase
            where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5,T6> {
            
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
            var dataVal = new JobData<T, T0,T1,T2,T3,T4,T5,T6>() {
                scheduleFlags = flags,
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state.ptr->components.GetRW<T0>(buffer->state, buffer->worldId),c1 = buffer->state.ptr->components.GetRW<T1>(buffer->state, buffer->worldId),c2 = buffer->state.ptr->components.GetRW<T2>(buffer->state, buffer->worldId),c3 = buffer->state.ptr->components.GetRW<T3>(buffer->state, buffer->worldId),c4 = buffer->state.ptr->components.GetRW<T4>(buffer->state, buffer->worldId),c5 = buffer->state.ptr->components.GetRW<T5>(buffer->state, buffer->worldId),c6 = buffer->state.ptr->components.GetRW<T6>(buffer->state, buffer->worldId),
            };
            data = _addressPtr(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, reflectionData, dependsOn, scheduleMode);
            #endif
            
            if (scheduleMode == ScheduleMode.Parallel) {
                return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            }
            return JobsUtility.Schedule(ref parameters);
            
        }

        private struct JobData<T, T0,T1,T2,T3,T4,T5,T6>
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase
            where T : struct {
            public ScheduleFlags scheduleFlags;
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public RefRW<T0> c0;public RefRW<T1> c1;public RefRW<T2> c2;public RefRW<T3> c3;public RefRW<T4> c4;public RefRW<T5> c5;public RefRW<T6> c6;
        }

        internal struct JobProcess<T, T0,T1,T2,T3,T4,T5,T6>
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase where T6 : unmanaged, IComponentBase
            where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5,T6> {

            [BurstDiscard]
            public static void Initialize() {
                if (JobReflectionData<T>.data.Data == System.IntPtr.Zero) {
                    #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(false), typeof(T), (ExecuteJobFunction)Execute);
                    JobReflectionUnsafeData<T>.data.Data = JobsUtility.CreateJobReflectionData(CompiledJobs<T>.GetJobType(true), typeof(T), (ExecuteJobFunction)Execute);
                    #else
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1,T2,T3,T4,T5,T6>), typeof(T), (ExecuteJobFunction)Execute);
                    #endif
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1,T2,T3,T4,T5,T6> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1,T2,T3,T4,T5,T6> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
            
                var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                jobInfo.count = jobData.buffer->count;
                
                if ((jobData.scheduleFlags & ScheduleFlags.IsReadonly) != 0) {
                    if ((jobData.scheduleFlags & ScheduleFlags.Parallel) != 0) {
                        while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                            jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                            for (uint i = (uint)begin; i < end; ++i) {
                                jobInfo.index = i;
                                var entId = *(jobData.buffer->entities + i);
                                var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                                var ent = new Ent(entId, gen, jobData.buffer->worldId);
                                jobData.jobData.Execute(in jobInfo, in ent, ref jobData.c0.GetReadonly(ent.id, ent.gen),ref jobData.c1.GetReadonly(ent.id, ent.gen),ref jobData.c2.GetReadonly(ent.id, ent.gen),ref jobData.c3.GetReadonly(ent.id, ent.gen),ref jobData.c4.GetReadonly(ent.id, ent.gen),ref jobData.c5.GetReadonly(ent.id, ent.gen),ref jobData.c6.GetReadonly(ent.id, ent.gen));
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
                            jobData.jobData.Execute(in jobInfo, in ent, ref jobData.c0.GetReadonly(ent.id, ent.gen),ref jobData.c1.GetReadonly(ent.id, ent.gen),ref jobData.c2.GetReadonly(ent.id, ent.gen),ref jobData.c3.GetReadonly(ent.id, ent.gen),ref jobData.c4.GetReadonly(ent.id, ent.gen),ref jobData.c5.GetReadonly(ent.id, ent.gen),ref jobData.c6.GetReadonly(ent.id, ent.gen));
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
                                jobData.jobData.Execute(in jobInfo, in ent, ref jobData.c0.Get(ent.id, ent.gen),ref jobData.c1.Get(ent.id, ent.gen),ref jobData.c2.Get(ent.id, ent.gen),ref jobData.c3.Get(ent.id, ent.gen),ref jobData.c4.Get(ent.id, ent.gen),ref jobData.c5.Get(ent.id, ent.gen),ref jobData.c6.Get(ent.id, ent.gen));
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
                            jobData.jobData.Execute(in jobInfo, in ent, ref jobData.c0.Get(ent.id, ent.gen),ref jobData.c1.Get(ent.id, ent.gen),ref jobData.c2.Get(ent.id, ent.gen),ref jobData.c3.Get(ent.id, ent.gen),ref jobData.c4.Get(ent.id, ent.gen),ref jobData.c5.Get(ent.id, ent.gen),ref jobData.c6.Get(ent.id, ent.gen));
                        }
                        jobData.buffer->EndForEachRange();
                        JobUtils.SetCurrentThreadAsSingle(false);
                    }
                }
                
            }
        }
    }
    
}