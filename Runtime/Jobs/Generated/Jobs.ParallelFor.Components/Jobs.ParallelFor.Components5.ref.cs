// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.ParallelFor.Components.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobParallelForComponentsExtensions.JobProcess<,,,,,>))]
    [System.Obsolete("IJobParallelForComponents is deprecated, use .AsParallel() API instead.")]
    public interface IJobParallelForComponents<T0,T1,T2,T3,T4> : IJobParallelForComponentsBase where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4);
    }
    
    #pragma warning disable
    public static unsafe partial class QueryParallelScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4>(this QueryBuilder builder, in T job = default) where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase;
        
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoParallelForComponents<T, T0,T1,T2,T3,T4>()
                where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase
                where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4>;
    }
    #pragma warning restore

    #pragma warning disable
    public static unsafe partial class JobParallelForComponentsExtensions {
    
        public static partial void JobEarlyInitialize<T, T0,T1,T2,T3,T4>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase
            where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4>(this T jobData, CommandBuffer* buffer, uint innerLoopBatchCount, bool unsafeMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase
            where T : struct, IJobParallelForComponents<T0,T1,T2,T3,T4>;
    }
    #pragma warning restore
    
}