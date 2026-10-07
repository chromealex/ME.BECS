// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.ParallelFor.Aspect.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobParallelForAspectExtensions.JobProcess<,,,,,,>))]
    [System.Obsolete("IJobParallelForAspects is deprecated, use .AsParallel() API instead.")]
    public interface IJobParallelForAspects<T0,T1,T2,T3,T4,T5> : IJobParallelForAspectsBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4,ref T5 c5);
    }

    #pragma warning disable
    public static unsafe partial class QueryAspectParallelScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this QueryBuilder builder, in T job = default) where T : struct, IJobParallelForAspects<T0,T1,T2,T3,T4,T5> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect;
        
        
    }

    public static partial class EarlyInit {
        public static partial void DoParallelForAspect<T, T0,T1,T2,T3,T4,T5>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect
                where T : struct, IJobParallelForAspects<T0,T1,T2,T3,T4,T5>;
    }
    #pragma warning restore

    #pragma warning disable
    public static unsafe partial class JobParallelForAspectExtensions {
        
        public static partial void JobEarlyInitialize<T, T0,T1,T2,T3,T4,T5>() where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T : struct, IJobParallelForAspects<T0,T1,T2,T3,T4,T5>;
        
        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this T jobData, CommandBuffer* buffer, uint innerLoopBatchCount, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect
            where T : struct, IJobParallelForAspects<T0,T1,T2,T3,T4,T5>;
    }
    #pragma warning restore
    
}