// Public contracts for component jobs. JobBackendGenerator emits implementations
// from Jobs.Components.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobComponentsExtensions.JobProcess<,,,,,,>))]
    public interface IJobForComponents<T0,T1,T2,T3,T4,T5> : IJobForComponentsBase where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4,ref T5 c5);
    }

    public static unsafe partial class QueryScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this QueryBuilder builder, in T job = default) where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5> where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase;
        
        
    }

    public static partial class EarlyInit {
        public static partial void DoComponents<T, T0,T1,T2,T3,T4,T5>()
                where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase
                where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5>;
    }

    public static unsafe partial class JobComponentsExtensions {
        
        public static partial void JobEarlyInitialize<T, T0,T1,T2,T3,T4,T5>()
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase
            where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase where T2 : unmanaged, IComponentBase where T3 : unmanaged, IComponentBase where T4 : unmanaged, IComponentBase where T5 : unmanaged, IComponentBase
            where T : struct, IJobForComponents<T0,T1,T2,T3,T4,T5>;
    }
    
}
