// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.AspectsComponents_var.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectsComponentsExtensions2_1.JobProcess<,,,>))]
    public interface IJobFor2Aspects1Components<A0,A1, C0> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where C0 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0,ref A1 a1, ref C0 c0);
    }

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions2_1 {
        
        public static partial JobHandle Schedule<T, A0,A1, C0>(this QueryBuilder builder, in T job = default) where T : struct, IJobFor2Aspects1Components<A0,A1, C0> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where C0 : unmanaged, IComponentBase;
        
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoAspectsComponents2_1<T, A0,A1, C0>()
                where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase
                where T : struct, IJobFor2Aspects1Components<A0,A1, C0>;
    }

    public static unsafe partial class JobAspectsComponentsExtensions2_1 {
        
        public static partial void JobEarlyInitialize<T, A0,A1, C0>()
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor2Aspects1Components<A0,A1, C0>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, A0,A1, C0>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor2Aspects1Components<A0,A1, C0>;
    }
    
}