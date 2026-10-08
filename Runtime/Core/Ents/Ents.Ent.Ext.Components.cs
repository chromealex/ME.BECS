#define NO_INLINE

namespace ME.BECS {

    #if !NO_INLINE
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides component, lifecycle and identity operations on entity handles.
    /// </summary>
    public static unsafe partial class EntExt {

        #if !NO_INLINE
        /// <summary>
        /// Tests whether the specified component is enabled on the entity.
        /// </summary>
        [INLINE(256)]
        #endif
        [CodeGeneratorIgnore][IgnoreProfiler]
        public static bool IsEnabled<T>(in this Ent ent) where T : unmanaged, IComponent {
            
            var world = ent.World;
            var typeId = StaticTypes<T>.typeId;
            return Components.ReadState(world.state, typeId, in ent);
            
        }

        #if !NO_INLINE
        /// <summary>
        /// Enables an existing component so queries can match it.
        /// </summary>
        [INLINE(256)]
        #endif
        [CodeGeneratorIgnore][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadWrite)] public static bool Enable<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            E.REQUIRED<T>(in ent);

            var world = ent.World;
            Journal.EnableComponent<T>(in ent);
            return Batches.Enable<T>(in ent, world.state);

        }

        #if !NO_INLINE
        /// <summary>
        /// Disables an existing component without removing its stored data.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadWrite)] public static bool Disable<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            E.REQUIRED<T>(in ent);
            
            var world = ent.World;
            Journal.DisableComponent<T>(in ent);
            return Batches.Disable<T>(in ent, world.state);

        }

        #if !NO_INLINE
        /// <summary>
        /// Writes component data to the entity and records the change through the batching layer.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadWrite)] public static bool Set<T>(in this Ent ent, in T data) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            Journal.SetComponent(in ent, in data);
            return Batches.Set(in ent, in data, world.state);

        }

        #if !NO_INLINE
        /// <summary>
        /// Removes the component from the entity through the batching layer.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadWrite)] public static bool Remove<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            Journal.RemoveComponent<T>(in ent);
            return Batches.Remove<T>(in ent, world.state);

        }

        #if !NO_INLINE
        /// <summary>
        /// Returns writable component data, creating the component when it is absent.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadWrite)] public static ref T Get<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Batches.Get<T>(in ent, world.state);

        }

        #if !NO_INLINE
        /// <summary>
        /// Returns writable data for an existing component; the component must already be present.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static ref T GetOrThrow<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Batches.GetOrThrow<T>(in ent, world.state);

        }

        #if !NO_INLINE
        /// <summary>
        /// Tests component presence, optionally requiring the component to be enabled.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool Has<T>(in this Ent ent, bool checkEnabled = true) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Components.Has<T>(world.state, ent.id, ent.gen, checkEnabled);

        }

        #if !NO_INLINE
        /// <summary>
        /// Returns read-only access to the component, or its default value when the component is absent.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static ref readonly T Read<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Components.Read<T>(world.state, ent.id, ent.gen);

        }

        #if !NO_INLINE
        /// <summary>
        /// Reads component data and reports whether the component is present.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static ref readonly T TryRead<T>(in this Ent ent, out bool exists) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Components.Read<T>(world.state, ent.id, ent.gen, out exists);

        }

        #if !NO_INLINE
        /// <summary>
        /// Reads component data and reports whether the component is present.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool TryRead<T>(in this Ent ent, out T component) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            component = Components.Read<T>(world.state, ent.id, ent.gen, out var exists);
            return exists;

        }

        #if !NO_INLINE
        /// <summary>
        /// Adds the tag when the value is true and removes it when the value is false.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadWrite)] public static void SetTag<T>(in this Ent ent, bool value) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            if (value == true) {
                T comp = default;
                ent.Set(comp);
            } else {
                ent.Remove<T>();
            }

        }

        #if !NO_INLINE
        /// <summary>
        /// Compares the tag's presence with the requested boolean value.
        /// </summary>
        [INLINE(256)]
        #endif
        [IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool HasTag<T>(in this Ent ent, bool value) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            return ent.Has<T>() == value;

        }

    }

}