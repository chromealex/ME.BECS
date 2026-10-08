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
    public static unsafe partial class EntExt {

        /// <summary>
        /// Sets shared.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool SetShared<T>(in this Ent ent, in T data) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.SetShared(in ent, in data, world.state);

        }

        /// <summary>
        /// Removes shared.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool RemoveShared<T>(in this Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.RemoveShared<T>(in ent, world.state, hash);

        }

        /// <summary>
        /// Tests whether the context has shared.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool HasShared<T>(in this Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.HasShared<T>(in ent, world.state, hash);

        }

        /// <summary>
        /// Returns shared.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref T GetShared<T>(in this Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Batches.GetShared<T>(in ent, world.state, hash);

        }

        /// <summary>
        /// Reads shared.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref readonly T ReadShared<T>(in this Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Batches.ReadShared<T>(in ent, world.state, hash);

        }

    }

}