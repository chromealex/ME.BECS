namespace ME.BECS.Jobs {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    public static unsafe partial class QueryScheduleExtensions {
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(this QueryBuilder builder, in T job) where T : struct, IJobComponents<T0,T1,T2,T3,T4,T5,T6,T7,T8> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent {
            builder.With<T0>(); builder.With<T1>(); builder.With<T2>(); builder.With<T3>(); builder.With<T4>(); builder.With<T5>(); builder.With<T6>(); builder.With<T7>(); builder.With<T8>();
            builder.builderDependsOn = builder.SetEntities(builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(in builder.commandBuffer, builder.builderDependsOn);
            builder.builderDependsOn = builder.Dispose(builder.builderDependsOn);
            return builder.builderDependsOn;
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobComponents<T0,T1,T2,T3,T4,T5,T6,T7,T8> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent {
            return staticQuery.Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(in job, in context.world, context.dependsOn);
        }
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobComponents<T0,T1,T2,T3,T4,T5,T6,T7,T8> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent {
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, staticQuery.id));
            return query.Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(in job);
        }

        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobComponents<T0,T1,T2,T3,T4,T5,T6,T7,T8> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent {
            staticQuery.builderDependsOn = job.Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(in staticQuery.commandBuffer, staticQuery.builderDependsOn);
            staticQuery.builderDependsOn = staticQuery.Dispose(staticQuery.builderDependsOn);
            return staticQuery.builderDependsOn;
        }
        
    }

    [JobProducerType(typeof(JobComponentsExtensions_1.JobProcess<,,,,,,,,,>))]
    public interface IJobComponents<T0,T1,T2,T3,T4,T5,T6,T7,T8> where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent {
        void Execute(ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4,ref T5 c5,ref T6 c6,ref T7 c7,ref T8 c8);
    }

    public static unsafe partial class JobComponentsExtensions_1 {
        
        public static JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(this T jobData, in CommandBuffer* buffer, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent
            where T : struct, IJobComponents<T0,T1,T2,T3,T4,T5,T6,T7,T8> {
            
            buffer->sync = false;
            var data = new JobData<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>() {
                jobData = jobData,
                buffer = buffer,
                c0 = buffer->state->components.GetRW<T0>(buffer->state),c1 = buffer->state->components.GetRW<T1>(buffer->state),c2 = buffer->state->components.GetRW<T2>(buffer->state),c3 = buffer->state->components.GetRW<T3>(buffer->state),c4 = buffer->state->components.GetRW<T4>(buffer->state),c5 = buffer->state->components.GetRW<T5>(buffer->state),c6 = buffer->state->components.GetRW<T6>(buffer->state),c7 = buffer->state->components.GetRW<T7>(buffer->state),c8 = buffer->state->components.GetRW<T8>(buffer->state),
            };
            
            var parameters = new JobsUtility.JobScheduleParameters(_address(ref data), JobProcess<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>.Initialize(), dependsOn, ScheduleMode.Parallel);
            return JobsUtility.Schedule(ref parameters);

        }

        private struct JobData<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>
            where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent
            where T : struct {
            [NativeDisableUnsafePtrRestriction]
            public T jobData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            public RefRW<T0> c0;public RefRW<T1> c1;public RefRW<T2> c2;public RefRW<T3> c3;public RefRW<T4> c4;public RefRW<T5> c5;public RefRW<T6> c6;public RefRW<T7> c7;public RefRW<T8> c8;
        }

        internal struct JobProcess<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>
            where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent
            where T : struct, IJobComponents<T0,T1,T2,T3,T4,T5,T6,T7,T8> {

            private static readonly Unity.Burst.SharedStatic<System.IntPtr> jobReflectionData = Unity.Burst.SharedStatic<System.IntPtr>.GetOrCreate<JobProcess<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>>();

            public static System.IntPtr Initialize() {
                if (jobReflectionData.Data == System.IntPtr.Zero) {
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobData<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>), typeof(T), (ExecuteJobFunction)Execute);
                }
                return jobReflectionData.Data;
            }

            private delegate void ExecuteJobFunction(ref JobData<T, T0,T1,T2,T3,T4,T5,T6,T7,T8> jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static void Execute(ref JobData<T, T0,T1,T2,T3,T4,T5,T6,T7,T8> jobData, System.IntPtr additionalData, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
            
                JobUtils.SetCurrentThreadAsSingle(true);
                
                jobData.buffer->BeginForEachRange(0u, jobData.buffer->count);
                for (int i = 0; i < jobData.buffer->count; ++i) {
                    var entId = jobData.buffer->entities[i];
                    jobData.jobData.Execute(ref jobData.c0.Get(entId),ref jobData.c1.Get(entId),ref jobData.c2.Get(entId),ref jobData.c3.Get(entId),ref jobData.c4.Get(entId),ref jobData.c5.Get(entId),ref jobData.c6.Get(entId),ref jobData.c7.Get(entId),ref jobData.c8.Get(entId));
                }
                jobData.buffer->EndForEachRange();
                
                JobUtils.SetCurrentThreadAsSingle(false);
                
            }
        }
    }
    
}