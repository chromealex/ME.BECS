namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public static unsafe partial class QueryScheduleExtensions {
        
        public static JobHandle Schedule<T>(this QueryBuilder builder, in T job = default) where T : struct, IJobComponents {
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T>(in builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobComponents {
            return staticQuery.Schedule<T>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobComponents {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T>(in job);
        }

        public static JobHandle Schedule<T>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobComponents {
            staticQuery.builderDependsOn = job.Schedule<T>(in staticQuery.commandBuffer, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    [JobProducerType(typeof(JobComponentsExtensions.JobProcess<>))]
    public interface IJobComponents : IJobComponentsBase {
        void Execute(in JobInfo jobInfo, in Ent ent);
    }

    public static unsafe partial class JobComponentsExtensions {
        
        public static void JobEarlyInit<T>()
            where T : struct, IJobComponents => JobProcess<T>.Initialize();

        private static System.IntPtr GetReflectionData<T>()
            where T : struct, IJobComponents {
            JobProcess<T>.Initialize();
            System.IntPtr reflectionData = JobProcess<T>.jobReflectionData.Data;
            return reflectionData;
        }

        public static JobHandle Schedule<T>(this T jobData, in CommandBuffer* buffer, JobHandle dependsOn = default)
            where T : struct, IJobComponents {
            
            buffer->sync = false;
            var data = new JobData<T>() {
                jobData = jobData,
                buffer = buffer,
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_address(ref data), GetReflectionData<T>(), dependsOn, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);

        }

        private struct JobData<T>
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
        }

        internal struct JobProcess<T>
            where T : struct, IJobComponents {

            internal static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T>>();

            [BurstDiscard]
            public static void Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T>), typeof(T), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobData<T> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
            
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