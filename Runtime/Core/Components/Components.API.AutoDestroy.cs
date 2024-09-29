namespace ME.BECS {

    public static class EntAutoDestroyExt {

        public static unsafe void RegisterAutoDestroy<T>(this in Ent ent) where T : unmanaged, IComponentDestroy {
            
            ent.World.state->autoDestroyRegistry.Add(ent.World.state, in ent, StaticTypes<T>.typeId);
            
        }

        public static unsafe void UnRegisterAutoDestroy<T>(this in Ent ent) where T : unmanaged, IComponentDestroy {
            
            ent.World.state->autoDestroyRegistry.Remove(ent.World.state, in ent, StaticTypes<T>.typeId);
            
        }

    }
    
}