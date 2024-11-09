namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct Components {

        [INLINE(256)]
        public static bool Enable<T>(State* state, in Ent ent) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return Components.SetState(state, typeId, groupId, in ent, true);
            
        }

        [INLINE(256)]
        public static bool Disable<T>(State* state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return Components.SetState(state, typeId, groupId, in ent, false);
            
        }

        [INLINE(256)]
        public static bool Set<T>(State* state, in Ent ent, in T data) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return Components.SetUnknownType(state, typeId, groupId, in ent, in data);
            
        }

        [INLINE(256)]
        public static bool Remove<T>(State* state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return Components.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        [INLINE(256)]
        public static bool Remove(State* state, in Ent ent, uint typeId, uint groupId) {

            return Components.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        [INLINE(256)]
        public static ref readonly T Read<T>(State* state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)]
        public static ref readonly T Read<T>(State* state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)]
        public static T* ReadPtr<T>(State* state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)]
        public static T* ReadPtr<T>(State* state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)]
        public static bool Has<T>(State* state, uint entId, ushort gen, bool checkEnabled) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            return Components.HasUnknownType(state, typeId, entId, gen, checkEnabled);

        }

        [INLINE(256)]
        public static ref T Get<T>(State* state, in Ent ent) where T : unmanaged, IComponent => ref Get<T>(state, ent);

        [INLINE(256)]
        public static ref T Get<T>(State* state, Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var data = Components.GetUnknownType(state, typeId, groupId, in ent, out _);
            return ref *(T*)data;

        }

        [INLINE(256)]
        public static T* Get<T>(State* state, in Ent ent, out bool isNew) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var data = Components.GetUnknownType(state, typeId, groupId, in ent, out isNew);
            return (T*)data;

        }

        public static bool HasDirect<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.Has<T>(ent.World.state, ent.id, ent.gen, checkEnabled: false);

        }

        public static T ReadDirect<T>(Ent ent) where T : unmanaged, IComponent {

            if (StaticTypes<T>.isTag == true) return StaticTypes<T>.defaultValue;

            return Components.Read<T>(ent.World.state, ent.id, ent.gen);

        }

        public static void SetDirect<T>(Ent ent, T data) where T : unmanaged, IComponent {

            if (StaticTypes<T>.isTag == true) return;

            Components.Set(ent.World.state, in ent, in data);

        }

    }

}