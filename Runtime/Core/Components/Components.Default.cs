namespace ME.BECS {
    
    using static Cuts;
    using MemPtr = System.Int64;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct Components {

        [INLINE(256)]
        public bool Set<T>(State* state, uint entId, ushort gen, in T data) where T : unmanaged {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return this.SetUnknownType(state, typeId, groupId, entId, gen, in data);
            
        }

        [INLINE(256)]
        public bool Remove<T>(State* state, uint entId, ushort gen) where T : unmanaged {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            return this.RemoveUnknownType(state, typeId, groupId, entId, gen);

        }

        [INLINE(256)]
        public readonly ref readonly T Read<T>(State* state, uint entId, ushort gen, out bool exists) where T : unmanaged {

            var typeId = StaticTypes<T>.typeId;
            var data = this.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)]
        public readonly ref readonly T Read<T>(State* state, uint entId, ushort gen) where T : unmanaged {

            var typeId = StaticTypes<T>.typeId;
            var data = this.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)]
        public readonly T* ReadPtr<T>(State* state, uint entId, ushort gen, out bool exists) where T : unmanaged {

            var typeId = StaticTypes<T>.typeId;
            var data = this.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)]
        public readonly T* ReadPtr<T>(State* state, uint entId, ushort gen) where T : unmanaged {

            var typeId = StaticTypes<T>.typeId;
            var data = this.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)]
        public bool Has<T>(State* state, uint entId, ushort gen) where T : unmanaged {

            var typeId = StaticTypes<T>.typeId;
            return this.HasUnknownType(state, typeId, entId, gen);

        }

        [INLINE(256)]
        public ref T Get<T>(State* state, uint entId, ushort gen) where T : unmanaged {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var data = this.GetUnknownType(state, typeId, groupId, entId, gen, out _);
            return ref *(T*)data;

        }

        [INLINE(256)]
        public T* Get<T>(State* state, uint entId, ushort gen, out bool isNew) where T : unmanaged {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var data = this.GetUnknownType(state, typeId, groupId, entId, gen, out isNew);
            return (T*)data;

        }

        public bool HasDirect<T>(Ent ent) where T : unmanaged {

            return this.Has<T>(ent.World.state, ent.id, ent.gen);

        }

        public T ReadDirect<T>(Ent ent) where T : unmanaged {

            if (StaticTypes<T>.isTag == true) return StaticTypes<T>.defaultValue;

            return this.Read<T>(ent.World.state, ent.id, ent.gen);

        }

        public void SetDirect<T>(Ent ent, T data) where T : unmanaged {

            if (StaticTypes<T>.isTag == true) return;

            this.Set(ent.World.state, ent.id, ent.gen, in data);

        }

    }

}