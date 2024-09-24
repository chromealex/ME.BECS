namespace ME.BECS.Addons {

    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadAttribute]
    #endif
    public static class ObjectReferenceRegistry {

        public static ObjectReferenceRegistryData data;
        
        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(Load);
            
        }
        
        public static void Load() {

            if (data != null) return;
            
            {
                // Validate Resources directory
                #if UNITY_EDITOR
                var dir = "Resources";
                if (UnityEditor.AssetDatabase.IsValidFolder("Assets/" + dir) == false) {
                    UnityEditor.AssetDatabase.CreateFolder("Assets", dir);
                }

                var obj = UnityEngine.Resources.Load<ObjectReferenceRegistryData>("ObjectReferenceRegistry");
                if (obj == null) {
                    var path = "Assets/Resources/ObjectReferenceRegistry.asset";
                    var file = ObjectReferenceRegistryData.CreateInstance<ObjectReferenceRegistryData>();
                    UnityEditor.AssetDatabase.CreateAsset(file, path);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
                #endif
            }

            data = UnityEngine.Resources.Load<ObjectReferenceRegistryData>("ObjectReferenceRegistry");

        }

        public static T GetObjectBySourceId<T>(uint sourceId) where T : UnityEngine.Object {

            if (ObjectReferenceRegistry.data == null) return null;
            
            return (T)ObjectReferenceRegistry.data.GetObjectBySourceId(sourceId);

        }

        public static uint Assign(UnityEngine.Object previousValue, UnityEngine.Object newValue) {
            
            if (ObjectReferenceRegistry.data == null) return 0u;
            
            var removed = ObjectReferenceRegistry.data.Remove(previousValue);
            var sourceId = ObjectReferenceRegistry.data.Add(newValue, out bool isNew);

            if (isNew == true || removed == true) {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(ObjectReferenceRegistry.data);
                #endif
            }

            return sourceId;

        }

    }

}