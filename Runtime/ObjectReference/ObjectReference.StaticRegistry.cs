namespace ME.BECS {

    public static class ObjectReferenceRegistry {

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
            if (UnityEditor.EditorSettings.enterPlayModeOptionsEnabled == true) {
                data = null;
            }
        }
        #endif
        
        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(Load);
            
        }
        
        public static void Load() {

            if (ObjectReferenceRegistry.data != null) return;
            LoadForced();
            
        }

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
                    UnityEngine.Debug.LogError("ObjectReferenceRegistry can not be loaded");
                }
                #endif
            }

            ObjectReferenceRegistry.data = UnityEngine.Resources.Load<ObjectReferenceRegistryData>("ObjectReferenceRegistry");

        }

        public static void CleanUpLoadedAssets() {
            
            ObjectReferenceRegistry.data.CleanUpLoadedAssets();
            
        }

        public static uint AddRuntimeObject(UnityEngine.Object obj) {

            var nextId = ObjectReferenceRegistry.data.sourceId;
            ItemInfo item;
            for (var index = 0; index < additionalRuntimeObjects.Count; ++index) {
                var elem = additionalRuntimeObjects[index];
                if (elem.Is(obj) == true) {
                    item = elem;
                    ++item.referencesCount;
                    additionalRuntimeObjects[index] = item;
                    return item.sourceId;
                }
            }

            item = new ItemInfo() {
                referencesCount = 1u,
                source = obj,
                sourceId = nextId + (++nextRuntimeId),
            };
            additionalRuntimeObjects.Add(item);

            return item.sourceId;

        }

        public static void ClearRuntimeObjects() {
            additionalRuntimeObjects.Clear();
        }

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

        public static uint GetId(UnityEngine.Object obj) {

            foreach (var item in ObjectReferenceRegistry.data.items) {
                if (item.Is(obj) == true) return item.sourceId;
            }

            foreach (var item in ObjectReferenceRegistry.additionalRuntimeObjects) {
                if (item.Is(obj) == true) return item.sourceId;
            }

            return 0u;
            
        }

    }

}