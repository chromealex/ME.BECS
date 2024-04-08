namespace ME.BECS.Views {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    [System.Serializable]
    public struct ViewSource : System.IEquatable<ViewSource> {

        public uint providerId;
        public uint prefabId;

        public bool IsValid => this.providerId > 0u && this.prefabId > 0u;

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

        [INLINE(256)]
        public static bool InstantiateView(this in Ent ent, in ViewSource viewSource) {
            
            return UnsafeViewsModule.InstantiateView(in ent, in viewSource);

        }

        [INLINE(256)]
        public static void DestroyView(this in Ent ent) {
            
            UnsafeViewsModule.DestroyView(in ent);

        }

    }

}