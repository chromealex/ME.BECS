// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.AspectsComponents_var.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectsComponentsExtensions1_4.JobProcess<,,,,,>))]
    public interface IJobFor1Aspects4Components<A0, C0,C1,C2,C3> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0, ref C0 c0,ref C1 c1,ref C2 c2,ref C3 c3);
    }

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions1_4 {
        
        public static partial JobHandle Schedule<T, A0, C0,C1,C2,C3>(this QueryBuilder builder, in T job = default) where T : struct, IJobFor1Aspects4Components<A0, C0,C1,C2,C3> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase;
        
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoAspectsComponents1_4<T, A0, C0,C1,C2,C3>()
                where A0 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
                where T : struct, IJobFor1Aspects4Components<A0, C0,C1,C2,C3>;
    }

    public static unsafe partial class JobAspectsComponentsExtensions1_4 {
        
        public static partial void JobEarlyInitialize<T, A0, C0,C1,C2,C3>()
            where A0 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobFor1Aspects4Components<A0, C0,C1,C2,C3>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, A0, C0,C1,C2,C3>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase where C3 : unmanaged, IComponentBase
            where T : struct, IJobFor1Aspects4Components<A0, C0,C1,C2,C3>;
    }
    
}