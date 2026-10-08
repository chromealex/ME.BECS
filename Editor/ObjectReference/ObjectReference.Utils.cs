namespace ME.BECS.Editor {

    /// <summary>
    /// Provides helper operations for object reference registry.
    /// </summary>
    public static class ObjectReferenceRegistryUtils {

        /// <summary>
        /// Assigns the supplied state to its destination.
        /// </summary>
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