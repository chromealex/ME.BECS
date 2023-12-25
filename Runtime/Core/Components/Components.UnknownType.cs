namespace ME.BECS {

    using MemPtr = System.Int64;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct Components {

        public uint GetReservedSizeInBytes(State* state) {

            if (this.items.isCreated == false) return 0u;
            
            var size = 0u;
            var c = StaticTypes.counter;
            for (uint i = 1u; i <= c; ++i) {
                ref var ptr = ref this.items[in state->allocator, i];
                if (StaticTypes.sizes.Get(i) == 0u) {
                    ref var storage = ref ptr.As<SparseSetUnknownTypeTag>(in state->allocator);
                    size += storage.GetReservedSizeInBytes(state);
                } else {
                    ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
                    size += storage.GetReservedSizeInBytes(state);
                }
            }
            
            return size;
            
        }
        
        [INLINE(256)]
        public void CopyFrom(State* sourceState, uint sourceId, State* targetState, uint targetId, ushort targetGen) {

            var srcArchId = sourceState->archetypes.entToArchetypeIdx[sourceState, sourceId];
            ref var srcArch = ref sourceState->archetypes.list[sourceState, srcArchId];
            var e = srcArch.components.GetEnumerator(sourceState);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                var groupId = StaticTypes.groups.Get(typeId);
                ref var ptr = ref this.items[in sourceState->allocator, typeId];
                if (StaticTypes.sizes.Get(typeId) == 0u) {
                    targetState->components.SetUnknownType(targetState, typeId, groupId, targetId, targetGen, null);
                } else {
                    ref var storage = ref ptr.As<SparseSetUnknownType>(in sourceState->allocator);
                    var data = storage.Read(sourceState, sourceId);
                    targetState->components.SetUnknownType(targetState, typeId, groupId, targetId, targetGen, data);
                }
                targetState->batches.Set_INTERNAL(typeId, targetId, targetState);
            }
            
        }

        [INLINE(256)]
        public void OnEntityAdd(State* state, uint entityId) {

            var c = StaticTypes.counter;
            for (uint i = 1u; i <= c; ++i) {
                ref var ptr = ref this.items[in state->allocator, i];
                if (StaticTypes.sizes.Get(i) == 0u) {
                    ref var storage = ref ptr.As<SparseSetUnknownTypeTag>(in state->allocator);
                    storage.OnEntityAdd(state, entityId);
                } else {
                    ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
                    storage.OnEntityAdd(state, entityId);
                }
            }

        }
        
        [INLINE(256)]
        public bool SetUnknownType(State* state, uint typeId, uint groupId, uint entId, ushort gen, void* data) {

            E.IS_VALID_TYPE_ID(typeId);

            bool isNew;
            ref var ptr = ref this.items[in state->allocator, typeId];
            if (StaticTypes.sizes.Get(typeId) == 0u) {
                ref var storage = ref ptr.As<SparseSetUnknownTypeTag>(in state->allocator);
                storage.Set(state, entId, gen, out isNew);
            } else {
                ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
                storage.Set(state, entId, gen, data, out isNew);
            }

            state->entities.UpVersion(state, entId, groupId);
            return isNew;

        }

        [INLINE(256)]
        public bool SetUnknownType<T>(State* state, uint typeId, uint groupId, uint entId, ushort gen, in T data) where T : unmanaged {

            E.IS_VALID_TYPE_ID(typeId);

            bool isNew;
            ref var ptr = ref this.items[in state->allocator, typeId];
            if (StaticTypes<T>.isTag == true) {
                ref var storage = ref ptr.As<SparseSetUnknownTypeTag>(in state->allocator);
                storage.Set(state, entId, gen, out isNew);
            } else {
                ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
                storage.Set(state, entId, gen, in data, out isNew);
            }

            state->entities.UpVersion(state, entId, groupId);
            return isNew;

        }

        [INLINE(256)]
        public bool SetState<T>(State* state, uint typeId, uint groupId, uint entId, ushort gen, bool value) where T : unmanaged {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            ref var ptr = ref this.items[in state->allocator, typeId];
            ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
            var res = storage.SetState(state, entId, gen, value);
            state->entities.UpVersion(state, entId, groupId);
            return res;

        }

        [INLINE(256)]
        public byte* GetUnknownType(State* state, uint typeId, uint groupId, uint entId, ushort gen, out bool isNew) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            ref var ptr = ref this.items[in state->allocator, typeId];
            ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
            var data = storage.Get(state, entId, gen, out isNew);
            state->entities.UpVersion(state, entId, groupId);
            return data;

        }

        [INLINE(256)]
        public static byte* GetUnknownType(State* state, in MemAllocatorPtr storage, uint typeId, uint groupId, uint entId, ushort gen, out bool isNew) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var data = storage.AsPtr<SparseSetUnknownType>(in state->allocator)->Get(state, entId, gen, out isNew);
            state->entities.UpVersion(state, entId, groupId);
            return data;

        }

        [INLINE(256)]
        public bool RemoveUnknownType(State* state, uint typeId, uint groupId, uint entId, ushort gen) {

            E.IS_VALID_TYPE_ID(typeId);

            ref var ptr = ref this.items[state, typeId];
            if (StaticTypes.sizes.Get(typeId) == 0u) {
                ref var storage = ref ptr.As<SparseSetUnknownTypeTag>(in state->allocator);
                if (storage.Remove(state, entId, gen) == true) {
                    state->entities.UpVersion(state, entId, groupId);
                    return true;
                }
            } else {
                ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
                if (storage.Remove(state, entId, gen) == true) {
                    state->entities.UpVersion(state, entId, groupId);
                    return true;
                }
            }

            return false;

        }

        [INLINE(256)]
        public readonly byte* ReadUnknownType(State* state, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            ref var ptr = ref this.items[state, typeId];
            ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
            return storage.Read(state, entId, gen, out exists);
            
        }

        [INLINE(256)]
        public static byte* ReadUnknownType(State* state, MemAllocatorPtr storage, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            return storage.AsPtr<SparseSetUnknownType>(in state->allocator)->Read(state, entId, gen, out exists);
            
        }

        [INLINE(256)]
        public bool HasUnknownType(State* state, uint typeId, uint entId, ushort gen) {

            E.IS_VALID_TYPE_ID(typeId);

            ref var ptr = ref this.items[state, typeId];
            if (StaticTypes.sizes.Get(typeId) == 0u) {
                ref var storage = ref ptr.As<SparseSetUnknownTypeTag>(in state->allocator);
                return storage.Has(state, entId, gen);
            } else {
                ref var storage = ref ptr.As<SparseSetUnknownType>(in state->allocator);
                return storage.Has(state, entId, gen);
            }

        }

        [INLINE(256)]
        public readonly ref MemAllocatorPtr GetUnsafeSparseSetPtr(State* state, uint typeId) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            return ref this.items[state, typeId];
            
        }

    }

}