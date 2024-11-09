namespace ME.BECS {

    public static class EntAutoDestroyExt {

        public static unsafe void RegisterAutoDestroy<T>(this in Ent ent) where T : unmanaged, IComponentDestroy {
            
            AutoDestroyRegistry.Add(ent.World.state, in ent, StaticTypes<T>.typeId);
            
        }

        public static unsafe void UnRegisterAutoDestroy<T>(this in Ent ent) where T : unmanaged, IComponentDestroy {
            
            AutoDestroyRegistry.Remove(ent.World.state, in ent, StaticTypes<T>.typeId);
            
        }

    }
    
}