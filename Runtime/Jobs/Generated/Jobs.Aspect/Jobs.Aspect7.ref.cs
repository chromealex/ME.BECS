// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.Aspect.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectExtensions.JobProcess<,,,,,,,>))]
    public interface IJobForAspects<T0,T1,T2,T3,T4,T5,T6> : IJobForAspectsBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4,ref T5 c5,ref T6 c6);
    }

    public static unsafe partial class QueryAspectScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this QueryBuilder builder, in T job = default) where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect;
        
        #if !ENABLE_BECS_FLAT_QUERIES
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect;
        
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect;

        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect;
        #endif
        
    }

    public static partial class EarlyInit {
        public static partial void DoAspect<T, T0,T1,T2,T3,T4,T5,T6>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect
                where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6>;
    }

    public static unsafe partial class JobAspectExtensions {
        
        public static partial void JobEarlyInitialize<T, T0,T1,T2,T3,T4,T5,T6>()
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6>;
    }
    
}