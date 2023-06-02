namespace ME.BECS.Views {

    using BECS;
    
    [System.Serializable]
    public struct ViewSource : System.IEquatable<ViewSource> {

        public uint providerId;
        public uint prefabId;

        public bool Equals(ViewSource other) {
            return this.providerId == other.providerId && this.prefabId == other.prefabId;
        }

        public override bool Equals(object obj) {
            return obj is ViewSource other && this.Equals(other);
        }

        public override int GetHashCode() {
            return (int)this.providerId ^ (int)this.prefabId;
        }

        public override string ToString() {
            return $"[ ViewSource ] PrefabId: {this.prefabId}, Provider: {this.providerId}";
        }

    }

    public static class EntExt {

        public static void InstantiateView(this in Ent ent, in ViewSource viewSource) {
            
            UnsafeViewsModule.InstantiateView(in ent, in viewSource);

        }

        public static void DestroyView(this in Ent ent) {
            
            UnsafeViewsModule.DestroyView(in ent);

        }

    }

}