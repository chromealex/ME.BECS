// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.AspectsComponents_var.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectsComponentsExtensions9_1.JobProcess<,,,,,,,,,,>))]
    public interface IJobFor9Aspects1Components<A0,A1,A2,A3,A4,A5,A6,A7,A8, C0> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where A6 : unmanaged, IAspect where A7 : unmanaged, IAspect where A8 : unmanaged, IAspect where C0 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0,ref A1 a1,ref A2 a2,ref A3 a3,ref A4 a4,ref A5 a5,ref A6 a6,ref A7 a7,ref A8 a8, ref C0 c0);
    }

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions9_1 {
        
        public static partial JobHandle Schedule<T, A0,A1,A2,A3,A4,A5,A6,A7,A8, C0>(this QueryBuilder builder, in T job = default) where T : struct, IJobFor9Aspects1Components<A0,A1,A2,A3,A4,A5,A6,A7,A8, C0> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where A6 : unmanaged, IAspect where A7 : unmanaged, IAspect where A8 : unmanaged, IAspect where C0 : unmanaged, IComponentBase;
        
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoAspectsComponents9_1<T, A0,A1,A2,A3,A4,A5,A6,A7,A8, C0>()
                where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where A6 : unmanaged, IAspect where A7 : unmanaged, IAspect where A8 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase
                where T : struct, IJobFor9Aspects1Components<A0,A1,A2,A3,A4,A5,A6,A7,A8, C0>;
    }

    public static unsafe partial class JobAspectsComponentsExtensions9_1 {
        
        public static partial void JobEarlyInitialize<T, A0,A1,A2,A3,A4,A5,A6,A7,A8, C0>()
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where A6 : unmanaged, IAspect where A7 : unmanaged, IAspect where A8 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor9Aspects1Components<A0,A1,A2,A3,A4,A5,A6,A7,A8, C0>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, A0,A1,A2,A3,A4,A5,A6,A7,A8, C0>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where A6 : unmanaged, IAspect where A7 : unmanaged, IAspect where A8 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor9Aspects1Components<A0,A1,A2,A3,A4,A5,A6,A7,A8, C0>;
    }
    
}