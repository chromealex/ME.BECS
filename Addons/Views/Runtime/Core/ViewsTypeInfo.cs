namespace ME.BECS.Views {

    [System.Flags]
    public enum TypeFlags : byte {

        Initialize = 1 << 0,
        DeInitialize = 1 << 1,
        EnableFromPool = 1 << 2,
        DisableToPool = 1 << 3,
        ApplyState = 1 << 4,
        Update = 1 << 5,

    }
    
    [System.Serializable]
    public struct ViewTypeInfo {

        public TypeFlags flags;
        public CullingType cullingType;

        public bool HasInitialize => (this.flags & TypeFlags.Initialize) != 0;
        public bool HasDeInitialize => (this.flags & TypeFlags.DeInitialize) != 0;
        public bool HasEnableFromPool => (this.flags & TypeFlags.EnableFromPool) != 0;
        public bool HasDisableToPool => (this.flags & TypeFlags.DisableToPool) != 0;
        public bool HasApplyState => (this.flags & TypeFlags.ApplyState) != 0;
        public bool HasUpdate => (this.flags & TypeFlags.Update) != 0;

    }
    
    public static class ViewsTypeInfo {

        public static readonly System.Collections.Generic.Dictionary<System.Type, ViewTypeInfo> types = new System.Collections.Generic.Dictionary<System.Type, ViewTypeInfo>();

        public static void RegisterType<T>(ViewTypeInfo viewTypeInfo) {

            if (types.ContainsKey(typeof(T)) == false) {
                types.Add(typeof(T), viewTypeInfo);
            }

        }

    }

}