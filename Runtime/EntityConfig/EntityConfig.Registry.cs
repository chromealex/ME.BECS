namespace ME.BECS {

    /// <summary>
    /// Stores shared registration state for entity configurations.
    /// </summary>
    public class EntityConfigRegistryShared {

        /// <summary>
        /// Static world used by <c>EntityConfigRegistryShared</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<World> staticWorld = Unity.Burst.SharedStatic<World>.GetOrCreate<EntityConfigRegistryShared>();

    }

    /// <summary>
    /// Resolves entity configurations registered for runtime use.
    /// </summary>
    public static unsafe class EntityConfigRegistry {

        private static ref World staticWorld => ref EntityConfigRegistryShared.staticWorld.Data;

        private static readonly System.Collections.Generic.Dictionary<EntityConfig, uint> registryToId = new System.Collections.Generic.Dictionary<EntityConfig, uint>();
        private static UIntDictionary<UnsafeEntityConfig> registryFromId;
        private static LockSpinner lockSpinner = new LockSpinner();

        /// <summary>
        /// Initializes entity config registry state from the supplied context.
        /// </summary>
        public static void Initialize() {
            //UnityEngine.Debug.Log("Initialize static world for configs");
            lockSpinner.Lock();
            try {
                if (staticWorld.isCreated == false) {
                    var props = WorldProperties.Default;
                    props.name = "EntityConfig Static World";
                    props.stateProperties.allowStaticStorage = true;
                    staticWorld = World.Create(props, switchContext: false);
                    registryFromId = new UIntDictionary<UnsafeEntityConfig>(ref staticWorld.state.ptr->allocator, 10u);
                    registryToId.Clear();
                }
            } finally {
                lockSpinner.Unlock();
            }
        }
        
        /// <summary>
        /// Registers the supplied instance or type for subsequent lookup.
        /// </summary>
        public static uint Register(EntityConfig config, out UnsafeEntityConfig unsafeConfig) {

            if (staticWorld.isCreated == false) {
                Initialize();
            }
            
            if (registryToId.TryGetValue(config, out var id) == true) {
                unsafeConfig = registryFromId[in staticWorld.state.ptr->allocator, id];
                return id;
            }

            var nextId = ObjectReferenceRegistry.GetId(config);

            if (nextId == 0u) {
                if (UnityEngine.Application.isPlaying == true) {
                    nextId = ObjectReferenceRegistry.AddRuntimeObject(config);
                } else {
                    throw new System.Exception($"ObjectReferenceRegistry does not contain Config {config.name}");
                }
            }
            
            lockSpinner.Lock();
            try {
                config.ValidateCollectionTypes();
                var staticConfigEnt = staticWorld.NewEnt();
                unsafeConfig = config.CreateUnsafeConfig(nextId, staticConfigEnt);
                registryToId.Add(config, nextId);
                registryFromId.Add(ref staticWorld.state.ptr->allocator, nextId, unsafeConfig);
                EntityConfigsRegistry.TryAdd(nextId, unsafeConfig);
                Batches.Apply(in staticWorld);
                return nextId;
            } finally {
                lockSpinner.Unlock();
            }

        }

        /// <summary>
        /// Synchronizes the associated state with the supplied source.
        /// </summary>
        public static void Sync(EntityConfig config) {
            config.ValidateCollectionTypes();
            lockSpinner.Lock();
            if (registryToId.TryGetValue(config, out var id) == true) {
                if (registryFromId.TryGetValue(in staticWorld.state.ptr->allocator, id, out var unsafeEntityConfig) == true) {
                    lockSpinner.Unlock();
                    var staticEntity = unsafeEntityConfig.GetStaticEntity();
                    unsafeEntityConfig.Dispose();
                    unsafeEntityConfig = new UnsafeEntityConfig(config, id, staticEntity);
                    lockSpinner.Lock();
                    registryFromId[in staticWorld.state.ptr->allocator, id] = unsafeEntityConfig;
                }
            }
            lockSpinner.Unlock();

        }

    }

}
