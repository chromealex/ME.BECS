namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static unsafe partial class EntExt {

        [INLINE(256)]
        public static bool Set(in this Ent ent, uint typeId, void* data) {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.Set(in ent, typeId, data, world.state);

        }

        [INLINE(256)]
        public static bool Remove(in this Ent ent, uint typeId) {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.Remove(in ent, typeId, world.state);

        }

        [INLINE(256)]
        public static bool SetPtr(in this Ent ent, uint typeId, void* data) {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.Set(in ent, typeId, data, world.state);

        }

        [INLINE(256)]
        public static bool SetPtr<T>(in this Ent ent, T* data) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.Set(in ent, StaticTypes<T>.typeId, data, world.state);

        }

        [INLINE(256)]
        public static T* GetPtr<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Batches.GetPtr<T>(in ent, world.state);

        }

        [INLINE(256)]
        public static void* ReadPtr(in this Ent ent, uint typeId) {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Components.ReadUnknownType(world.state, typeId, ent.id, ent.gen, out _);

        }

        [INLINE(256)]
        public static T* ReadPtr<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Components.ReadPtr<T>(world.state, ent.id, ent.gen);

        }

        [INLINE(256)]
        public static T* TryReadPtr<T>(in this Ent ent, out bool exists) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Components.ReadPtr<T>(world.state, ent.id, ent.gen, out exists);

        }

        [INLINE(256)]
        public static bool TryReadPtr<T>(in this Ent ent, out T* component) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            component = Components.ReadPtr<T>(world.state, ent.id, ent.gen, out var exists);
            return exists;

        }

    }

}