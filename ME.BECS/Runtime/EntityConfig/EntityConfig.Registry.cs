namespace ME.BECS {

    public static class EntityConfigRegistry {

        public static System.Collections.Generic.Dictionary<EntityConfig, uint> registryToId = new System.Collections.Generic.Dictionary<EntityConfig, uint>();
        public static System.Collections.Generic.Dictionary<uint, EntityConfig> registryFromId = new System.Collections.Generic.Dictionary<uint, EntityConfig>();
        public static uint nextId;

        public static uint Register(EntityConfig config) {

            if (registryToId.TryGetValue(config, out var id) == true) {
                return id;
            }

            ++nextId;
            registryToId.Add(config, nextId);
            registryFromId.Add(nextId, config);
            return nextId;

        }

        public static EntityConfig GetId(uint id) {
            
            if (registryFromId.TryGetValue(id, out var config) == true) {
                return config;
            }

            return null;

        }

    }

}