// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.ParallelFor.Aspect.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobParallelForAspectExtensions.JobProcess<,,>))]
    [System.Obsolete("IJobParallelForAspects is deprecated, use .AsParallel() API instead.")]
    public interface IJobParallelForAspects<T0,T1> : IJobParallelForAspectsBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1);
    }

    #pragma warning disable
    public static unsafe partial class QueryAspectParallelScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1>(this QueryBuilder builder, in T job = default) where T : struct, IJobParallelForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect;
        
        #if !ENABLE_BECS_FLAT_QUERIES
        public static partial JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobParallelForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect;
        
        public static partial JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobParallelForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect;

        public static partial JobHandle Schedule<T, T0,T1>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobParallelForAspects<T0,T1> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect;
        #endif
        
    }

    public static partial class EarlyInit {
        public static partial void DoParallelForAspect<T, T0,T1>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
                where T : struct, IJobParallelForAspects<T0,T1>;
    }
    #pragma warning restore

    #pragma warning disable
    public static unsafe partial class JobParallelForAspectExtensions {
        
        public static partial void JobEarlyInitialize<T, T0,T1>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T : struct, IJobParallelForAspects<T0,T1>;
        
        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1>(this T jobData, CommandBuffer* buffer, uint innerLoopBatchCount, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect
            where T : struct, IJobParallelForAspects<T0,T1>;
    }
    #pragma warning restore
    
}