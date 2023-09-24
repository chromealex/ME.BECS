using UnityEngine;

namespace ME.BECS {
    
    [CreateAssetMenu(menuName = "ME.BECS/Entity Config")]
    public class EntityConfig : ScriptableObject {

        public EntityConfig baseConfig;
        public ComponentsStorage<IConfigComponent> data = new() { isShared = false, components = System.Array.Empty<IConfigComponent>() };
        public ComponentsStorage<IConfigComponentShared> sharedData = new() { isShared = true, components = System.Array.Empty<IConfigComponentShared>() };
        public ComponentsStorage<IComponentStatic> staticData = new() { isShared = false, components = System.Array.Empty<IComponentStatic>() };

        public UnsafeEntityConfig CreateUnsafeConfig(uint id = 0u, Ent ent = default) {
            return new UnsafeEntityConfig(this, id, ent);
        }
        
        public void Sync() {

            EntityConfigRegistry.Sync(this);
            
        }

        public UnsafeEntityConfig AsUnsafeConfig() {
            EntityConfigRegistry.Register(this, out var config);
            return config;
        }

        public void ResetCache() {
            this.data.ResetCache();
            this.staticData.ResetCache();
        }

        public void Apply(in Ent ent) {
            
            E.IS_ALIVE(in ent);
            E.IS_CREATED(in ent.World);

            var id = EntityConfigRegistry.Register(this, out var unsafeConfig);
            ent.Set(new EntityConfigComponent() {
                id = id,
            });
            
            unsafeConfig.Apply(in ent);
            
        }

    }

}