namespace ME.BECS {

    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    public static class EntAutoDestroyExt {

        [IgnoreProfiler]
        public static unsafe void RegisterAutoDestroy<T>(this in Ent ent) where T : unmanaged, IComponentDestroy {
            
            AutoDestroyRegistry.Add(ent.World.state, in ent, StaticTypes<T>.typeId);
            
        }

        [IgnoreProfiler]
        public static unsafe void UnRegisterAutoDestroy<T>(this in Ent ent) where T : unmanaged, IComponentDestroy {
            
            AutoDestroyRegistry.Remove(ent.World.state, in ent, StaticTypes<T>.typeId);
            
        }

    }
    
}