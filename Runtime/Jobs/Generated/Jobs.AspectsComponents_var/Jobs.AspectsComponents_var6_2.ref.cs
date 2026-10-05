// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.AspectsComponents_var.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectsComponentsExtensions6_2.JobProcess<,,,,,,,,>))]
    public interface IJobFor6Aspects2Components<A0,A1,A2,A3,A4,A5, C0,C1> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0,ref A1 a1,ref A2 a2,ref A3 a3,ref A4 a4,ref A5 a5, ref C0 c0,ref C1 c1);
    }

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions6_2 {
        
        public static partial JobHandle Schedule<T, A0,A1,A2,A3,A4,A5, C0,C1>(this QueryBuilder builder, in T job = default) where T : struct, IJobFor6Aspects2Components<A0,A1,A2,A3,A4,A5, C0,C1> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase;
        
        #if !ENABLE_BECS_FLAT_QUERIES
        public static partial JobHandle Schedule<T, A0,A1,A2,A3,A4,A5, C0,C1>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobFor6Aspects2Components<A0,A1,A2,A3,A4,A5, C0,C1> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase;
        
        public static partial JobHandle Schedule<T, A0,A1,A2,A3,A4,A5, C0,C1>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobFor6Aspects2Components<A0,A1,A2,A3,A4,A5, C0,C1> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase;

        public static partial JobHandle Schedule<T, A0,A1,A2,A3,A4,A5, C0,C1>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobFor6Aspects2Components<A0,A1,A2,A3,A4,A5, C0,C1> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase;
        #endif
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoAspectsComponents6_2<T, A0,A1,A2,A3,A4,A5, C0,C1>()
                where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase
                where T : struct, IJobFor6Aspects2Components<A0,A1,A2,A3,A4,A5, C0,C1>;
    }

    public static unsafe partial class JobAspectsComponentsExtensions6_2 {
        
        public static partial void JobEarlyInitialize<T, A0,A1,A2,A3,A4,A5, C0,C1>()
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase
            where T : struct, IJobFor6Aspects2Components<A0,A1,A2,A3,A4,A5, C0,C1>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, A0,A1,A2,A3,A4,A5, C0,C1>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where A4 : unmanaged, IAspect where A5 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase
            where T : struct, IJobFor6Aspects2Components<A0,A1,A2,A3,A4,A5, C0,C1>;
    }
    
}