namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    public static unsafe partial class QueryAspectScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this QueryBuilder builder, in T job) where T : struct, IJobAspect<T0,T1,T2,T3,T4,T5> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect {
            builder.WithAspect<T0>(); builder.WithAspect<T1>(); builder.WithAspect<T2>(); builder.WithAspect<T3>(); builder.WithAspect<T4>(); builder.WithAspect<T5>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4,T5>(in builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobAspect<T0,T1,T2,T3,T4,T5> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect {
            return staticQuery.Schedule<T, T0,T1,T2,T3,T4,T5>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobAspect<T0,T1,T2,T3,T4,T5> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1,T2,T3,T4,T5>(in job);
        }

        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobAspect<T0,T1,T2,T3,T4,T5> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4,T5>(in staticQuery.commandBuffer, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    [JobProducerType(typeof(JobAspectExtensions_1.JobProcess<,,,,,,>))]
    public interface IJobAspect<T0,T1,T2,T3,T4,T5> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect {
        void Execute(ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4,ref T5 c5);
    }

    public static unsafe partial class JobAspectExtensions_1 {
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this T jobData, in CommandBuffer* buffer, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect
            where T : struct, IJobAspect<T0,T1,T2,T3,T4,T5> {
            
            buffer->sync = false;
            var data = new JobData<T, T0,T1,T2,T3,T4,T5>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->aspectsStorage.Initialize<T0>(buffer->state),c1 = buffer->state->aspectsStorage.Initialize<T1>(buffer->state),c2 = buffer->state->aspectsStorage.Initialize<T2>(buffer->state),c3 = buffer->state->aspectsStorage.Initialize<T3>(buffer->state),c4 = buffer->state->aspectsStorage.Initialize<T4>(buffer->state),c5 = buffer->state->aspectsStorage.Initialize<T5>(buffer->state),
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_address(ref data), JobProcess<T, T0,T1,T2,T3,T4,T5>.Initialize(), dependsOn, ScheduleMode.Parallel);
            return JobsUtility.Schedule(ref parameters);

        }

        private struct JobData<T, T0,T1,T2,T3,T4,T5>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public T0 c0;public T1 c1;public T2 c2;public T3 c3;public T4 c4;public T5 c5;
        }

        internal struct JobProcess<T, T0,T1,T2,T3,T4,T5>
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect
            where T : struct, IJobAspect<T0,T1,T2,T3,T4,T5> {

            private static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T, T0,T1,T2,T3,T4,T5>>();

            public static System.IntPtr Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1,T2,T3,T4,T5>), typeof(T), (ExecuteJobFunction)Execute);
                }
                return jobReflectionData.Data;
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1,T2,T3,T4,T5> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1,T2,T3,T4,T5> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
            
                JobUtils.SetCurrentThreadAsSingle(true);
                
                jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                for (int i = 0; i < jobData.buffer->count; ++i) {
                    var entId = jobData.buffer->entities[i];
                    var gen = jobData.buffer->state->entities.GetGeneration(jobData.buffer->state, entId);
                    jobData.c0.ent = new Ent(entId, gen, jobData.buffer->worldId);jobData.c1.ent = new Ent(entId, gen, jobData.buffer->worldId);jobData.c2.ent = new Ent(entId, gen, jobData.buffer->worldId);jobData.c3.ent = new Ent(entId, gen, jobData.buffer->worldId);jobData.c4.ent = new Ent(entId, gen, jobData.buffer->worldId);jobData.c5.ent = new Ent(entId, gen, jobData.buffer->worldId);
                    jobData.jobData.Execute(ref jobData.c0,ref jobData.c1,ref jobData.c2,ref jobData.c3,ref jobData.c4,ref jobData.c5);
                }
                jobData.buffer->EndForEachRange();
                
                JobUtils.SetCurrentThreadAsSingle(false);
                
            }
        }
    }
    
}