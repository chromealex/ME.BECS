namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryParallelScheduleExtensions {
        
        public static JobHandle Schedule<T>(this QueryBuilder builder, in T job = default) where T : struct, IJobForComponents {
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T>(in builder.commandBuffer.ptr, builder.parallelForBatch, builder.scheduleMode, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobForComponents {
            return staticQuery.Schedule<T>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobForComponents {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state.ptr->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T>(in job);
        }

        public static JobHandle Schedule<T>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobForComponents {
            staticQuery.builderDependsOn = job.Schedule<T>(in staticQuery.commandBuffer.ptr, staticQuery.parallelForBatch, staticQuery.scheduleMode, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }
    
    [JobProducerType(typeof(JobForComponentsExtensions.JobProcess<>))]
    public interface IJobForComponents : IJobForComponentsBase {
        void Execute(in JobInfo jobInfo, in Ent ent);
    }

    public static partial class EarlyInit {
        public static void DoComponents<T>()
            where T : struct, IJobForComponents => JobForComponentsExtensions.JobEarlyInitialize<T>();
    }

    public static unsafe partial class JobForComponentsExtensions {
        
        public static void JobEarlyInitialize<T>()
            where T : struct, IJobForComponents => JobProcess<T>.Initialize();

        public static JobHandle Schedule<T>(this T jobData, in CommandBuffer* buffer, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T : struct, IJobForComponents {
            
            buffer->sync = true;
            if (scheduleMode == ScheduleMode.Parallel) {
                
                buffer->sync = false;
                //dependsOn = new StartParallelJob() {
                //                buffer = buffer,
                //            }.ScheduleSingle(dependsOn);
                            
                if (innerLoopBatchCount == 0u) innerLoopBatchCount = JobUtils.GetScheduleBatchCount(buffer->count);

            }
            
            var dataVal = new JobData<T>() {
                scheduleMode = scheduleMode,
                jobData = jobData,
                buffer = buffer,
            };
            var data = _addressPtr(ref dataVal);
            var parameters = new JobsUtility.JobScheduleParameters(data, JobReflectionData<T>.data.Data, dependsOn, scheduleMode);
            if (scheduleMode == ScheduleMode.Parallel) {
                return JobsUtility.ScheduleParallelForDeferArraySize(ref parameters, (int)innerLoopBatchCount, (byte*)buffer, null);
            }
            return JobsUtility.Schedule(ref parameters);

        }

        private struct JobData<T>
            where T : struct {
            public ScheduleMode scheduleMode;
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
        }

        internal struct JobProcess<T>
            where T : struct, IJobForComponents {

            [BurstDiscard]
            public static void Initialize() {
                if (JobReflectionData<T>.data.Data == System.IntPtr.Zero) {
                    JobReflectionData<T>.data.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T>), typeof(T), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {

                if (jobData.scheduleMode == ScheduleMode.Parallel) {
                    var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                    jobInfo.count = jobData.buffer->count;
                    while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end) == true) {
                        jobData.buffer->BeginForEachRange((uint)begin, (uint)end);
                        for (uint i = (uint)begin; i < end; ++i) {
                            jobInfo.index = i;
                            var entId = *(jobData.buffer->entities + i);
                            var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                            var ent = new Ent(entId, gen, jobData.buffer->worldId);
                            jobData.jobData.Execute(in jobInfo, in ent);
                        }
                        jobData.buffer->EndForEachRange();
                    }
                } else {
                    var jobInfo = JobInfo.Create(jobData.buffer->worldId);
                    jobInfo.count = jobData.buffer->count;
                    JobUtils.SetCurrentThreadAsSingle(true);
                    jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                    for (uint i = 0u; i < jobData.buffer->count; ++i) {
                        jobInfo.index = i;
                        var entId = *(jobData.buffer->entities + i);
                        var gen = Ents.GetGeneration(jobData.buffer->state, entId);
                        var ent = new Ent(entId, gen, jobData.buffer->worldId);
                        jobData.jobData.Execute(in jobInfo, in ent);
                    }
                    jobData.buffer->EndForEachRange();
                    JobUtils.SetCurrentThreadAsSingle(false);
                }

            }
        }
    }
    
}