namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct Components {

        public uint GetReservedSizeInBytes(State* state) {

            if (this.items.IsCreated == false) return 0u;
            
            var size = 0u;
            var c = StaticTypes.counter;
            for (uint i = 1u; i <= c; ++i) {
                ref var ptr = ref this.items[in state->allocator, i];
                ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
                size += storage.GetReservedSizeInBytes(state);
            }
            
            return size;
            
        }
        
        [INLINE(256)]
        public void CopyFrom(State* sourceState, in Ent ent, State* targetState, in Ent targetEnt) {

            var srcArchId = sourceState->archetypes.entToArchetypeIdx[sourceState, ent.id];
            ref var srcArch = ref sourceState->archetypes.list[sourceState, srcArchId];
            var e = srcArch.components.GetEnumerator(sourceState);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                var groupId = StaticTypes.groups.Get(typeId);
                ref var ptr = ref this.items[in sourceState->allocator, typeId];
                ref var storage = ref ptr.As<DataDenseSet>(in sourceState->allocator);
                var data = storage.Get(sourceState, ent.id, ent.gen, true, out _);
                targetState->components.SetUnknownType(targetState, typeId, groupId, in targetEnt, data);
                targetState->batches.Set_INTERNAL(typeId, in targetEnt, targetState);
            }
            
        }

        [INLINE(256)]
        public void OnEntityAdd(State* state, uint entityId) {

            var c = StaticTypes.counter;
            for (uint i = 1u; i <= c; ++i) {
                ref var ptr = ref this.items[in state->allocator, i];
                ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
                storage.OnEntityAdd(state, entityId);
            }

        }
        
        [INLINE(256)]
        public bool SetUnknownType(State* state, uint typeId, uint groupId, in Ent ent, void* data) {

            E.IS_VALID_TYPE_ID(typeId);

            ref var ptr = ref this.items[in state->allocator, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
            var isNew = storage.Set(state, ent.id, ent.gen, data, out var changed);
            if (changed == true) state->entities.UpVersion(state, in ent, groupId);
            return isNew;

        }

        [INLINE(256)]
        public bool SetUnknownType<T>(State* state, uint typeId, uint groupId, in Ent ent, in T data) where T : unmanaged, IComponent {

            fixed (T* dataPtr = &data) {
                return this.SetUnknownType(state, typeId, groupId, in ent, dataPtr);
            }

        }

        [INLINE(256)]
        public bool SetState<T>(State* state, uint typeId, uint groupId, in Ent ent, bool value) where T : unmanaged {

            E.IS_VALID_TYPE_ID(typeId);
            
            ref var ptr = ref this.items[in state->allocator, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
            var res = storage.SetState(state, ent.id, ent.gen, value);
            state->entities.UpVersion(state, in ent, groupId);
            return res;

        }

        [INLINE(256)]
        public byte* GetUnknownType(State* state, uint typeId, uint groupId, in Ent ent, out bool isNew) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            ref var ptr = ref this.items[in state->allocator, typeId];
            return GetUnknownType(state, in ptr, typeId, groupId, in ent, out isNew);

        }

        [INLINE(256)]
        public static byte* GetUnknownType(State* state, in MemAllocatorPtr storage, uint typeId, uint groupId, in Ent ent, out bool isNew) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var data = storage.AsPtr<DataDenseSet>(in state->allocator)->Get(state, ent.id, ent.gen, false, out isNew);
            state->entities.UpVersion(state, in ent, groupId);
            return data;

        }

        [INLINE(256)]
        public bool RemoveUnknownType(State* state, uint typeId, uint groupId, in Ent ent) {

            E.IS_VALID_TYPE_ID(typeId);

            ref var ptr = ref this.items[state, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
            if (storage.Remove(state, ent.id, ent.gen) == true) {
                state->entities.UpVersion(state, in ent, groupId);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public readonly byte* ReadUnknownType(State* state, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            ref var ptr = ref this.items[state, typeId];
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
        public bool HasUnknownType(State* state, uint typeId, uint entId, ushort gen, bool checkEnabled) {

            E.IS_VALID_TYPE_ID(typeId);

            ref var ptr = ref this.items[state, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in state->allocator);
            return storage.Has(state, entId, gen, checkEnabled);
            
        }

        [INLINE(256)]
        public readonly ref MemAllocatorPtr GetUnsafeSparseSetPtr(State* state, uint typeId) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            return ref this.items[state, typeId];
            
        }

    }

}