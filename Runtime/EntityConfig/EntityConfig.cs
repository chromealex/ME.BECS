using System.Linq;
using UnityEngine;

namespace ME.BECS {
    
    [CreateAssetMenu(menuName = "ME.BECS/Entity Config")]
    public class EntityConfig : ScriptableObject {

        [System.Serializable]
        public struct CollectionsData {

            [System.Serializable]
            public struct Collection {

                public uint id;
                [UnityEngine.SerializeReference]
                public System.Collections.Generic.List<object> array;

            }

            public uint nextId;
            public System.Collections.Generic.List<Collection> items;

            public void CleanUp(EntityConfig config) {

                if (this.items == null) return;
                // Clean up unused collections
                var used = new System.Collections.Generic.HashSet<uint>(this.items.Count);
                foreach (var item in this.items) {
                    used.Add(item.id);
                }

                foreach (var comp in config.data.components) {
                    if (comp == null) continue;
                    var ids = this.GetCollectionIds(comp);
                    foreach (var id in ids) {
                        used.Remove(id);
                    }
                }

                foreach (var comp in config.staticData.components) {
                    if (comp == null) continue;
                    var ids = this.GetCollectionIds(comp);
                    foreach (var id in ids) {
                        used.Remove(id);
                    }
                }

                if (used.Count > 0) {
                    for (int i = this.items.Count - 1; i >= 0; --i) {
                        var item = this.items[i];
                        if (used.Contains(item.id) == true) {
                            this.items.RemoveAt(i);
                            used.Remove(item.id);
                            Logger.Editor.Log($"[ EntityConfig ] Removed unused array from {config.name} with id {item.id}");
                            continue;
                        }
                        this.items[i] = item;
                    }
                }
            }

            private System.Collections.Generic.List<uint> GetCollectionIds(object comp) {
                var list = new System.Collections.Generic.List<uint>();
                this.GetCollectionIds(comp, list);
                return list;
            }

            private void GetCollectionIds(object comp, System.Collections.Generic.List<uint> collect) {
                var fields = comp.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var field in fields) {
                    if (typeof(IUnmanagedList).IsAssignableFrom(field.FieldType) == true) {
                        collect.Add(((IUnmanagedList)field.GetValue(comp)).GetConfigId());
                    } else if (field.FieldType.IsPrimitive == false) {
                        this.GetCollectionIds(field.GetValue(comp), collect);
                    }
                }
            }

        }

        public EntityConfig baseConfig;
        public ComponentsStorage<IConfigComponent> data = new() { components = System.Array.Empty<IConfigComponent>() };
        public ComponentsStorage<IConfigComponentShared> sharedData = new() { components = System.Array.Empty<IConfigComponentShared>() };
        public ComponentsStorage<IConfigComponentStatic> staticData = new() { components = System.Array.Empty<IConfigComponentStatic>() };
        public ComponentsStorage<IConfigInitialize> dataInitialize = new() { components = System.Array.Empty<IConfigInitialize>() };
        public ComponentsStorage<IAspect> aspects = new() { components = System.Array.Empty<IAspect>() };
        public CollectionsData collectionsData;

        public void Validate() {
            this.OnValidate();
            this.collectionsData.CleanUp(this);
        }

        public void OnValidate() {
            var list = new System.Collections.Generic.List<IConfigInitialize>();
            list.AddRange(this.data.components.OfType<IConfigInitialize>());
            list.AddRange(this.sharedData.components.OfType<IConfigInitialize>());
            list.AddRange(this.staticData.components.OfType<IConfigInitialize>());
            this.dataInitialize = new ComponentsStorage<IConfigInitialize>() {
                components = list.ToArray(),
            };
        }

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

        public void Apply(in Ent ent, Config.JoinOptions options = Config.JoinOptions.FullJoin) {
            
            E.IS_ALIVE(in ent);
            E.IS_CREATED(in ent.World);

            EntityConfigRegistry.Register(this, out var unsafeConfig);
            unsafeConfig.Apply(in ent, options);
            
        }

        public uint GetCollection(uint id, out CollectionsData.Collection data, out int index) {
            if (id == 0u) id = ++this.collectionsData.nextId;
            if (this.collectionsData.items == null) this.collectionsData.items = new System.Collections.Generic.List<CollectionsData.Collection>();
            for (var i = 0; i < this.collectionsData.items.Count; ++i) {
                var item = this.collectionsData.items[i];
                if (item.id == id) {
                    data = item;
                    index = i;
                    return id;
                }
            }

            data = new CollectionsData.Collection() {
                id = id,
                array = new System.Collections.Generic.List<object>(),
            };
            index = this.collectionsData.items.Count;
            this.collectionsData.items.Add(data);
            return id;
        }

    }

}