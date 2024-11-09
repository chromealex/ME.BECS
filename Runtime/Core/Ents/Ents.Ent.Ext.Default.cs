namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    /// <summary>
    /// Default component.
    /// See ent.SetActive() API for more details.
    /// </summary>
    public struct IsInactive : IComponent { }
    
    public static unsafe partial class EntExt {

        /// <summary>
        /// Include (true) or exclude (false) entity from any filters.
        /// By default all entities are included.
        /// </summary>
        /// <param name="ent">Entity</param>
        /// <param name="state">State</param>
        [INLINE(256)]
        public static void SetActive(in this Ent ent, bool state) {
            
            ent.SetTag<IsInactive>(state == false);
            
        }

        [INLINE(256)]
        public static bool IsActive(in this Ent ent) {
            return ent.Has<IsInactive>() == false;
        }

        [INLINE(256)]
        public static bool IsAlive(in this Ent ent) {

            if (ent.World.isCreated == false) return false;
            var state = ent.World.state;
            return Ents.IsAlive(state, ent);

        }

        [INLINE(256)]
        public static bool IsEmpty(in this Ent ent) => ent is { id: 0u, gen: 0, worldId: 0 };

        [INLINE(256)]
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
                Batches.Clear(state, in ent);
            }
            {
                Archetypes.RemoveEntity(state, in ent);
            }
            {
                CollectionsRegistry.Destroy(state, in ent);
            }
            Ents.Unlock(state, in ent);
            
        }

    }

}