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
        /// Writes component data to the entity and records the change through the batching layer.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool Set(in this Ent ent, uint typeId, void* data) {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.Set(in ent, typeId, data, world.state);

        }

        /// <summary>
        /// Removes the component from the entity through the batching layer.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool Remove(in this Ent ent, uint typeId) {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.Remove(in ent, typeId, world.state);

        }

        /// <summary>
        /// Sets ptr.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool SetPtr(in this Ent ent, uint typeId, void* data) {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.Set(in ent, typeId, data, world.state);

        }

        /// <summary>
        /// Sets ptr.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool SetPtr<T>(in this Ent ent, T* data) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.Set(in ent, StaticTypes<T>.typeId, data, world.state);

        }

        /// <summary>
        /// Resolves the requested address to a native pointer.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T* GetPtr<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.GetPtr<T>(in ent, world.state);

        }

        /// <summary>
        /// Reads ptr.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void* ReadPtr(in this Ent ent, uint typeId) {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Components.ReadUnknownType(world.state, typeId, ent.id, ent.gen, out _);

        }

        /// <summary>
        /// Reads ptr.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T* ReadPtr<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Components.ReadPtr<T>(world.state, ent.id, ent.gen);

        }

        /// <summary>
        /// Attempts to read ptr and reports whether the operation succeeded.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T* TryReadPtr<T>(in this Ent ent, out bool exists) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Components.ReadPtr<T>(world.state, ent.id, ent.gen, out exists);

        }

        /// <summary>
        /// Attempts to read ptr and reports whether the operation succeeded.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool TryReadPtr<T>(in this Ent ent, out T* component) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            component = Components.ReadPtr<T>(world.state, ent.id, ent.gen, out var exists);
            return exists;

        }

    }

}