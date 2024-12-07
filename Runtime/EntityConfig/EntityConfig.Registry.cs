namespace ME.BECS {

    public class EntityConfigRegistryShared {

        public static readonly Unity.Burst.SharedStatic<World> staticWorld = Unity.Burst.SharedStatic<World>.GetOrCreate<EntityConfigRegistryShared>();

    }

    public static unsafe class EntityConfigRegistry {

        private static ref World staticWorld => ref EntityConfigRegistryShared.staticWorld.Data;

        private static readonly System.Collections.Generic.Dictionary<EntityConfig, uint> registryToId = new System.Collections.Generic.Dictionary<EntityConfig, uint>();
        private static UIntDictionary<UnsafeEntityConfig> registryFromId;

        public static void Initialize() {
            //UnityEngine.Debug.Log("Initialize static world for configs");
            var props = WorldProperties.Default;
            props.name = "EntityConfig Static World";
            staticWorld = World.Create(props, false);
            registryFromId = new UIntDictionary<UnsafeEntityConfig>(ref staticWorld.state->allocator, 10u);
            registryToId.Clear();
        }
        
        public static uint Register(EntityConfig config, out UnsafeEntityConfig unsafeConfig) {

            if (staticWorld.isCreated == false) {
                Initialize();
            }
            
            if (registryToId.TryGetValue(config, out var id) == true) {
                unsafeConfig = registryFromId[in staticWorld.state->allocator, id];
                return id;
            }

            var staticConfigEnt = staticWorld.NewEnt();
            var nextId = ObjectReferenceRegistry.GetId(config);

            if (nextId == 0u) {
                //nextId = ObjectReferenceRegistry.AddRuntimeObject(config);
                throw new System.Exception($"ObjectReferenceRegistry does not contain Config {config.name}");
            }
            
            registryToId.Add(config, nextId);
            unsafeConfig = config.CreateUnsafeConfig(nextId, staticConfigEnt);
            registryFromId.Add(ref staticWorld.state->allocator, nextId, unsafeConfig);
            EntityConfigsRegistry.TryAdd(nextId, unsafeConfig);
            Batches.Apply(staticWorld.state);
            return nextId;

        }

        public static void Sync(EntityConfig config) {

            if (registryToId.TryGetValue(config, out var id) == true) {
                if (registryFromId.TryGetValue(in staticWorld.state->allocator, id, out var unsafeEntityConfig) == true) {
                    unsafeEntityConfig.Dispose();
                    unsafeEntityConfig = new UnsafeEntityConfig(config, id);
                    registryFromId[in staticWorld.state->allocator, id] = unsafeEntityConfig;
                }
            }

        }

    }

}