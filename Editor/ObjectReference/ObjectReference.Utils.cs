namespace ME.BECS.Editor {

    public static class ObjectReferenceRegistryUtils {

        public static uint Assign(UnityEngine.Object previousValue, UnityEngine.Object newValue) {
            
            if (ObjectReferenceRegistry.data == null) return 0u;

            if (previousValue == newValue) {
                var id = ObjectReferenceRegistry.GetId(newValue);
                if (id == 0u) {
                    var sourceId = ObjectReferenceRegistry.data.Add(newValue, out _);
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(ObjectReferenceRegistry.data);
                    #endif
                    return sourceId;
                }
                return id;
            }

            {
                var removed = ObjectReferenceRegistry.data.Remove(previousValue);
                var sourceId = ObjectReferenceRegistry.data.Add(newValue, out bool isNew);

                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(ObjectReferenceRegistry.data);
                #endif

                return sourceId;
            }

        }

    }

}