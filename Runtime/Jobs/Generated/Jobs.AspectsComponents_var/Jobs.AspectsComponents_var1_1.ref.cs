// Public job contracts. JobBackendGenerator emits implementations from
// Jobs.AspectsComponents_var.Tpl.txt embedded in ME.BECS.SourceGenerator.dll.
namespace ME.BECS.Jobs {
    
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [JobProducerType(typeof(JobAspectsComponentsExtensions1_1.JobProcess<,,>))]
    public interface IJobFor1Aspects1Components<A0, C0> : IJobForAspectsComponentsBase where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase {
        void Execute(in JobInfo jobInfo, in Ent ent, ref A0 a0, ref C0 c0);
    }

    public static unsafe partial class QueryAspectsComponentsScheduleExtensions1_1 {
        
        public static partial JobHandle Schedule<T, A0, C0>(this QueryBuilder builder, in T job = default) where T : struct, IJobFor1Aspects1Components<A0, C0> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase;
        
        #if !ENABLE_BECS_FLAT_QUERIES
        public static partial JobHandle Schedule<T, A0, C0>(this Query staticQuery, in T job, in SystemContext context) where T : struct, IJobFor1Aspects1Components<A0, C0> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase;
        
        public static partial JobHandle Schedule<T, A0, C0>(this Query staticQuery, in T job, in World world, JobHandle dependsOn = default) where T : struct, IJobFor1Aspects1Components<A0, C0> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase;

        public static partial JobHandle Schedule<T, A0, C0>(this QueryBuilderDisposable staticQuery, in T job) where T : struct, IJobFor1Aspects1Components<A0, C0> where A0 : unmanaged, IAspect where C0 : unmanaged, IComponentBase;
        #endif
        
    }
    
    public static partial class EarlyInit {
        public static partial void DoAspectsComponents1_1<T, A0, C0>()
                where A0 : unmanaged, IAspect
                where C0 : unmanaged, IComponentBase
                where T : struct, IJobFor1Aspects1Components<A0, C0>;
    }

    public static unsafe partial class JobAspectsComponentsExtensions1_1 {
        
        public static partial void JobEarlyInitialize<T, A0, C0>()
            where A0 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor1Aspects1Components<A0, C0>;

        [CodeGeneratorIgnore]
        public static partial JobHandle Schedule<T, A0, C0>(this T jobData, CommandBuffer* buffer, bool unsafeMode, bool isReadonly, uint innerLoopBatchCount, ScheduleMode scheduleMode, JobHandle dependsOn = default)
            where A0 : unmanaged, IAspect
            where C0 : unmanaged, IComponentBase
            where T : struct, IJobFor1Aspects1Components<A0, C0>;
    }
    
}