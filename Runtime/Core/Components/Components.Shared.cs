namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;

    /// <summary>
    /// TODO: Locks improvement required
    /// </summary>
    public unsafe partial struct Components {

        public const uint COMPONENT_SHARED_DEFAULT_HASH = 0u;

        [StructLayout(LayoutKind.Sequential)]
        public struct SharedComponentStorageUnknown {

            public UIntHashSet entities;
            public MemAllocatorPtr data;
            
            public SharedComponentStorageUnknown(safe_ptr<State> state, safe_ptr data, uint dataSize) {
                this = default;
                this.data.Set(ref state.ptr->allocator, data, dataSize);
                this.entities = new UIntHashSet(ref state.ptr->allocator, 1u);
            }

            public void Dispose(ref MemoryAllocator allocator) {
                this.entities.Dispose(ref allocator);
                this = default;
            }

        }

        [INLINE(256)]
        private Components InitializeSharedComponents(safe_ptr<State> state, in StateProperties stateProperties) {

            this.sharedData = new UIntDictionary<MemAllocatorPtr>(ref state.ptr->allocator, stateProperties.sharedComponentsCapacity);
            this.entityIdToHash = new MemArray<MemArray<uint>>(ref state.ptr->allocator, stateProperties.entitiesCapacity);
            return this;

        }

        [INLINE(256)]
        private static uint GetStoredSharedHash<T>(safe_ptr<State> state, in Components components, uint entId) where T : unmanaged, IComponentShared {

            if (entId >= components.entityIdToHash.Length) return Components.COMPONENT_SHARED_DEFAULT_HASH;
            var typeId = StaticTypes<T>.sharedTypeId; 
            ref var arr = ref components.entityIdToHash[state, entId];
            if (typeId >= arr.Length) return Components.COMPONENT_SHARED_DEFAULT_HASH;
            return arr[state, typeId];

        }

        [INLINE(256)]
        private static uint GetSharedHash<T>(in T data, safe_ptr<State> state, in Components components, uint entId, uint hash) where T : unmanaged, IComponentShared {

            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) {
                // try to use stored hash
                hash = GetStoredSharedHash<T>(state, in components, entId);
                if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) {
                    // no stored hash - use default hash
                    hash = GetDataSharedHash(in data);
                }
            }

            return hash;

        }

        [INLINE(256)]
        private static uint GetDataSharedHash<T>(in T data)  where T : unmanaged, IComponentShared {

            // [!] C# now has no way to prevent copying here
            var customHash = StaticTypes<T>.hasSharedCustomHash == true ? data.GetHash() : COMPONENT_SHARED_DEFAULT_HASH;
            if (customHash == COMPONENT_SHARED_DEFAULT_HASH) {
                // use typeId as hash
                customHash = StaticTypes<T>.sharedTypeId;
            }

            return customHash;

        }

        [INLINE(256)]
        private static void SetSharedHash(safe_ptr<State> state, ref Components components, uint entId, uint typeId, uint hash) {
            
            if (entId >= components.entityIdToHash.Length) components.entityIdToHash.Resize(ref state.ptr->allocator, entId + 1u, 2);
            ref var typeIdToHash = ref components.entityIdToHash[state, entId];
            if (typeId >= typeIdToHash.Length) typeIdToHash.Resize(ref state.ptr->allocator, typeId + 1u, 2);
            typeIdToHash[state, typeId] = hash;

        }

        [INLINE(256)]
        public static bool SetShared<T>(safe_ptr<State> state, in Ent ent, in T data, uint hash = 0u) where T : unmanaged, IComponentShared {

            // No custom hash provided - use data hash
            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) {
                hash = GetDataSharedHash(in data);
            }

            var rData = data;
            return SetShared(state, in ent, StaticTypes<T>.groupId, _address(ref rData).ptr, TSize<T>.size, StaticTypes<T>.sharedTypeId, hash);
            
        }

        [INLINE(256)]
        public static bool SetShared(safe_ptr<State> state, in Ent ent, uint groupId, void* data, uint dataSize, uint sharedTypeId, uint hash) {

            // No custom hash provided - use data hash
            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) {
                // use typeId as hash
                hash = sharedTypeId;
            }
            
            state.ptr->components.lockSharedIndex.Lock();
            // get shared storage for component by hash
            ref var ptr = ref state.ptr->components.sharedData.GetValue(ref state.ptr->allocator, hash, out var exist);
            if (exist == false) ptr.Set(ref state.ptr->allocator, new SharedComponentStorageUnknown(state, (safe_ptr)data, dataSize));

            // update data in storage
            ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state.ptr->allocator);
            var dataMemPtr = storage.data.ptr;
            var dataPtr = state.ptr->allocator.GetUnsafePtr(in dataMemPtr);
            if (dataSize > 0u) _memcpy((safe_ptr)data, dataPtr, dataSize);
            var added = storage.entities.Add(ref state.ptr->allocator, ent.id);
            
            // update indexer
            SetSharedHash(state, ref state.ptr->components, ent.id, sharedTypeId, hash);
            
            if (added == true) Ents.UpVersion(state, in ent, groupId);
            state.ptr->components.lockSharedIndex.Unlock();
            
            return added;

        }

        [INLINE(256)]
        public static void ClearShared(safe_ptr<State> state, uint entId) {
            
            if (entId >= state.ptr->components.entityIdToHash.Length) return;
            
            state.ptr->components.lockSharedIndex.Lock();
            ref var typeIdToHash = ref state.ptr->components.entityIdToHash[state, entId];
            for (uint i = 0; i < typeIdToHash.Length; ++i) {
                
                var hash = typeIdToHash[state, i];
                if (state.ptr->components.sharedData.TryGetValue(in state.ptr->allocator, hash, out var ptr) == false) {
                    continue;
                }
            
                // get data from storage
                ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state.ptr->allocator);
                var exist = storage.entities.Remove(ref state.ptr->allocator, entId);
                if (exist == true && storage.entities.Count == 0u) {
                    // remove data from storage
                    storage.Dispose(ref state.ptr->allocator);
                    ptr.Dispose(ref state.ptr->allocator);
                    state.ptr->components.sharedData.Remove(in state.ptr->allocator, hash);
                }

            }
            state.ptr->components.lockSharedIndex.Unlock();
            
        }

        [INLINE(256)]
        public static bool RemoveShared<T>(safe_ptr<State> state, in Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (ent.id >= state.ptr->components.entityIdToHash.Length) return false;
            
            hash = GetSharedHash(default(T), state, in state.ptr->components, ent.id, hash);
            
            state.ptr->components.lockSharedIndex.Lock();
            // get shared storage for component by hash
            if (state.ptr->components.sharedData.TryGetValue(in state.ptr->allocator, hash, out var ptr) == false) {
                return false;
            }
            
            // get data from storage
            ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state.ptr->allocator);
            var exist = storage.entities.Remove(ref state.ptr->allocator, ent.id);
            if (exist == true && storage.entities.Count == 0u) {
                // remove data from storage
                storage.Dispose(ref state.ptr->allocator);
                ptr.Dispose(ref state.ptr->allocator);
                state.ptr->components.sharedData.Remove(in state.ptr->allocator, hash);
            }
            
            if (exist == true) Ents.UpVersion<T>(state, in ent);
            state.ptr->components.lockSharedIndex.Unlock();
            
            return exist;

        }

        [INLINE(256)]
        public static ref readonly T ReadShared<T>(safe_ptr<State> state, uint entId, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (entId >= state.ptr->components.entityIdToHash.Length) return ref StaticTypes<T>.defaultValue;
            hash = GetSharedHash(default(T), state, in state.ptr->components, entId, hash);

            if (state.ptr->components.sharedData.TryGetValue(in state.ptr->allocator, hash, out var ptr) == true) {
                ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state.ptr->allocator);
                if (storage.entities.Contains(in state.ptr->allocator, entId) == false) return ref StaticTypes<T>.defaultValue;
                return ref *storage.data.AsPtr<T>(in state.ptr->allocator).ptr;
            } else {
                return ref StaticTypes<T>.defaultValue;
            }

        }

        [INLINE(256)]
        public static ref T GetShared<T>(safe_ptr<State> state, in Ent ent, uint hash, out bool isNew) where T : unmanaged, IComponentShared {

            state.ptr->components.lockSharedIndex.Lock();
            isNew = false;
            if (ent.id >= state.ptr->components.entityIdToHash.Length) state.ptr->components.entityIdToHash.Resize(ref state.ptr->allocator, ent.id + 1u, 2);
            hash = GetSharedHash(default(T), state, in state.ptr->components, ent.id, hash);
            
            // get shared storage for component by hash
            ref var ptr = ref state.ptr->components.sharedData.GetValue(ref state.ptr->allocator, hash, out var exist);
            if (exist == false) ptr.Set(ref state.ptr->allocator, new SharedComponentStorageUnknown(state, default, TSize<T>.size));

            // get data from storage
            ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state.ptr->allocator);
            if (storage.entities.Add(ref state.ptr->allocator, ent.id) == true) {
                isNew = true;
            }

            Ents.UpVersion<T>(state, in ent);
            state.ptr->components.lockSharedIndex.Unlock();
            return ref *storage.data.AsPtr<T>(in state.ptr->allocator).ptr;
            
        }

        [INLINE(256)]
        public static bool HasShared<T>(safe_ptr<State> state, uint entId, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (entId >= state.ptr->components.entityIdToHash.Length) return false;
            hash = GetSharedHash(default(T), state, in state.ptr->components, entId, hash);

            if (state.ptr->components.sharedData.TryGetValue(in state.ptr->allocator, hash, out var ptr) == true) {
                
                ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state.ptr->allocator);
                return storage.entities.Contains(in state.ptr->allocator, entId);

            }

            return false;

        }

        public static bool HasSharedDirect<T>(Ent ent) where T : unmanaged, IComponentShared {

            return Components.HasShared<T>(ent.World.state, ent.id);

        }

        public static T ReadSharedDirect<T>(Ent ent) where T : unmanaged, IComponentShared {

            return Components.ReadShared<T>(ent.World.state, ent.id);

        }

        public static void SetSharedDirect<T>(Ent ent, T data) where T : unmanaged, IComponentShared {

            Components.SetShared(ent.World.state, in ent, in data);

        }

    }

}