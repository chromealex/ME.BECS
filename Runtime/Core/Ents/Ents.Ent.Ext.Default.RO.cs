namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    public static partial class EntExt {

        [INLINE(256)][IgnoreProfiler]
        public static bool IsAlive(in this EntRO ent) => ent.ent.IsAlive();

        [INLINE(256)][IgnoreProfiler]
        public static bool IsActive(in this EntRO ent) => ent.ent.IsActive();

        [INLINE(256)][IgnoreProfiler]
        public static bool IsEmpty(in this EntRO ent) => ent.ent.IsEmpty();

    }

}