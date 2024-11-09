namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct Components {

        public static uint GetReservedSizeInBytes(State* state) {

            if (state->components.items.IsCreated == false) return 0u;
            
            var size = 0u;
            var c = StaticTypes.counter;
            for (uint i = 1u; i <= c; ++i) {
                ref var ptr = ref state->components.items[in state->allocator, i];
                ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
                size += storage.GetReservedSizeInBytes(state);
            }
            
            return size;
            
        }
        
        [INLINE(256)]
        public static void OnEntityAdd(State* state, uint entityId) {

            var c = StaticTypes.counter;
            for (uint i = 1u; i <= c; ++i) {
                ref var ptr = ref state->components.items[in state->allocator, i];
                ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
                storage.OnEntityAdd(state, entityId);
            }

        }
        
        [INLINE(256)]
        public static bool SetUnknownType(State* state, uint typeId, uint groupId, in Ent ent, void* data) {

            E.IS_VALID_TYPE_ID(typeId);

            ref var ptr = ref state->components.items[in state->allocator, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
            var isNew = storage.Set(state, ent.id, ent.gen, data, out var changed);
            if (changed == true) Ents.UpVersion(state, in ent, groupId);
            return isNew;

        }

        [INLINE(256)]
        public static bool SetUnknownType<T>(State* state, uint typeId, uint groupId, in Ent ent, in T data) where T : unmanaged, IComponent {

            fixed (T* dataPtr = &data) {
                return Components.SetUnknownType(state, typeId, groupId, in ent, dataPtr);
            }

        }

        [INLINE(256)]
        public static bool SetState(State* state, uint typeId, uint groupId, in Ent ent, bool value) {

            E.IS_VALID_TYPE_ID(typeId);
            
            ref var ptr = ref state->components.items[in state->allocator, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
            var res = storage.SetState(state, ent.id, ent.gen, value);
            Ents.UpVersion(state, in ent, groupId);
            return res;

        }

        [INLINE(256)]
        public static byte* GetUnknownType(State* state, uint typeId, uint groupId, in Ent ent, out bool isNew) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            ref var ptr = ref state->components.items[in state->allocator, typeId];
            return GetUnknownType(state, in ptr, typeId, groupId, in ent, out isNew);

        }

        [INLINE(256)]
        public static byte* GetUnknownType(State* state, in MemAllocatorPtr storage, uint typeId, uint groupId, in Ent ent, out bool isNew) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var data = storage.AsPtr<DataDenseSet>(in state->allocator)->Get(state, ent.id, ent.gen, false, out isNew);
            Ents.UpVersion(state, in ent, groupId);
            return data;

        }

        [INLINE(256)]
        public static bool RemoveUnknownType(State* state, uint typeId, uint groupId, in Ent ent) {

            E.IS_VALID_TYPE_ID(typeId);

            ref var ptr = ref state->components.items[state, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
            if (storage.Remove(state, ent.id, ent.gen) == true) {
                Ents.UpVersion(state, in ent, groupId);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static byte* ReadUnknownType(State* state, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            ref var ptr = ref state->components.items[state, typeId];
            return ReadUnknownType(state, ptr, typeId, entId, gen, out exists);
            
        }

        [INLINE(256)]
        public static byte* ReadUnknownType(State* state, MemAllocatorPtr storage, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var data = storage.AsPtr<DataDenseSet>(in state->allocator)->Get(state, entId, gen, true, out _);
            exists = data != null;
            return data;

        }

        [INLINE(256)]
        public static bool HasUnknownType(State* state, uint typeId, uint entId, ushort gen, bool checkEnabled) {

            E.IS_VALID_TYPE_ID(typeId);

            ref var ptr = ref state->components.items[state, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
            return storage.Has(state, entId, gen, checkEnabled);
            
        }

        [INLINE(256)]
        public static ref MemAllocatorPtr GetUnsafeSparseSetPtr(State* state, uint typeId) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            return ref state->components.items[state, typeId];
            
        }

    }

}