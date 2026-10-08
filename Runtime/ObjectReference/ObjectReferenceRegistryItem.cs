namespace ME.BECS {

    /// <summary>
    /// Stores object reference registry item for the associated runtime API.
    /// </summary>
    public class ObjectReferenceRegistryItem : UnityEngine.ScriptableObject {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public ItemInfo data;

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        public bool IsValid() {
            return this.data.IsValid();
        }

    }

}