namespace ME.BECS {
    
    using ME.BECS.Jobs;

    public static class EarlyInit {

        public static void Do<TJob, T>() where TJob : struct, IJobParallelForComponents<T> where T : unmanaged, IComponent {
            
            JobParallelForComponentsExtensions.JobEarlyInitialize<TJob, T>();
            
        }

    }

}