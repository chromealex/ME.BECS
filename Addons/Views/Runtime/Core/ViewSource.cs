namespace ME.BECS.Views {

    [System.Serializable]
    public struct View {

        public ViewSource viewSource;

        public bool IsValid => this.viewSource.IsValid;

        public static implicit operator ViewSource(View view) {
            return view.viewSource;
        }
        
    }

    public static class ViewsRegistry {
        
        public static EntityView GetEntityViewByPrefabId(uint prefabId) {

            return BECS.ObjectReferenceRegistry.GetObjectBySourceId<EntityView>(prefabId);

        }

        public static uint Assign(EntityView previousValue, EntityView newValue) {

            return BECS.ObjectReferenceRegistry.Assign(previousValue, newValue);

        }

    }

}