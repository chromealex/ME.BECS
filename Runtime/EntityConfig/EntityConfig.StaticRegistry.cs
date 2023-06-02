namespace ME.BECS {

    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadAttribute]
    #endif
    public static class EntityConfigsRegistry {

        public static EntityConfigsRegistryData data;
        
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void Initialize() {

            if (data != null) return;
            
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

        }

        public static EntityConfig GetEntityConfigBySourceId(uint sourceId) {

            if (EntityConfigsRegistry.data == null) return null;
            
            return EntityConfigsRegistry.data.GetEntityConfigBySourceId(sourceId);

        }

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