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

        /// <summary>
        /// Instantiate view by ViewSource id
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="viewSource"></param>
        /// <returns></returns>
        [INLINE(256)]
        public static bool InstantiateView(this in Ent ent, in ViewSource viewSource) {
            
            return UnsafeViewsModule.InstantiateView(in ent, in viewSource);

        }

        /// <summary>
        /// Uses sourceEnt's view and apply it to ent
        /// This method removes view from sourceEnt completely
        /// Use this method to change view owner
        /// </summary>
        /// <param name="ent">Entity to receive view</param>
        /// <param name="sourceEnt">Entity with source view</param>
        /// <returns>True if operation is success</returns>
        [INLINE(256)]
        public static bool AssignView(this in Ent ent, in Ent sourceEnt) {
            
            return UnsafeViewsModule.AssignView(in ent, in sourceEnt);

        }

        /// <summary>
        /// Remove view from entity
        /// </summary>
        /// <param name="ent"></param>
        [INLINE(256)]
        public static void DestroyView(this in Ent ent) {
            
            UnsafeViewsModule.DestroyView(in ent);

        }

    }

}