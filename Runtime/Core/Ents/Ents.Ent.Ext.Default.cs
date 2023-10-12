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
            E.IS_IN_TICK(state);
            
            JobUtils.Lock(ref state->entities.lockIndex);
            {
                state->entities.Remove(state, ent.id);
            }
            JobUtils.Unlock(ref state->entities.lockIndex);
            JobUtils.Lock(ref state->components.lockSharedIndex);
            {
                state->components.ClearShared(state, ent.id);
            }
            JobUtils.Unlock(ref state->components.lockSharedIndex);
            JobUtils.Lock(ref state->batches.lockIndex);
            {
                state->batches.Clear(state, ent.id);
            }
            JobUtils.Unlock(ref state->batches.lockIndex);
            JobUtils.Lock(ref state->archetypes.lockIndex);
            {
                state->archetypes.RemoveEntity(state, ent.id);
            }
            JobUtils.Unlock(ref state->archetypes.lockIndex);
            
        }

    }

}