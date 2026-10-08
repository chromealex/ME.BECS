namespace ME.BECS.Views {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;
    
    /// <summary>
    /// Presents view source state through the associated view.
    /// </summary>
    [System.Serializable]
    [LAYOUT(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 8)]
    public struct ViewSource : System.IEquatable<ViewSource> {

        /// <summary>
        /// Provider id used to locate the associated entry.
        /// </summary>
        public uint providerId;
        /// <summary>
        /// Registered prefab identifier used to resolve a view source.
        /// </summary>
        public uint prefabId;

        /// <summary>
        /// Indicates is valid.
        /// </summary>
        public bool IsValid => this.providerId > 0u && this.prefabId > 0u;

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public bool Equals(ViewSource other) {
            return this.providerId == other.providerId && this.prefabId == other.prefabId;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public override bool Equals(object obj) {
            return obj is ViewSource other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            return (int)this.providerId ^ (int)this.prefabId;
        }

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() {
            return $"[ ViewSource ] PrefabId: {this.prefabId}, Provider: {this.providerId}";
        }

    }

    /// <summary>
    /// Provides component, lifecycle and identity operations on entity handles.
    /// </summary>
    public static class EntExt {

        /// <summary>
        /// Instantiate view by ViewSource id
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="viewSource"></param>
        /// <returns></returns>
        [INLINE(256)][IgnoreProfiler]
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
        [INLINE(256)][IgnoreProfiler]
        public static bool AssignView(this in Ent ent, in Ent sourceEnt) {
            
            return UnsafeViewsModule.AssignView(in ent, in sourceEnt);

        }

        /// <summary>
        /// Remove view from entity
        /// </summary>
        /// <param name="ent"></param>
        [INLINE(256)][IgnoreProfiler]
        public static void DestroyView(this in Ent ent) {
            
            UnsafeViewsModule.DestroyView(in ent);

        }

    }

}