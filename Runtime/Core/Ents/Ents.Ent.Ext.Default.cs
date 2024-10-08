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
            return state->entities.IsAlive(state, ent);

        }

        [INLINE(256)]
        public static bool IsEmpty(in this Ent ent) => ent is { id: 0u, gen: 0, worldId: 0 };

        [INLINE(256)]
        public static void Destroy(in this Ent ent) {

            E.IS_ALIVE(ent);
            var state = ent.World.state;
            state->entities.Lock(state, in ent);
            UnityEngine.Debug.Log("Destroy Request: " + ent + " :: " + state->archetypes.entToArchetypeIdx[state, ent.id]);
            E.IS_ALIVE(ent);
            E.IS_IN_TICK(state);
            
            {
                state->autoDestroyRegistry.Destroy(state, in ent);
            }
            {
                state->entities.Remove(state, in ent);
            }
            {
                state->components.ClearShared(state, ent.id);
            }
            {
                state->batches.Clear(state, in ent);
            }
            {
                state->archetypes.RemoveEntity(state, in ent);
            }
            {
                state->collectionsRegistry.Destroy(state, in ent);
            }
            //UnityEngine.Debug.Log("Destroy Request Complete: " + ent + " :: " + state->archetypes.entToArchetypeIdx[state, ent.id]);
            state->entities.Unlock(state, in ent);
            
        }

    }

}