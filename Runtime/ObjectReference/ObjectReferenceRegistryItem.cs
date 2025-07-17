namespace ME.BECS {

    public class ObjectReferenceRegistryItem : UnityEngine.ScriptableObject {

        public ItemInfo data;

        public bool IsValid() {
            return this.data.IsValid();
        }

    }

}