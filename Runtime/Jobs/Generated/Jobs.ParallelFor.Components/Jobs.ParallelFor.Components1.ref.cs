// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.ParallelFor.Components.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobParallelForComponentsExtensions.JobProcess<,>))]
    [System.Obsolete("IJobParallelForComponents is deprecated, use .AsParallel() API instead.")]
    public interface IJobParallelForComponents<T0> : IJobParallelForComponentsBase where T0 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0);
    }
    
    #pragma warning disable
    public static unsafe partial class QueryParallelScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0>(this QueryBuilder builder, in T job = default) where T : struct, IJobParallelForComponents<T0> where T0 : unmanaged, IComponentBase;
        
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoParallelForComponents<T, T0>()
                where T0 : unmanaged, IComponentBase
                where T : struct, IJobParallelForComponents<T0>;
    }
    #pragma warning restore

    #pragma warning disable
    public static unsafe partial class JobParallelForComponentsExtensions {
    
        public static partial void JobEarlyInitialize<T, T0>()
            where T0 : unmanaged, IComponentBase
            where T : struct, IJobParallelForComponents<T0>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0>(this T jobData, CommandBuffer* buffer, uint innerLoopBatchCount, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponentBase
            where T : struct, IJobParallelForComponents<T0>;
    }
    #pragma warning restore
    
}