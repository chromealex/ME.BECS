// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.AspectsComponents_var.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectsComponentsExtensions4_3.JobProcess<,,,,,,,>))]
    public interface IJobFor4Aspects3Components<A0,A1,A2,A3, C0,C1,C2> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0,ref A1 a1,ref A2 a2,ref A3 a3, ref C0 c0,ref C1 c1,ref C2 c2);
    }

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions4_3 {
        
        public static partial JobHandle Schedule<T, A0,A1,A2,A3, C0,C1,C2>(this QueryBuilder builder, in T job = default) where T : struct, IJobFor4Aspects3Components<A0,A1,A2,A3, C0,C1,C2> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase;
        
        #if !ENABLE_BECS_FLAT_QUERIES
        public static partial JobHandle Schedule<T, A0,A1,A2,A3, C0,C1,C2>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobFor4Aspects3Components<A0,A1,A2,A3, C0,C1,C2> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase;
        
        public static partial JobHandle Schedule<T, A0,A1,A2,A3, C0,C1,C2>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobFor4Aspects3Components<A0,A1,A2,A3, C0,C1,C2> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase;

        public static partial JobHandle Schedule<T, A0,A1,A2,A3, C0,C1,C2>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobFor4Aspects3Components<A0,A1,A2,A3, C0,C1,C2> where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase;
        #endif
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoAspectsComponents4_3<T, A0,A1,A2,A3, C0,C1,C2>()
                where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase
                where T : struct, IJobFor4Aspects3Components<A0,A1,A2,A3, C0,C1,C2>;
    }

    public static unsafe partial class JobAspectsComponentsExtensions4_3 {
        
        public static partial void JobEarlyInitialize<T, A0,A1,A2,A3, C0,C1,C2>()
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase
            where T : struct, IJobFor4Aspects3Components<A0,A1,A2,A3, C0,C1,C2>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, A0,A1,A2,A3, C0,C1,C2>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect where A1 : unmanaged, IAspect where A2 : unmanaged, IAspect where A3 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase where C1 : unmanaged, IComponentBase where C2 : unmanaged, IComponentBase
            where T : struct, IJobFor4Aspects3Components<A0,A1,A2,A3, C0,C1,C2>;
    }
    
}