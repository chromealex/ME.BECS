namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    public static partial class EntExt {

        [INLINE(256)][IgnoreProfiler]
        public static bool HasShared<T>(in this EntRO ent) where T : unmanaged, IComponentShared => ent.HasShared<T>();

        [INLINE(256)][IgnoreProfiler]
        public static ref readonly T ReadShared<T>(in this EntRO ent, uint hash = 0u) where T : unmanaged, IComponentShared => ref ent.ReadShared<T>(hash);

    }

}