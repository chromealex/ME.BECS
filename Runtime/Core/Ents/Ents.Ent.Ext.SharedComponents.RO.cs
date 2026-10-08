namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides component, lifecycle and identity operations on entity handles.
    /// </summary>
    public static partial class EntExt {

        /// <summary>
        /// Tests whether the context has shared.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool HasShared<T>(in this EntRO ent) where T : unmanaged, IComponentShared => ent.HasShared<T>();

        /// <summary>
        /// Reads shared.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref readonly T ReadShared<T>(in this EntRO ent, uint hash = 0u) where T : unmanaged, IComponentShared => ref ent.ReadShared<T>(hash);

    }

}