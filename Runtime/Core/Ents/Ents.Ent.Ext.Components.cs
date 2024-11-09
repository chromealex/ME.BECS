namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static unsafe partial class EntExt {

        [INLINE(256)]
        public static bool Enable<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            E.REQUIRED<T>(in ent);

            var world = ent.World;
            Journal.EnableComponent<T>(in ent);
            return Batches.Enable<T>(in ent, world.state);

        }

        [INLINE(256)]
        public static bool Disable<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            E.REQUIRED<T>(in ent);
            
            var world = ent.World;
            Journal.DisableComponent<T>(in ent);
            return Batches.Disable<T>(in ent, world.state);

        }

        [INLINE(256)]
        public static bool Set<T>(in this Ent ent, in T data) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            Journal.SetComponent(in ent, in data);
            return Batches.Set(in ent, in data, world.state);

        }

        [INLINE(256)]
        public static bool Remove<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            Journal.RemoveComponent<T>(in ent);
            return Batches.Remove<T>(in ent, world.state);

        }

        [INLINE(256)]
        public static ref T Get<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Batches.Get<T>(in ent, world.state);

        }

        [INLINE(256)]
        public static bool Has<T>(in this Ent ent, bool checkEnabled = true) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return Components.Has<T>(world.state, ent.id, ent.gen, checkEnabled);

        }

        [INLINE(256)]
        public static ref readonly T Read<T>(in this Ent ent) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Components.Read<T>(world.state, ent.id, ent.gen);

        }

        [INLINE(256)]
        public static ref readonly T TryRead<T>(in this Ent ent, out bool exists) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            return ref Components.Read<T>(world.state, ent.id, ent.gen, out exists);

        }

        [INLINE(256)]
        public static bool TryRead<T>(in this Ent ent, out T component) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            var world = ent.World;
            component = Components.Read<T>(world.state, ent.id, ent.gen, out var exists);
            return exists;

        }

        [INLINE(256)]
        public static void SetTag<T>(in this Ent ent, bool value) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            if (value == true) {
                T comp = default;
                ent.Set(comp);
            } else {
                ent.Remove<T>();
            }

        }
        
    }

}