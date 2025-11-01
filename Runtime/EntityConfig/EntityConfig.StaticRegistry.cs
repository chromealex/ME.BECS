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

                Logger.Core.Log("[ ME.BECS ] Loading entity configs...");
                configs.Data = new UnsafeHashMap<uint, UnsafeEntityConfig>(ObjectReferenceRegistry.data.objects.Length, Constants.ALLOCATOR_DOMAIN);
                if (isEditor == false) {
                    foreach (var item in ObjectReferenceRegistry.data.objects) {
                        if (item.data.Is<EntityConfig>() == true) {
                            var obj = new ObjectItem(item.data);
                            var config = obj.Load<EntityConfig>();
                            if (config == null) {
                                Logger.Core.Warning($"Config is null while loading #{obj.sourceId}");
                                continue;
                            }
                            var unsafeConfig = config.AsUnsafeConfig();
                            if (unsafeConfig.IsValid() == false) continue;
                            configs.Data.TryAdd(item.data.sourceId, unsafeConfig);
                        }
                    }
                }
                Logger.Core.Log("[ ME.BECS ] Loaded entity configs");

            } catch (System.Exception ex) {

                Logger.Core.Error("Error while initializing configs");
                Logger.Core.Exception(ex);

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