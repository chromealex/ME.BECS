namespace ME.BECS {

    using Extensions.SubclassSelector;

    [System.Serializable]
    public unsafe struct ComponentsStorage<T> where T : class {

        private class CacheData {

            private readonly System.Collections.Generic.Dictionary<System.Type, CacheBase> dictionary = new System.Collections.Generic.Dictionary<System.Type, CacheBase>();

            public CacheData(T[] components) {

                var types = new System.Type[1];
                for (var i = 0; i < components.Length; ++i) {
                    
                    var component = components[i];
                    var type = component.GetType();
                    types[0] = type;
                    var gType = typeof(CacheData<>).MakeGenericType(types);
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

        public void BuildCache() {

            if (this.cache == null) {

                this.cache = new CacheData(this.components);
                
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