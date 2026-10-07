// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.Aspect.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectExtensions.JobProcess<,,,,,,,,,>))]
    public interface IJobForAspects<T0,T1,T2,T3,T4,T5,T6,T7,T8> : IJobForAspectsBase where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect where T7 : unmanaged, IAspect where T8 : unmanaged, IAspect {
        void Execute(in JobInfo jobInfo, in Ent ent, ref T0 c0,ref T1 c1,ref T2 c2,ref T3 c3,ref T4 c4,ref T5 c5,ref T6 c6,ref T7 c7,ref T8 c8);
    }

    public static unsafe partial class QueryAspectScheduleExtensions {
        
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(this QueryBuilder builder, in T job = default) where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6,T7,T8> where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect where T7 : unmanaged, IAspect where T8 : unmanaged, IAspect;
        
        
    }

    public static partial class EarlyInit {
        public static partial void DoAspect<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>()
                where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect where T7 : unmanaged, IAspect where T8 : unmanaged, IAspect
                where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6,T7,T8>;
    }

    public static unsafe partial class JobAspectExtensions {
        
        public static partial void JobEarlyInitialize<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>()
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect where T7 : unmanaged, IAspect where T8 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6,T7,T8>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, T0,T1,T2,T3,T4,T5,T6,T7,T8>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where T0 : unmanaged, IAspect where T1 : unmanaged, IAspect where T2 : unmanaged, IAspect where T3 : unmanaged, IAspect where T4 : unmanaged, IAspect where T5 : unmanaged, IAspect where T6 : unmanaged, IAspect where T7 : unmanaged, IAspect where T8 : unmanaged, IAspect
            where T : struct, IJobForAspects<T0,T1,T2,T3,T4,T5,T6,T7,T8>;
    }
    
}