using UnityEngine;

namespace ME.BECS {
    
    [CreateAssetMenu(menuName = "ME.BECS/Entity Config")]
    public class EntityConfig : ScriptableObject {

        public EntityConfig baseConfig;
        public ComponentsStorage<IConfigComponent> data = new() { isShared = false, components = System.Array.Empty<IConfigComponent>() };
        public ComponentsStorage<IConfigComponentShared> sharedData = new() { isShared = true, components = System.Array.Empty<IConfigComponentShared>() };
        public ComponentsStorage<IStaticComponent> staticData = new() { isShared = false, components = System.Array.Empty<IStaticComponent>() };

        public void ResetCache() {
            this.data.ResetCache();
            this.staticData.ResetCache();
        }

        public void Apply(in Ent ent) {
            
            E.IS_ALIVE(in ent);
            E.IS_CREATED(in ent.World);

            var id = EntityConfigRegistry.Register(this);
            ent.Set(new EntityConfigComponent() {
                id = id,
            });
            
            this.data.Apply(in ent);
            this.sharedData.Apply(in ent);
            if (this.baseConfig != null) this.baseConfig.Apply(in ent);
            
        }

        public T ReadStatic<T>() where T : unmanaged, IStaticComponent {

            if (this.staticData.TryRead<T>(out var data) == true) {
                return data;
            }

            if (this.baseConfig != null) {
                return this.baseConfig.ReadStatic<T>();
            }
            
            return default;

        }

        public bool HasStatic<T>() where T : unmanaged, IStaticComponent {

            if (this.staticData.Has<T>() == true) {
                return true;
            }

            if (this.baseConfig != null) {
                return this.baseConfig.HasStatic<T>();
            }
            
            return false;

        }

    }

}