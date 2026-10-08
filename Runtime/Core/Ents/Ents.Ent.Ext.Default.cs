namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Groups core components for change tracking and queries.
    /// </summary>
    public struct CoreComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.white;

    }

    /// <summary>
    /// Default component.
    /// See ent.SetActive() API for more details.
    /// </summary>
    [ComponentGroup(typeof(CoreComponentGroup))]
    public struct IsInactive : IComponent { }
    
    /// <summary>
    /// Provides component, lifecycle and identity operations on entity handles.
    /// </summary>
    [IgnoreProfiler]
    public static unsafe partial class EntExt {

        /// <summary>
        /// Include (true) or exclude (false) entity from any filters.
        /// By default, all entities are included.
        /// </summary>
        /// <param name="ent">Entity</param>
        /// <param name="state">State</param>
        [IgnoreProfiler]
        public static void SetActive(in this Ent ent, bool state) {
            
            ent.SetTag<IsInactive>(state == false);
            
        }

        /// <summary>
        /// Tests whether the context is active.
        /// </summary>
        [IgnoreProfiler]
        public static bool IsActive(in this Ent ent) {
            return ent.Has<IsInactive>() == false;
        }

        /// <summary>
        /// Tests whether the referenced entity or world still matches its registered lifetime.
        /// </summary>
        [IgnoreProfiler]
        public static bool IsAlive(in this Ent ent) {

            if (ent.World.isCreated == false) return false;
            var state = ent.World.state;
            return Ents.IsAlive(state, ent);

        }

        /// <summary>
        /// Tests whether the entity slot, generation and world identifier are all zero.
        /// </summary>
        [IgnoreProfiler]
        public static bool IsEmpty(in this Ent ent) => ent is { id: 0u, gen: 0, worldId: 0 };

        /// <summary>
        /// Destroys the referenced instance and applies its registered destruction handling.
        /// </summary>
        [CodeGeneratorIgnore][IgnoreProfiler]
        public static void Destroy(in this Ent ent) {

            E.IS_ALIVE(ent);
            var state = ent.World.state;
            Ents.Lock(state, in ent);
            E.IS_ALIVE(ent);
            E.IS_IN_TICK(state);
            {
                AutoDestroyRegistry.Destroy(state, in ent);
            }
            {
                Ents.Remove(state, in ent);
            }
            {
                Components.ClearShared(state, ent.id);
            }
            {
                Components.CleanUpEntity(state, in ent);
            }
            {
                CollectionsRegistry.Destroy(state, in ent);
            }
            Ents.Unlock(state, in ent);
            
        }

    }

}