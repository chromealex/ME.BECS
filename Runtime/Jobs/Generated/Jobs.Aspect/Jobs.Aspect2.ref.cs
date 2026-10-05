// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.Aspect.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectExtensions.JobProcess<,,>))]
    public interface IJobForAspects<T0,T1> : IJobForAspectsBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1);
    }

    public static unsafe partial class QueryAspectScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1>(this QueryBuilder builder, in T job = default) where T : struct, IJobForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect;
        
        #if !ENABLE_BECS_FLAT_QUERIES
        public static partial JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect;
        
        public static partial JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect;

        public static partial JobHandle Schedule<T, T0,T1>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect;
        #endif
        
    }

    public static partial class EarlyInit {
        public static partial void DoAspect<T, T0,T1>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
                where T : struct, IJobForAspects<T0,T1>;
    }

    public static unsafe partial class JobAspectExtensions {
        
        public static partial void JobEarlyInitialize<T, T0,T1>()
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1>;
    }
    
}