// Public contracts for component jobs. JobBackendGenerator emits implementations
// from Jobs.Components.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobComponentsExtensions.JobProcess<,,>))]
    public interface IJobForComponents<T0,T1> : IJobForComponentsBase where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1);
    }

    public static unsafe partial class QueryScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1>(this QueryBuilder builder, in T job = default) where T : struct, IJobForComponents<T0,T1> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase;
        
        
    }

    public static partial class EarlyInit {
        public static partial void DoComponents<T, T0,T1>()
                where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
                where T : struct, IJobForComponents<T0,T1>;
    }

    public static unsafe partial class JobComponentsExtensions {
        
        public static partial void JobEarlyInitialize<T, T0,T1>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobForComponents<T0,T1>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase
            where T : struct, IJobForComponents<T0,T1>;
    }
    
}
