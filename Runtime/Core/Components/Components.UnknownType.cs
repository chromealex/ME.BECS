namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    public unsafe partial struct Components {

        public static uint GetReservedSizeInBytes(safe_ptr<State> state) {

            if (state.ptr->components.items.IsCreated == false) return 0u;
            
            var size = 0u;
            var c = StaticTypes.counter;
            for (uint i = 1u; i <= c; ++i) {
                var ptr = state.ptr->components.items.GetUnsafePtr(state, i);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                size += storage.ptr->GetReservedSizeInBytes(state);
            }
            
            return size;
            
        }
        
        [INLINE(256)][IgnoreProfiler]
        public static void OnEntityAdd(safe_ptr<State> state, uint entityId) {

            var c = StaticTypes.counter;
            for (uint i = 1u; i <= c; ++i) {
                var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, i);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                storage.ptr->OnEntityAdd(state, entityId);
            }

        }

        [INLINE(256)][IgnoreProfiler]
        public static bool SetUnknownType(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, void* data) {

            E.IS_VALID_TYPE_ID(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            var isNew = storage.ptr->Set(state, ent.id, ent.gen, data, out var changed);
            if (changed == true) Ents.UpVersion(state, in ent, groupId);
            return isNew;

        }

        [INLINE(256)][IgnoreProfiler]
        public static bool SetUnknownType<T>(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, in T data) where T : unmanaged, IComponent {

            fixed (T* dataPtr = &data) {
                return Components.SetUnknownType(state, typeId, groupId, in ent, dataPtr);
            }

        }

        [INLINE(256)][IgnoreProfiler]
        public static bool SetState(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, bool value) {

            E.IS_VALID_TYPE_ID(typeId);
            
            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            var res = storage.ptr->SetState(state, ent.id, ent.gen, value);
            Ents.UpVersion(state, in ent, groupId);
            return res;

        }

        [INLINE(256)][IgnoreProfiler]
        public static bool ReadState(safe_ptr<State> state, uint typeId, in Ent ent) {

            E.IS_VALID_TYPE_ID(typeId);
            
            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            return storage.ptr->ReadState(state, ent.id, ent.gen);

        }

        [INLINE(256)][IgnoreProfiler]
        public static byte* GetUnknownType(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            return GetUnknownType(state, ptr, typeId, groupId, in ent, out isNew, defaultValue);

        }

        [INLINE(256)][IgnoreProfiler]
        public static byte* GetUnknownType(safe_ptr<State> state, safe_ptr<MemAllocatorPtr> storage, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->Get(state, ent.id, ent.gen, out isNew, defaultValue);
            Ents.UpVersion(state, in ent, groupId);
            return data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static byte* GetUnknownType(safe_ptr<State> state, in MemAllocatorPtr storage, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->Get(state, ent.id, ent.gen, out isNew, defaultValue);
            Ents.UpVersion(state, in ent, groupId);
            return data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static bool RemoveUnknownType(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent) {

            E.IS_VALID_TYPE_ID(typeId);
            
            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            if (storage.ptr->Remove(state, ent.id, ent.gen) == true) {
                Ents.UpVersion(state, in ent, groupId);
                return true;
            }
            
            return false;

        }

        [INLINE(256)][IgnoreProfiler]
        public static byte* ReadUnknownType(safe_ptr<State> state, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            return ReadUnknownType(state, ptr, typeId, entId, gen, out exists);
            
        }

        [INLINE(256)][IgnoreProfiler]
        public static byte* ReadUnknownType(safe_ptr<State> state, safe_ptr<MemAllocatorPtr> storage, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->Read(state, entId, gen, out _);
            exists = data != null;
            return data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static byte* ReadUnknownType(safe_ptr<State> state, MemAllocatorPtr storage, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->Read(state, entId, gen, out _);
            exists = data != null;
            return data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static bool HasUnknownType(safe_ptr<State> state, uint typeId, uint entId, ushort gen, bool checkEnabled) {

            E.IS_VALID_TYPE_ID(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            return storage.ptr->Has(state, entId, gen, checkEnabled);
            
        }

        [INLINE(256)][IgnoreProfiler]
        public static ref MemAllocatorPtr GetUnsafeSparseSetPtr(safe_ptr<State> state, uint typeId) {

            E.IS_VALID_TYPE_ID(typeId);

            return ref state.ptr->components.items[state, typeId];
            
        }

    }

}