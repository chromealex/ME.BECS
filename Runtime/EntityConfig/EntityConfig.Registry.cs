namespace ME.BECS {

    public class EntityConfigRegistryShared {

        public static readonly Unity.Burst.SharedStatic<World> staticWorld = Unity.Burst.SharedStatic<World>.GetOrCreate<EntityConfigRegistryShared>();

    }

    public static unsafe class EntityConfigRegistry {

        private static ref World staticWorld => ref EntityConfigRegistryShared.staticWorld.Data;

        private static readonly System.Collections.Generic.Dictionary<EntityConfig, uint> registryToId = new System.Collections.Generic.Dictionary<EntityConfig, uint>();
        private static UIntDictionary<UnsafeEntityConfig> registryFromId;
        private static LockSpinner lockSpinner = new LockSpinner();

        public static void Initialize() {
            //UnityEngine.Debug.Log("Initialize static world for configs");
            lockSpinner.Lock();
            var props = WorldProperties.Default;
            props.name = "EntityConfig Static World";
            staticWorld = World.Create(props, false);
            registryFromId = new UIntDictionary<UnsafeEntityConfig>(ref staticWorld.state.ptr->allocator, 10u);
            registryToId.Clear();
            lockSpinner.Unlock();
        }
        
        public static uint Register(EntityConfig config, out UnsafeEntityConfig unsafeConfig) {

            if (staticWorld.isCreated == false) {
                Initialize();
            }
            
            if (registryToId.TryGetValue(config, out var id) == true) {
                unsafeConfig = registryFromId[in staticWorld.state.ptr->allocator, id];
                return id;
            }

            var staticConfigEnt = staticWorld.NewEnt();
            var nextId = ObjectReferenceRegistry.GetId(config);

            if (nextId == 0u) {
                //nextId = ObjectReferenceRegistry.AddRuntimeObject(config);
                throw new System.Exception($"ObjectReferenceRegistry does not contain Config {config.name}");
            }
            
            lockSpinner.Lock();
            registryToId.Add(config, nextId);
            unsafeConfig = config.CreateUnsafeConfig(nextId, staticConfigEnt);
            registryFromId.Add(ref staticWorld.state.ptr->allocator, nextId, unsafeConfig);
            EntityConfigsRegistry.TryAdd(nextId, unsafeConfig);
            Batches.Apply(staticWorld.state);
            lockSpinner.Unlock();
            return nextId;

        }

        public static void Sync(EntityConfig config) {
            
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