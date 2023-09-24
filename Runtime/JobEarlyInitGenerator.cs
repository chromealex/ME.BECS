namespace ME.BECS {
    
    using ME.BECS.Jobs;

    public class JobEarlyInitGenerator {

        public static void Init<TJob, TAspect>() where TJob : struct, IJobParallelForAspect<TAspect> where TAspect : unmanaged, IAspect {
            
            JobParallelForAspectExtensions_1.JobEarlyInitialize<TJob, TAspect>();
            
        }

    }

}