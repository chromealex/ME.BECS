namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct Components {

        [INLINE(256)]
        public static bool IsEnabled<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            return Components.ReadState(state, typeId, in ent);
            
        }

        [INLINE(256)]
        public static bool Enable<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return Components.SetState(state, typeId, groupId, in ent, true);
            
        }

        [INLINE(256)]
        public static bool Disable<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return Components.SetState(state, typeId, groupId, in ent, false);
            
        }

        [INLINE(256)]
        public static bool Set<T>(safe_ptr<State> state, in Ent ent, in T data) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return Components.SetUnknownType(state, typeId, groupId, in ent, in data);
            
        }

        [INLINE(256)]
        public static bool Remove<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return Components.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        [INLINE(256)]
        public static bool Remove(safe_ptr<State> state, in Ent ent, uint typeId, uint groupId) {

            return Components.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        [INLINE(256)]
        public static ref readonly T Read<T>(safe_ptr<State> state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)]
        public static ref readonly T Read<T>(safe_ptr<State> state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)]
        public static T* ReadPtr<T>(safe_ptr<State> state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)]
        public static T* ReadPtr<T>(safe_ptr<State> state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)]
        public static bool Has<T>(safe_ptr<State> state, uint entId, ushort gen, bool checkEnabled) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            return Components.HasUnknownType(state, typeId, entId, gen, checkEnabled);

        }

        [INLINE(256)]
        public static ref T Get<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent => ref Get<T>(state, ent);

        [INLINE(256)]
        public static ref T Get<T>(safe_ptr<State> state, Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var data = Components.GetUnknownType(state, typeId, groupId, in ent, out _, StaticTypes<T>.defaultValuePtr);
            return ref *(T*)data;

        }

        [INLINE(256)]
        public static T* Get<T>(safe_ptr<State> state, in Ent ent, out bool isNew) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var data = Components.GetUnknownType(state, typeId, groupId, in ent, out isNew, StaticTypes<T>.defaultValuePtr);
            return (T*)data;

        }

        public static bool HasStaticDirect<T>(Ent ent) where T : unmanaged, IConfigComponentStatic {

            return ent.HasStatic<T>();

        }

        public static T ReadStaticDirect<T>(Ent ent) where T : unmanaged, IConfigComponentStatic {

            if (StaticTypes<T>.isTag == true) return StaticTypes<T>.defaultValue;

            return ent.ReadStatic<T>();

        }

        public static bool IsTagDirect<T>() where T : unmanaged, IComponentBase {

            return StaticTypesIsTag<T>.value.Data;

        }
        
        public static bool IsEnabledDirect<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.ReadState(ent.World.state, StaticTypes<T>.typeId, in ent);

        }

        public static bool HasDirect<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.Has<T>(ent.World.state, ent.id, ent.gen, checkEnabled: false);

        }

        public static bool HasDirectEnabled<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.Has<T>(ent.World.state, ent.id, ent.gen, checkEnabled: true);

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