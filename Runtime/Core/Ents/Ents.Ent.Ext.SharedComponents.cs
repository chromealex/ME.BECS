namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static unsafe partial class EntExt {

        [INLINE(256)]
        public static bool SetShared<T>(in this Ent ent, in T data) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return world.state->batches.SetShared(in ent, in data, world.state);

        }

        [INLINE(256)]
        public static bool RemoveShared<T>(in this Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return world.state->batches.RemoveShared<T>(in ent, world.state, hash);

        }

        [INLINE(256)]
        public static bool HasShared<T>(in this Ent ent) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return world.state->batches.HasShared<T>(in ent, world.state);

        }

        [INLINE(256)]
        public static ref T GetShared<T>(in this Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref world.state->batches.GetShared<T>(in ent, world.state, hash);

        }

        [INLINE(256)]
        public static ref readonly T ReadShared<T>(in this Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref world.state->batches.ReadShared<T>(in ent, world.state, hash);

        }

    }

}