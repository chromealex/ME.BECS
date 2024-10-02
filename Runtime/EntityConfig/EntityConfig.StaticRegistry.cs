namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public class EntityConfigsRegistry {

        private static readonly SharedStatic<UnsafeHashMap<uint, UnsafeEntityConfig>> configs = SharedStatic<UnsafeHashMap<uint, UnsafeEntityConfig>>.GetOrCreate<EntityConfigsRegistry>();
        
        #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        #endif
        public static void Initialize() {
            
            InitializeConfigs();
            
        }

        private static void InitializeConfigs() {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (ObjectReferenceRegistry.data == null) return;

            try {

                configs.Data = new UnsafeHashMap<uint, UnsafeEntityConfig>(ObjectReferenceRegistry.data.items.Length, Constants.ALLOCATOR_DOMAIN);
                foreach (var item in ObjectReferenceRegistry.data.items) {
                    if (item.source is EntityConfig entityConfig) {
                        configs.Data.TryAdd(item.sourceId, entityConfig.AsUnsafeConfig());
                    }
                }

            } catch (System.Exception ex) {

                UnityEngine.Debug.LogError("Error while initializing configs");
                UnityEngine.Debug.LogException(ex);

            }

        }

        [INLINE(256)]
        public static void TryAdd(uint sourceId, UnsafeEntityConfig unsafeEntityConfig) {
            if (configs.Data.TryAdd(sourceId, unsafeEntityConfig) == false) {
                configs.Data[sourceId] = unsafeEntityConfig;
            }
        }

        [INLINE(256)]
        public static UnsafeEntityConfig GetUnsafeEntityConfigBySourceId(uint sourceId) {

            configs.Data.TryGetValue(sourceId, out var config);
            return config;

        }

        [INLINE(256)]
        public static EntityConfig GetEntityConfigBySourceId(uint sourceId) {

            return ObjectReferenceRegistry.GetObjectBySourceId<EntityConfig>(sourceId);
            
        }

        [INLINE(256)]
        public static uint Assign(EntityConfig previousValue, EntityConfig newValue) {

            return ObjectReferenceRegistry.Assign(previousValue, newValue);

        }

    }

}