namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public class EntityConfigsRegistry {

        private static readonly SharedStatic<UnsafeHashMap<uint, UnsafeEntityConfig>> configs = SharedStatic<UnsafeHashMap<uint, UnsafeEntityConfig>>.GetOrCreate<EntityConfigsRegistry>();

        private static EntityConfigsRegistryData data;
        
        public static void Initialize() {

            if (data != null) {
                InitializeConfigs();
                return;
            }
            
            {
                // Validate Resources directory
                #if UNITY_EDITOR
                var dir = "Resources";
                if (UnityEditor.AssetDatabase.IsValidFolder("Assets/" + dir) == false) {
                    UnityEditor.AssetDatabase.CreateFolder("Assets", dir);
                }

                var obj = UnityEngine.Resources.Load<EntityConfigsRegistryData>("EntityConfigsRegistry");
                if (obj == null) {
                    var path = "Assets/Resources/EntityConfigsRegistry.asset";
                    var file = EntityConfigsRegistryData.CreateInstance<EntityConfigsRegistryData>();
                    UnityEditor.AssetDatabase.CreateAsset(file, path);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
                #endif
            }

            data = UnityEngine.Resources.Load<EntityConfigsRegistryData>("EntityConfigsRegistry");
            InitializeConfigs();
            
        }

        private static void InitializeConfigs() {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (data == null) return;

            try {

                configs.Data = new UnsafeHashMap<uint, UnsafeEntityConfig>(data.items.Length, Constants.ALLOCATOR_DOMAIN);
                foreach (var item in data.items) {
                    configs.Data.Add(item.sourceId, item.source.AsUnsafeConfig());
                }

            } catch (System.Exception ex) {

                UnityEngine.Debug.LogError("Error while initializing configs");
                UnityEngine.Debug.LogException(ex);

            }

        }

        [INLINE(256)]
        public static void TryAdd(uint sourceId, UnsafeEntityConfig unsafeEntityConfig) {
            configs.Data.TryAdd(sourceId, unsafeEntityConfig);
        }

        [INLINE(256)]
        public static UnsafeEntityConfig GetUnsafeEntityConfigBySourceId(uint sourceId) {

            configs.Data.TryGetValue(sourceId, out var config);
            return config;

        }

        [INLINE(256)]
        public static EntityConfig GetEntityConfigBySourceId(uint sourceId) {

            if (EntityConfigsRegistry.data == null) return null;
            
            return EntityConfigsRegistry.data.GetEntityConfigBySourceId(sourceId);

        }

        [INLINE(256)]
        public static uint Assign(EntityConfig previousValue, EntityConfig newValue) {
            
            if (EntityConfigsRegistry.data == null) return 0u;
            
            var removed = EntityConfigsRegistry.data.Remove(previousValue);
            var sourceId = EntityConfigsRegistry.data.Add(newValue, out bool isNew);

            if (isNew == true || removed == true) {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(EntityConfigsRegistry.data);
                #endif
            }

            return sourceId;

        }

    }

}