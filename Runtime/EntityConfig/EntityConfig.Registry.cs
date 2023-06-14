namespace ME.BECS {

    public static class EntityConfigRegistry {

        public static System.Collections.Generic.Dictionary<EntityConfig, uint> registryToId = new System.Collections.Generic.Dictionary<EntityConfig, uint>();
        public static System.Collections.Generic.Dictionary<uint, UnsafeEntityConfig> registryFromId = new System.Collections.Generic.Dictionary<uint, UnsafeEntityConfig>();
        public static uint nextId;

        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void CleanUp() {
            registryFromId.Clear();
            registryToId.Clear();
            nextId = 0u;
        }
        
        public static uint Register(EntityConfig config, out UnsafeEntityConfig unsafeConfig) {

            if (registryToId.TryGetValue(config, out var id) == true) {
                unsafeConfig = registryFromId[id];
                return id;
            }

            ++nextId;
            registryToId.Add(config, nextId);
            unsafeConfig = config.CreateUnsafeConfig(nextId);
            registryFromId.Add(nextId, unsafeConfig);
            return nextId;

        }

        public static void Sync(EntityConfig config) {

            if (registryToId.TryGetValue(config, out var id) == true) {
                if (registryFromId.TryGetValue(id, out var unsafeEntityConfig) == true) {
                    unsafeEntityConfig.Dispose();
                    unsafeEntityConfig = new UnsafeEntityConfig(config, id);
                    registryFromId[id] = unsafeEntityConfig;
                }
            }

        }

        public static UnsafeEntityConfig GetById(uint id) {
            
            if (registryFromId.TryGetValue(id, out var config) == true) {
                return config;
            }

            return default;

        }

    }

}