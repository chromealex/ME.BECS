namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static unsafe partial class EntExt {

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
            //UnityEngine.Debug.Log("Destroy Request: " + ent + " :: " + state->archetypes.entToArchetypeIdx[state, ent.id]);
            E.IS_ALIVE(ent);
            E.IS_IN_TICK(state);
            
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