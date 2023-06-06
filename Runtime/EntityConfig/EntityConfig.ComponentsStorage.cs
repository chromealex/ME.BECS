namespace ME.BECS {

    using Extensions.SubclassSelector;

    internal interface IConfigComponentsStorage {

        void ResetCache();

    }
    
    [System.Serializable]
    public unsafe struct ComponentsStorage<T> : IConfigComponentsStorage where T : class {

        public void AOTStatic<TComponent>() where TComponent : unmanaged, IComponentStatic {
            
            new CacheData<TComponent>();
            
        }

        public void AOTShared<TComponent>() where TComponent : unmanaged, IComponentShared {
            
            new CacheSharedData<TComponent>();
            
        }

        public void AOT<TComponent>() where TComponent : unmanaged {
            
            new CacheData<TComponent>();
            
        }

        private class CacheData {

            private readonly System.Collections.Generic.Dictionary<System.Type, CacheBase> dictionary = new System.Collections.Generic.Dictionary<System.Type, CacheBase>();

            public CacheData(T[] components, bool isShared) {

                var types = new System.Type[1];
                for (var i = 0; i < components.Length; ++i) {
                    
                    var component = components[i];
                    if (component == null) continue;
                    var type = component.GetType();
                    types[0] = type;
                    System.Type gType;
                    if (isShared == true) {
                        gType = typeof(CacheSharedData<>).MakeGenericType(types);
                    } else {
                        gType = typeof(CacheData<>).MakeGenericType(types);
                    }
                    var item = (CacheBase)System.Activator.CreateInstance(gType);
                    item.BuildCache(component);
                    this.dictionary.Add(type, item);

                }

            }

            public void Apply(State* worldState, uint entId, ushort entGen) {

                foreach (var kv in this.dictionary) {
                    kv.Value.Apply(worldState, entId, entGen);
                }

            }

            public bool TryRead<TComponent>(out TComponent component) where TComponent : unmanaged, T {

                component = default;
                var type = typeof(TComponent);
                if (this.dictionary.TryGetValue(type, out var cacheBase) == true) {
                    component = ((CacheData<TComponent>)cacheBase).data;
                    return true;
                }
                return false;

            }

            public bool Has<TComponent>() where TComponent : unmanaged, T {

                var type = typeof(TComponent);
                return this.dictionary.ContainsKey(type);

            }

        }
        
        private CacheData cache;
        
        [SubclassSelector(unmanagedTypes: true, runtimeAssembliesOnly: true, showSelector: false)]
        [UnityEngine.SerializeReference]
        public T[] components;
        public bool isShared;

        public void ResetCache() {
            
            this.cache = null;
            
        }

        public void BuildCache() {

            if (this.cache == null) {
                this.cache = new CacheData(this.components, this.isShared);
            }

        }

        public void Apply(in Ent ent) {

            this.BuildCache();
            this.cache.Apply(ent.World.state, ent.id, ent.gen);

        }

        public bool TryRead<TComponent>(out TComponent component) where TComponent : unmanaged, T {

            this.BuildCache();
            return this.cache.TryRead(out component);

        }

        public bool Has<TComponent>() where TComponent : unmanaged, T {

            this.BuildCache();
            return this.cache.Has<TComponent>();

        }

    }

}