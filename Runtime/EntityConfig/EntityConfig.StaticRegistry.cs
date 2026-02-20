namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;

    public class EntityConfigsRegistryLoaded {

        internal static readonly SharedStatic<bbool> isLoaded = SharedStatic<bbool>.GetOrCreate<EntityConfigsRegistryLoaded>();

    }

    public class EntityConfigsRegistry {

        private static readonly SharedStatic<UnsafeHashMap<uint, UnsafeEntityConfig>> configs = SharedStatic<UnsafeHashMap<uint, UnsafeEntityConfig>>.GetOrCreate<EntityConfigsRegistry>();
        
        private struct ProcessItem {

            public UnityEngine.Awaitable<EntityConfig> config;
            public ObjectItem objectItem;

            public UnityEngine.Awaitable<EntityConfig>.Awaiter GetAwaiter() {
                return this.config.GetAwaiter();
            }

        }

        public static void Initialize(bool isEditor = false) {

            if (StaticTypes.tracker.IsCreated == false) return;
            
            ObjectReferenceRegistry.Load();
            Load(isEditor);
            
        }

        private static void Load(bool isEditor) {

            if (ObjectReferenceRegistry.data == null) return;
            LoadForced(isEditor);

        }

        public static async UnityEngine.Awaitable WaitLoaded() {
            while (IsLoaded() == false) {
                await UnityEngine.Awaitable.NextFrameAsync();
            }
        }

        public static bool IsLoaded() => EntityConfigsRegistryLoaded.isLoaded.Data == true || UnityEngine.Application.isPlaying == false;
        
        private static void LoadForced(bool isEditor) {

            var requestedCount = 0;
            var loadedCount = 0;
            try {

                Logger.Core.Log("[ ME.BECS ] Loading entity configs...");
                var requested = UnityEngine.Pool.ListPool<ProcessItem>.Get();
                if (configs.Data.IsCreated == true) configs.Data.Dispose();
                configs.Data = new UnsafeHashMap<uint, UnsafeEntityConfig>(ObjectReferenceRegistry.data.objects.Length, Constants.ALLOCATOR_PERSISTENT);
                if (isEditor == false) {
                    foreach (var item in ObjectReferenceRegistry.data.objects) {
                        if (item.data.Is<EntityConfig>() == true) {
                            var obj = new ObjectItem(item.data);
                            var config = obj.LoadAsync<EntityConfig>();
                            ++requestedCount;
                            requested.Add(new ProcessItem() {
                                config = config,
                                objectItem = obj,
                            });
                        }
                    }
                }

                foreach (var item in requested) {
                    var awaiter = item.GetAwaiter();
                    if (awaiter.IsCompleted == true) {
                        CompleteItem(item);
                    } else {
                        item.GetAwaiter().OnCompleted(() => {
                            CompleteItem(item);
                        });
                    }
                }
                UnityEngine.Pool.ListPool<ProcessItem>.Release(requested);

            } catch (System.Exception ex) {

                Logger.Core.Error("Error while initializing configs");
                Logger.Core.Exception(ex);

            }

            void CompleteItem(ProcessItem processItem) {
                ++loadedCount;
                ProcessEntityConfig(processItem);
                if (loadedCount == requestedCount) {
                    EntityConfigsRegistryLoaded.isLoaded.Data = true;
                    Logger.Core.Log("[ ME.BECS ] Loaded entity configs");
                }
            }

            void ProcessEntityConfig(ProcessItem processItem) {
                var obj = processItem.objectItem;
                var config = processItem.config.GetAwaiter().GetResult();
                if (config == null) {
                    Logger.Core.Warning($"Config is null while loading #{obj.sourceId}");
                    return;
                }
                var unsafeConfig = config.AsUnsafeConfig();
                if (unsafeConfig.IsValid() == false) return;
                TryAdd(obj.sourceId, unsafeConfig);
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

            if (EntityConfigsRegistry.IsLoaded() == false) {
                throw new System.Exception("[ EntityConfigRegistry ] IsLoaded returns false, you need to wait before loading entity configs.");
            }
            
            configs.Data.TryGetValue(sourceId, out var config);
            return config;

        }

        [INLINE(256)]
        public static EntityConfig GetEntityConfigBySourceId(uint sourceId) {

            if (EntityConfigsRegistry.IsLoaded() == false) {
                throw new System.Exception("[ EntityConfigRegistry ] IsLoaded returns false, you need to wait before loading entity configs.");
            }

            return ObjectReferenceRegistry.GetObjectBySourceId<EntityConfig>(sourceId);
            
        }

    }

}