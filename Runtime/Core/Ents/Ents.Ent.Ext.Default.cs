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
            E.IS_ALIVE(ent);
            E.IS_IN_TICK(state);
            
            {
                state->entities.Remove(state, in ent);
            }
            JobUtils.Lock(ref state->components.lockSharedIndex);
            {
                state->components.ClearShared(state, ent.id);
            }
            JobUtils.Unlock(ref state->components.lockSharedIndex);
            {
                state->batches.Clear(state, ent.id);
            }
            {
                state->archetypes.RemoveEntity(state, ent.id);
            }
            state->collectionsRegistry.Destroy(state, in ent);
            state->entities.Unlock(state, in ent);
            
        }

    }

}