namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct Components {

        [INLINE(256)]
        public bool Enable<T>(State* state, in Ent ent) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return this.SetState<T>(state, typeId, groupId, in ent, true);
            
        }

        [INLINE(256)]
        public bool Disable<T>(State* state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return this.SetState<T>(state, typeId, groupId, in ent, false);
            
        }

        [INLINE(256)]
        public bool Set<T>(State* state, in Ent ent, in T data) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return this.SetUnknownType(state, typeId, groupId, in ent, in data);
            
        }

        [INLINE(256)]
        public bool Remove<T>(State* state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return this.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        [INLINE(256)]
        public bool Remove(State* state, in Ent ent, uint typeId, uint groupId) {

            return this.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        [INLINE(256)]
        public readonly ref readonly T Read<T>(State* state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            var data = this.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)]
        public readonly ref readonly T Read<T>(State* state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = this.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)]
        public readonly T* ReadPtr<T>(State* state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = this.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)]
        public readonly T* ReadPtr<T>(State* state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = this.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)]
        public bool Has<T>(State* state, uint entId, ushort gen, bool checkEnabled) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            return this.HasUnknownType(state, typeId, entId, gen, checkEnabled);

        }

        [INLINE(256)]
        public ref T Get<T>(State* state, in Ent ent) where T : unmanaged, IComponent => ref this.Get<T>(state, ent);

        [INLINE(256)]
        public ref T Get<T>(State* state, Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var data = this.GetUnknownType(state, typeId, groupId, in ent, out _);
            return ref *(T*)data;

        }

        [INLINE(256)]
        public T* Get<T>(State* state, in Ent ent, out bool isNew) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var data = this.GetUnknownType(state, typeId, groupId, in ent, out isNew);
            return (T*)data;

        }

        public bool HasDirect<T>(Ent ent) where T : unmanaged, IComponent {

            return this.Has<T>(ent.World.state, ent.id, ent.gen, checkEnabled: false);

        }

        public T ReadDirect<T>(Ent ent) where T : unmanaged, IComponent {

            if (StaticTypes<T>.isTag == true) return StaticTypes<T>.defaultValue;

            return this.Read<T>(ent.World.state, ent.id, ent.gen);

        }

        public void SetDirect<T>(Ent ent, T data) where T : unmanaged, IComponent {

            if (StaticTypes<T>.isTag == true) return;

            this.Set(ent.World.state, in ent, in data);

        }

    }

}