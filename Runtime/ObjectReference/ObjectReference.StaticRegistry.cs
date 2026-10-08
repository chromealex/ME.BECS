using CollectionExtensions = System.Collections.Generic.CollectionExtensions;

namespace ME.BECS {

    /// <summary>
    /// Resolves registered Unity objects and tracks runtime object registrations.
    /// </summary>
    public static class ObjectReferenceRegistry {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public static ObjectReferenceRegistryData data;

        internal static readonly System.Collections.Generic.List<ItemInfo> additionalRuntimeObjects = new System.Collections.Generic.List<ItemInfo>();
        private static uint nextRuntimeId;
        
        #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethodAttribute]
        private static void RegisterForPlaymodeChange() {
            UnityEditor.EditorApplication.playModeStateChanged -= EditorApplicationOnplayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += EditorApplicationOnplayModeStateChanged;
        }

        private static void EditorApplicationOnplayModeStateChanged(UnityEditor.PlayModeStateChange state) {
             if (state != UnityEditor.PlayModeStateChange.EnteredEditMode) {
                return;           
             }
             if (UnityEditor.EditorSettings.enterPlayModeOptionsEnabled == true) {
                 data = null;
             }
        }
        #endif
        
        /// <summary>
        /// Initializes object reference registry state from the supplied context.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(Load);
            
        }
        
        /// <summary>
        /// Loads the registered data required by this operation.
        /// </summary>
        public static void Load() {

            if (ObjectReferenceRegistry.data != null) return;
            LoadForced();
            
        }

        /// <summary>
        /// Loads forced.
        /// </summary>
        #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        #endif
        public static void LoadForced() {
            
            {
                // Validate Resources directory
                #if UNITY_EDITOR
                const string dir = "Resources";
                const string path = "Assets/Resources/ObjectReferenceRegistry.asset";
                if (UnityEditor.AssetDatabase.IsValidFolder($"Assets/{dir}") == false) {
                    UnityEditor.AssetDatabase.CreateFolder("Assets", dir);
                }

                var obj = UnityEngine.Resources.Load<ObjectReferenceRegistryData>("ObjectReferenceRegistry");
                if (obj == null && System.IO.File.Exists(path) == false) {
                    var file = UnityEngine.ScriptableObject.CreateInstance<ObjectReferenceRegistryData>();
                    UnityEditor.AssetDatabase.CreateAsset(file, path);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                } else if (obj == null) {
                    Logger.Core.Error("ObjectReferenceRegistry can not be loaded");
                }
                #endif
            }

            ObjectReferenceRegistry.data = UnityEngine.Resources.Load<ObjectReferenceRegistryData>("ObjectReferenceRegistry");
            ObjectReferenceRegistry.data?.Initialize();

        }

        /// <summary>
        /// Releases assets retained by the loading operation.
        /// </summary>
        public static void CleanUpLoadedAssets() {
            
            ObjectReferenceRegistry.data.CleanUpLoadedAssets();
            
        }

        /// <summary>
        /// Adds runtime object.
        /// </summary>
        public static uint AddRuntimeObject(UnityEngine.Object obj) {

            var nextId = ObjectReferenceRegistry.data.sourceId;
            ItemInfo item;
            for (var index = 0; index < additionalRuntimeObjects.Count; ++index) {
                var elem = additionalRuntimeObjects[index];
                if (elem.Is(obj) == true) {
                    item = elem;
                    //++item.referencesCount;
                    additionalRuntimeObjects[index] = item;
                    return item.sourceId;
                }
            }

            item = new ItemInfo() {
                //referencesCount = 1u,
                source = obj,
                sourceId = nextId + (++nextRuntimeId),
            };
            additionalRuntimeObjects.Add(item);
            ObjectReferenceRegistry.data.objectLookup.Add(obj, item.sourceId);

            return item.sourceId;

        }

        /// <summary>
        /// Clears runtime objects.
        /// </summary>
        public static void ClearRuntimeObjects() {
            additionalRuntimeObjects.Clear();
        }

        /// <summary>
        /// Loads async.
        /// </summary>
        public static UnityEngine.Awaitable<T> LoadAsync<T>(uint sourceId) where T : UnityEngine.Object {
            
            if (ObjectReferenceRegistry.data == null) return null;
            
            return ObjectReferenceRegistry.data.GetObjectBySourceId(sourceId).LoadAsync<T>();

        }

        /// <summary>
        /// Returns object by source ID.
        /// </summary>
        public static T GetObjectBySourceId<T>(uint sourceId) where T : UnityEngine.Object {

            if (ObjectReferenceRegistry.data == null) return null;
            
            var obj = ObjectReferenceRegistry.data.GetObjectBySourceId(sourceId).Load<T>();
            if (obj == null) {
                foreach (var item in ObjectReferenceRegistry.additionalRuntimeObjects) {
                    if (item.sourceId == sourceId) return item.source as T;
                }
            }

            return obj;

        }

        /// <summary>
        /// Returns object by source ID.
        /// </summary>
        public static ObjectItem GetObjectBySourceId(uint sourceId) {

            if (ObjectReferenceRegistry.data == null) return default;
            
            var obj = ObjectReferenceRegistry.data.GetObjectBySourceId(sourceId);
            if (obj.IsValid() == false) {
                foreach (var item in ObjectReferenceRegistry.additionalRuntimeObjects) {
                    if (item.sourceId == sourceId) return new ObjectItem(item);
                }
            }

            return obj;

        }

        /// <summary>
        /// Returns ID.
        /// </summary>
        public static uint GetId(UnityEngine.Object obj) {

            if (obj == null) return 0u;

            if (UnityEngine.Application.isPlaying == false) {
                
                foreach (var item in ObjectReferenceRegistry.data.objects) {
                    if (item.data.Is(obj) == true) return item.data.sourceId;
                }

                foreach (var item in ObjectReferenceRegistry.additionalRuntimeObjects) {
                    if (item.Is(obj) == true) return item.sourceId;
                }
                
            }
            
            return CollectionExtensions.GetValueOrDefault(ObjectReferenceRegistry.data.objectLookup, obj, 0u);

        }

    }

}