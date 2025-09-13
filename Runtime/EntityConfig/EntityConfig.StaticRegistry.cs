namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public class EntityConfigsRegistry {

        private static readonly SharedStatic<UnsafeHashMap<uint, UnsafeEntityConfig>> configs = SharedStatic<UnsafeHashMap<uint, UnsafeEntityConfig>>.GetOrCreate<EntityConfigsRegistry>();

        public static void Initialize(bool isEditor = false) {

            if (StaticTypes.tracker.IsCreated == false) return;
            
            ObjectReferenceRegistry.Load();
            Load(isEditor);
            
        }

        private static void Load(bool isEditor) {

            if (ObjectReferenceRegistry.data == null) return;
            LoadForced(isEditor);

        }

        private static void LoadForced(bool isEditor) {

            try {

                configs.Data = new UnsafeHashMap<uint, UnsafeEntityConfig>(ObjectReferenceRegistry.data.objects.Length, Constants.ALLOCATOR_DOMAIN);
                if (isEditor == false) {
                    foreach (var item in ObjectReferenceRegistry.data.objects) {
                        if (item.data.Is<EntityConfig>() == true) {
                            var obj = new ObjectItem(item.data);
                            var unsafeConfig = obj.Load<EntityConfig>().AsUnsafeConfig();
                            if (unsafeConfig.IsValid() == false) continue;
                            configs.Data.TryAdd(item.data.sourceId, unsafeConfig);
                        }
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

    }

}