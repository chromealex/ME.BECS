using UnityEngine;

namespace ME.BECS {
    
    /// <summary>
    /// Supplies entity config ID metadata to annotated declarations.
    /// </summary>
    public class EntityConfigIdAttribute : PropertyAttribute {}
    
    /// <summary>
    /// Defines reusable component data and initialization settings for entities.
    /// </summary>
    [CreateAssetMenu(menuName = "ME.BECS/Entity Config")]
    public class EntityConfig : ScriptableObject {

        /// <summary>
        /// Stores the descriptors used by registered collection operations.
        /// </summary>
        [System.Serializable]
        public struct CollectionsData {

            /// <summary>
            /// Defines collection state and operations for <c>EntityConfig.CollectionsData</c>.
            /// </summary>
            [System.Serializable]
            public struct Collection {

                /// <summary>
                /// Identifier used to address this entry within its containing registry.
                /// </summary>
                public uint id;
                /// <summary>
                /// Backing array used by this value.
                /// </summary>
                [UnityEngine.SerializeReference]
                public System.Collections.Generic.List<object> array;

            }

            /// <summary>
            /// Next id used to locate the associated entry.
            /// </summary>
            public uint nextId;
            /// <summary>
            /// Entries stored by this container.
            /// </summary>
            public System.Collections.Generic.List<Collection> items;

            /// <summary>
            /// Clears state and releases resources managed by this operation.
            /// </summary>
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

                foreach (var comp in config.sharedData.components) {
                    if (comp == null) continue;
                    foreach (var id in this.GetCollectionIds(comp)) used.Remove(id);
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
                if (comp == null) return;
                var fields = comp.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var field in fields) {
                    if (typeof(IUnmanagedList).IsAssignableFrom(field.FieldType) == true) {
                        var id = ((IUnmanagedList)field.GetValue(comp)).GetConfigId();
                        if (collect.Contains(id) == true) continue;
                        collect.Add(id);
                        var collection = this.items.Find(item => item.id == id);
                        if (collection.array == null) continue;
                        foreach (var item in collection.array) this.GetCollectionIds(item, collect);
                    } else if (field.FieldType.IsValueType == true && field.FieldType.IsPrimitive == false && field.FieldType.IsEnum == false) {
                        this.GetCollectionIds(field.GetValue(comp), collect);
                    }
                }
            }

        }

        /// <summary>
        /// Base config used by <c>EntityConfig</c>.
        /// </summary>
        public EntityConfig baseConfig;
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public ComponentsStorage<IConfigComponent> data = new() { components = System.Array.Empty<IConfigComponent>() };
        /// <summary>
        /// Shared data used by <c>EntityConfig</c>.
        /// </summary>
        public ComponentsStorage<IConfigComponentShared> sharedData = new() { components = System.Array.Empty<IConfigComponentShared>() };
        /// <summary>
        /// Static data used by <c>EntityConfig</c>.
        /// </summary>
        public ComponentsStorage<IConfigComponentStatic> staticData = new() { components = System.Array.Empty<IConfigComponentStatic>() };
        /// <summary>
        /// Data initialize used by <c>EntityConfig</c>.
        /// </summary>
        public ComponentsStorageLink dataInitialize = new() { items = System.Array.Empty<ComponentsStorageLink.Item>() };
        /// <summary>
        /// Aspect descriptors used by this operation.
        /// </summary>
        public ComponentsStorage<IAspect> aspects = new() { components = System.Array.Empty<IAspect>() };
        /// <summary>
        /// Collections data used by <c>EntityConfig</c>.
        /// </summary>
        public CollectionsData collectionsData;
        /// <summary>
        /// Whether maskable behavior or state is selected.
        /// </summary>
        public bool maskable;
        
        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
        public void Validate() {
            this.OnValidate();
            this.collectionsData.CleanUp(this);
        }

        // Config collections contain boxed serialized values, not necessarily the T
        // currently declared by their fields. Check before allocating or copying native data.
        internal void ValidateCollectionTypes() {
            for (var config = this; config != null; config = config.baseConfig) {
                if (config.collectionsData.items == null) {
                    continue;
                }
                var visited = new System.Collections.Generic.HashSet<uint>();
                foreach (var component in config.data.components) {
                    config.ValidateCollectionFields(component, component?.GetType().FullName, visited);
                }
                foreach (var component in config.sharedData.components) {
                    config.ValidateCollectionFields(component, component?.GetType().FullName, visited);
                }
                foreach (var component in config.staticData.components) {
                    config.ValidateCollectionFields(component, component?.GetType().FullName, visited);
                }
                // Unreferenced collections are packed too; their first element sets the stride.
                foreach (var collection in config.collectionsData.items) {
                    var type = collection.array != null && collection.array.Count > 0 ? collection.array[0]?.GetType() : null;
                    config.ValidateCollectionElements(collection, type, "serialized collection");
                }
            }
        }

        private void ValidateCollectionFields(object value, string path, System.Collections.Generic.HashSet<uint> visited) {
            if (value == null) {
                return;
            }
            foreach (var field in value.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)) {
                if (typeof(IUnmanagedList).IsAssignableFrom(field.FieldType) == true) {
                    var id = ((IUnmanagedList)field.GetValue(value)).GetConfigId();
                    if (id == 0u) {
                        continue;
                    }
                    var collection = this.collectionsData.items.Find(item => item.id == id);
                    if (collection.array == null) {
                        continue;
                    }
                    var fieldPath = path + "." + field.Name;
                    this.ValidateCollectionElements(collection, field.FieldType.GetGenericArguments()[0], fieldPath);
                    // Validate each reference's expected T, even when another field uses the same ID.
                    if (visited.Add(id) == true) {
                        for (var i = 0; i < collection.array.Count; ++i) {
                            this.ValidateCollectionFields(collection.array[i], fieldPath + "[" + i + "]", visited);
                        }
                    }
                } else if (field.FieldType.IsValueType == true && field.FieldType.IsPrimitive == false && field.FieldType.IsEnum == false) {
                    this.ValidateCollectionFields(field.GetValue(value), path + "." + field.Name, visited);
                }
            }
        }

        private void ValidateCollectionElements(CollectionsData.Collection collection, System.Type expected, string path) {
            if (collection.array == null) {
                throw new System.ArgumentException($"Config '{this.name}', collection {collection.id} ({path}): null array.");
            }
            for (var i = 0; i < collection.array.Count; ++i) {
                var actual = collection.array[i]?.GetType();
                if (actual == null || actual != expected) {
                    throw new System.ArgumentException($"Config '{this.name}', collection {collection.id}, element {i} ({path}): expected {expected?.FullName ?? "non-null value"}, actual {actual?.FullName ?? "null"}.");
                }
            }
        }

        /// <summary>
        /// Refreshes or validates state after values change in the Unity Inspector.
        /// </summary>
        public void OnValidate() {
            var list = new System.Collections.Generic.List<ComponentsStorageLink.Item>();
            for (uint i = 0u; i < this.data.components.Length; ++i) {
                var item = this.data.components[i];
                if (item is IConfigInitialize) {
                    list.Add(new ComponentsStorageLink.Item() {
                        type = 0,
                        index = i,
                    });
                }
            }
            for (uint i = 0u; i < this.sharedData.components.Length; ++i) {
                var item = this.sharedData.components[i];
                if (item is IConfigInitialize) {
                    list.Add(new ComponentsStorageLink.Item() {
                        type = 1,
                        index = i,
                    });
                }
            }
            for (uint i = 0u; i < this.staticData.components.Length; ++i) {
                var item = this.staticData.components[i];
                if (item is IConfigInitialize) {
                    list.Add(new ComponentsStorageLink.Item() {
                        type = 2,
                        index = i,
                    });
                }
            }
            this.dataInitialize = new ComponentsStorageLink() {
                items = list.ToArray(),
            };
        }

        /// <summary>
        /// Creates unsafe config.
        /// </summary>
        public UnsafeEntityConfig CreateUnsafeConfig(uint id = 0u, Ent ent = default) {
            return new UnsafeEntityConfig(this, id, ent);
        }
        
        /// <summary>
        /// Synchronizes the associated state with the supplied source.
        /// </summary>
        public void Sync() {

            EntityConfigRegistry.Sync(this);
            
        }

        /// <summary>
        /// Resolves the configuration reference to its native configuration data.
        /// </summary>
        public UnsafeEntityConfig AsUnsafeConfig() {
            EntityConfigRegistry.Register(this, out var config);
            return config;
        }

        /// <summary>
        /// Applies the supplied data or pending changes to the target state.
        /// </summary>
        public void Apply(in Ent ent, Config.JoinOptions options = Config.JoinOptions.FullJoin) {
            
            E.IS_ALIVE(in ent);
            E.IS_CREATED(in ent.World);

            EntityConfigRegistry.Register(this, out var unsafeConfig);
            unsafeConfig.Apply(in ent, options);
            
        }

        /// <summary>
        /// Returns collection.
        /// </summary>
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
