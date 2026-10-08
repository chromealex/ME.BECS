namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    /// <summary>
    /// Defines components data used by entity processing.
    /// </summary>
    public unsafe partial struct Components {

        /// <summary>
        /// Returns the amount of reserved storage in bytes.
        /// </summary>
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
        
        /// <summary>
        /// Handles the entity add callback.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void OnEntityAdd(safe_ptr<State> state, ushort worldId, uint entityId) {

            var requiredCapacity = Bitwise.AlignUp(entityId + 1u, DataDenseSet.ENTITIES_PER_PAGE);
            if (System.Threading.Volatile.Read(ref state.ptr->components.entitiesCapacity) >= requiredCapacity) return;

            state.ptr->components.resizeLock.LockWhile();
            if (state.ptr->components.entitiesCapacity < requiredCapacity) {
                var c = StaticTypes.counter;
                var allocatedCapacity = requiredCapacity;
                for (uint i = 1u; i <= c; ++i) {
                    var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, i);
                    var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                    var storageCapacity = storage.ptr->OnEntityAdd(state, worldId, entityId);
                    if (i == 1u || storageCapacity < allocatedCapacity) allocatedCapacity = storageCapacity;
                }
                System.Threading.Volatile.Write(ref state.ptr->components.entitiesCapacity, allocatedCapacity);
            }
            state.ptr->components.resizeLock.UnlockWhile();

        }

        /// <summary>
        /// Sets unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool SetUnknownType(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, void* data) {

            E.IS_VALID_TYPE_ID(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            var isNew = storage.ptr->Set(state, ent.worldId, ent.id, ent.gen, data, out var changed);
            if (changed == true) Ents.UpVersion(state, in ent, groupId);
            return isNew;

        }

        /// <summary>
        /// Sets unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool SetUnknownType<T>(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, in T data) where T : unmanaged, IComponent {

            fixed (T* dataPtr = &data) {
                return Components.SetUnknownType(state, typeId, groupId, in ent, dataPtr);
            }

        }

        /// <summary>
        /// Sets state.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool SetState(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, bool value) {

            E.IS_VALID_TYPE_ID(typeId);
            
            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            var res = storage.ptr->SetState(state, ent.id, ent.gen, value);
            Ents.UpVersion(state, in ent, groupId);
            return res;

        }

        /// <summary>
        /// Reads state.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool ReadState(safe_ptr<State> state, uint typeId, in Ent ent) {

            E.IS_VALID_TYPE_ID(typeId);
            
            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            return storage.ptr->ReadState(state, ent.id, ent.gen);

        }

        /// <summary>
        /// Returns or throw unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* GetOrThrowUnknownType(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            return GetOrThrowUnknownType(state, ptr, typeId, groupId, in ent, out isNew, defaultValue);

        }

        /// <summary>
        /// Returns or throw unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* GetOrThrowUnknownType(safe_ptr<State> state, safe_ptr<MemAllocatorPtr> storage, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->GetOrThrow(state, ent.id, ent.gen, out isNew, defaultValue);
            Ents.UpVersion(state, in ent, groupId);
            return data;

        }

        /// <summary>
        /// Returns or throw unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* GetOrThrowUnknownType(safe_ptr<State> state, in MemAllocatorPtr storage, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->GetOrThrow(state, ent.id, ent.gen, out isNew, defaultValue);
            Ents.UpVersion(state, in ent, groupId);
            return data;

        }

        /// <summary>
        /// Returns unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* GetUnknownType(safe_ptr<State> state, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            return GetUnknownType(state, ptr, typeId, groupId, in ent, out isNew, defaultValue);

        }

        /// <summary>
        /// Returns unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* GetUnknownType(safe_ptr<State> state, safe_ptr<MemAllocatorPtr> storage, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->Get(state, ent.worldId, ent.id, ent.gen, out isNew, defaultValue);
            Ents.UpVersion(state, in ent, groupId);
            return data;

        }

        /// <summary>
        /// Returns unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* GetUnknownType(safe_ptr<State> state, in MemAllocatorPtr storage, uint typeId, uint groupId, in Ent ent, out bool isNew, safe_ptr defaultValue) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->Get(state, ent.worldId, ent.id, ent.gen, out isNew, defaultValue);
            Ents.UpVersion(state, in ent, groupId);
            return data;

        }

        /// <summary>
        /// Removes unknown type.
        /// </summary>
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

        /// <summary>
        /// Reads unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* ReadUnknownType(safe_ptr<State> state, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);
            E.IS_NOT_TAG(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            return ReadUnknownType(state, ptr, typeId, entId, gen, out exists);
            
        }

        /// <summary>
        /// Reads unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* ReadUnknownType(safe_ptr<State> state, safe_ptr<MemAllocatorPtr> storage, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->Read(state, entId, gen, out _);
            exists = data != null;
            return data;

        }

        /// <summary>
        /// Reads unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static byte* ReadUnknownType(safe_ptr<State> state, MemAllocatorPtr storage, uint typeId, uint entId, ushort gen, out bool exists) {

            E.IS_VALID_TYPE_ID(typeId);

            var data = storage.AsPtr<DataDenseSet>(in state.ptr->allocator).ptr->Read(state, entId, gen, out _);
            exists = data != null;
            return data;

        }

        /// <summary>
        /// Tests whether the context has unknown type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool HasUnknownType(safe_ptr<State> state, uint typeId, uint entId, ushort gen, bool checkEnabled) {

            E.IS_VALID_TYPE_ID(typeId);

            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            return storage.ptr->Has(state, entId, gen, checkEnabled);
            
        }

        /// <summary>
        /// Returns unsafe sparse set ptr.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref MemAllocatorPtr GetUnsafeSparseSetPtr(safe_ptr<State> state, uint typeId) {

            E.IS_VALID_TYPE_ID(typeId);

            return ref state.ptr->components.items[state, typeId];
            
        }

    }

}
