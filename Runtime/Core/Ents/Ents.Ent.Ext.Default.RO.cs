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
        /// Tests whether the referenced entity or world still matches its registered lifetime.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool IsAlive(in this EntRO ent) => ent.ent.IsAlive();

        /// <summary>
        /// Tests whether the context is active.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool IsActive(in this EntRO ent) => ent.ent.IsActive();

        /// <summary>
        /// Tests whether the entity slot, generation and world identifier are all zero.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool IsEmpty(in this EntRO ent) => ent.ent.IsEmpty();

    }

}