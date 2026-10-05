// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.ParallelFor.Components.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobParallelForComponentsExtensions.JobProcess<,,>))]
    [System.Obsolete("IJobParallelForComponents is deprecated, use .AsParallel() API instead.")]
    public interface IJobParallelForComponents<T0,T1> : IJobParallelForComponentsBase where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1);
    }
    
    #pragma warning disable
    public static unsafe partial class QueryParallelScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1>(this QueryBuilder builder, in T job = default) where T : struct, IJobParallelForComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase;
        
        #if !ENABLE_BECS_FLAT_QUERIES
        public static partial JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobParallelForComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase;
        
        public static partial JobHandle Schedule<T, T0,T1>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobParallelForComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase;

        public static partial JobHandle Schedule<T, T0,T1>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobParallelForComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase;
        #endif
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoParallelForComponents<T, T0,T1>()
                where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
                where T : struct, IJobParallelForComponents<T0,T1>;
    }
    #pragma warning restore

    #pragma warning disable
    public static unsafe partial class JobParallelForComponentsExtensions {
    
        public static partial void JobEarlyInitialize<T, T0,T1>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobParallelForComponents<T0,T1>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1>(this T jobData, CommandBuffer* buffer, uint innerLoopBatchCount, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobParallelForComponents<T0,T1>;
    }
    #pragma warning restore
    
}