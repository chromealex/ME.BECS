namespace ME.BECS.Views {

    [System.Serializable]
    public struct View {

        public ViewSource viewSource;

        public static implicit operator ViewSource(View view) {
            return view.viewSource;
        }
        
    }

    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadAttribute]
    #endif
    public static class ViewsRegistry {

        public static ViewsRegistryData data;
        
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

                var obj = UnityEngine.Resources.Load<ViewsRegistryData>("ViewsRegistry");
                if (obj == null) {
                    var path = "Assets/Resources/ViewsRegistry.asset";
                    var file = ViewsRegistryData.CreateInstance<ViewsRegistryData>();
                    UnityEditor.AssetDatabase.CreateAsset(file, path);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
                #endif
            }

            data = UnityEngine.Resources.Load<ViewsRegistryData>("ViewsRegistry");

        }

        public static EntityView GetEntityViewByPrefabId(uint prefabId) {

            if (ViewsRegistry.data == null) return null;
            
            return ViewsRegistry.data.GetEntityViewByPrefabId(prefabId);

        }

        public static uint Assign(EntityView previousValue, EntityView newValue) {
            
            if (ViewsRegistry.data == null) return 0u;
            
            var removed = ViewsRegistry.data.Remove(previousValue);
            var prefabId = ViewsRegistry.data.Add(newValue, out bool isNew);

            if (isNew == true || removed == true) {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(ViewsRegistry.data);
                #endif
            }

            return prefabId;

        }

    }

}