namespace ME.BECS.Views.Editor {

    using ME.BECS.Editor;
    
    /// <summary>
    /// Registers and resolves views entries.
    /// </summary>
    public static class ViewsRegistry {
        
        /// <summary>
        /// Returns entity view by prefab ID.
        /// </summary>
        public static EntityView GetEntityViewByPrefabId(uint prefabId) {

            return ObjectReferenceRegistry.GetObjectBySourceId<EntityView>(prefabId);

        }

        /// <summary>
        /// Assigns the supplied state to its destination.
        /// </summary>
        public static uint Assign(EntityView previousValue, EntityView newValue) {

            return ObjectReferenceRegistryUtils.Assign(previousValue, newValue);

        }

    }

}