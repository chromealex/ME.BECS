// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.Aspect.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectExtensions.JobProcess<,>))]
    public interface IJobForAspects<T0> : IJobForAspectsBase where T0 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0);
    }

    public static unsafe partial class QueryAspectScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0>(this QueryBuilder builder, in T job = default) where T : struct, IJobForAspects<T0> where T0 : unmanaged, IAspect;
        
        #if !ENABLE_BECS_FLAT_QUERIES
        public static partial JobHandle Schedule<T, T0>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobForAspects<T0> where T0 : unmanaged, IAspect;
        
        public static partial JobHandle Schedule<T, T0>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobForAspects<T0> where T0 : unmanaged, IAspect;

        public static partial JobHandle Schedule<T, T0>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobForAspects<T0> where T0 : unmanaged, IAspect;
        #endif
        
    }

    public static partial class EarlyInit {
        public static partial void DoAspect<T, T0>()
                where T0 : unmanaged, IAspect
                where T : struct, IJobForAspects<T0>;
    }

    public static unsafe partial class JobAspectExtensions {
        
        public static partial void JobEarlyInitialize<T, T0>()
            where T0 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0>;
    }
    
}