namespace ME.BECS.Views.Editor {

    using ME.BECS.Editor;
    
    public static class ViewsRegistry {
        
        public static EntityView GetEntityViewByPrefabId(uint prefabId) {

            return ObjectReferenceRegistry.GetObjectBySourceId<EntityView>(prefabId);

        }

        public static uint Assign(EntityView previousValue, EntityView newValue) {

            return ObjectReferenceRegistryUtils.Assign(previousValue, newValue);

        }

    }

}