namespace ME.BECS {
    
    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
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

            this.sharedData = new ULongDictionary<MemAllocatorPtr>(ref state.ptr->allocator, stateProperties.sharedComponentsCapacity);
            this.sharedTypesCount = StaticSharedTypes.counter + 1u;
            this.entityIdToHash = new MemArray<uint>(ref state.ptr->allocator, stateProperties.EntitiesCapacity * this.sharedTypesCount);
            return this;

        }

        [INLINE(256)]
        private static ulong GetSharedKey(uint sharedTypeId, uint hash) {

            return ((ulong)sharedTypeId << 32) | hash;

        }

        [INLINE(256)]
        private static uint GetStoredSharedHash(safe_ptr<State> state, in Components components, uint entId, uint sharedTypeId) {

            if (sharedTypeId >= components.sharedTypesCount) return Components.COMPONENT_SHARED_DEFAULT_HASH;
            var index = entId * components.sharedTypesCount + sharedTypeId;
            if (index >= components.entityIdToHash.Length) return Components.COMPONENT_SHARED_DEFAULT_HASH;
            return components.entityIdToHash[state, index];

        }

        [INLINE(256)]
        private static uint GetStoredSharedHash<T>(safe_ptr<State> state, in Components components, uint entId) where T : unmanaged, IComponentShared {

            return GetStoredSharedHash(state, in components, entId, StaticTypes<T>.sharedTypeId);

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

            E.RANGE(typeId, 0u, components.sharedTypesCount);
            var index = entId * components.sharedTypesCount + typeId;
            if (index >= components.entityIdToHash.Length) components.entityIdToHash.Resize(ref state.ptr->allocator, index + 1u, 2);
            components.entityIdToHash[state, index] = hash;

        }

        [INLINE(256)]
        private static bool RemoveSharedEntity(safe_ptr<State> state, ref Components components, uint entId, uint sharedTypeId, uint hash) {

            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) return false;
            var key = GetSharedKey(sharedTypeId, hash);
            if (components.sharedData.TryGetValue(in state.ptr->allocator, key, out var ptr) == false) return false;

            var storage = ptr.AsPtr<SharedComponentStorageUnknown>(in state.ptr->allocator);
            var removed = storage.ptr->entities.Remove(ref state.ptr->allocator, entId);
            if (removed == true && storage.ptr->entities.Count == 0u) {
                storage.ptr->Dispose(ref state.ptr->allocator);
                ptr.Dispose(ref state.ptr->allocator);
                components.sharedData.Remove(in state.ptr->allocator, key);
            }

            return removed;

        }

        [INLINE(256)]
        public static bool SetShared<T>(safe_ptr<State> state, in Ent ent, in T data, uint hash = 0u) where T : unmanaged, IComponentShared {

            // No custom hash provided - use data hash
            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) {
                hash = GetDataSharedHash(in data);
            }

            var rData = data;
            return SetShared(state, in ent, StaticTypes<T>.trackerIndex, _address(ref rData).ptr, TSize<T>.size, StaticTypes<T>.sharedTypeId, hash, out _);
            
        }

        [INLINE(256)]
        public static bool SetShared(safe_ptr<State> state, in Ent ent, uint groupId, void* data, uint dataSize, uint sharedTypeId, uint hash, out safe_ptr dataPtr) {

            // No custom hash provided - use data hash
            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) {
                // use typeId as hash
                hash = sharedTypeId;
            }
            
            state.ptr->components.lockSharedIndex.Lock();
            var previousHash = GetStoredSharedHash(state, in state.ptr->components, ent.id, sharedTypeId);
            if (previousHash != hash) {
                RemoveSharedEntity(state, ref state.ptr->components, ent.id, sharedTypeId, previousHash);
            }
            // get shared storage for component by hash
            var key = GetSharedKey(sharedTypeId, hash);
            ref var ptr = ref state.ptr->components.sharedData.GetValue(ref state.ptr->allocator, key, out var exist);
            if (exist == false) ptr.Set(ref state.ptr->allocator, new SharedComponentStorageUnknown(state, (safe_ptr)data, dataSize));

            // update data in storage
            var storage = ptr.AsPtr<SharedComponentStorageUnknown>(in state.ptr->allocator);
            var dataMemPtr = storage.ptr->data.ptr;
            dataPtr = state.ptr->allocator.GetUnsafePtr(in dataMemPtr);
            if (dataSize > 0u) _memcpy((safe_ptr)data, dataPtr, dataSize);
            var added = storage.ptr->entities.Add(ref state.ptr->allocator, ent.id);
            
            // update indexer
            SetSharedHash(state, ref state.ptr->components, ent.id, sharedTypeId, hash);
            
            if (added == true) Ents.UpVersion(state, in ent, groupId);
            state.ptr->components.lockSharedIndex.Unlock();
            
            return added;

        }

        [INLINE(256)]
        public static void ClearShared(safe_ptr<State> state, uint entId) {

            var rowOffset = entId * state.ptr->components.sharedTypesCount;
            if (rowOffset >= state.ptr->components.entityIdToHash.Length) return;
            
            state.ptr->components.lockSharedIndex.Lock();
            for (uint i = 0; i < state.ptr->components.sharedTypesCount; ++i) {
                
                var hash = state.ptr->components.entityIdToHash[state, rowOffset + i];
                if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) continue;
                RemoveSharedEntity(state, ref state.ptr->components, entId, i, hash);
                state.ptr->components.entityIdToHash[state, rowOffset + i] = Components.COMPONENT_SHARED_DEFAULT_HASH;

            }
            state.ptr->components.lockSharedIndex.Unlock();
            
        }

        [INLINE(256)]
        public static bool RemoveShared<T>(safe_ptr<State> state, in Ent ent, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (ent.id * state.ptr->components.sharedTypesCount >= state.ptr->components.entityIdToHash.Length) return false;
            
            state.ptr->components.lockSharedIndex.Lock();
            var sharedTypeId = StaticTypes<T>.sharedTypeId;
            var storedHash = GetStoredSharedHash(state, in state.ptr->components, ent.id, sharedTypeId);
            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) hash = storedHash;
            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) hash = GetDataSharedHash(default(T));
            var exist = RemoveSharedEntity(state, ref state.ptr->components, ent.id, sharedTypeId, hash);
            if (exist == true && storedHash == hash) {
                SetSharedHash(state, ref state.ptr->components, ent.id, sharedTypeId, Components.COMPONENT_SHARED_DEFAULT_HASH);
            }
            
            if (exist == true) Ents.UpVersion<T>(state, in ent);
            state.ptr->components.lockSharedIndex.Unlock();
            
            return exist;

        }

        [INLINE(256)]
        public static ref readonly T ReadShared<T>(safe_ptr<State> state, uint entId, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (entId * state.ptr->components.sharedTypesCount >= state.ptr->components.entityIdToHash.Length) return ref StaticTypes<T>.defaultValue;
            hash = GetSharedHash(default(T), state, in state.ptr->components, entId, hash);

            var key = GetSharedKey(StaticTypes<T>.sharedTypeId, hash);
            if (state.ptr->components.sharedData.TryGetValue(in state.ptr->allocator, key, out var ptr) == true) {
                var storage = ptr.AsPtr<SharedComponentStorageUnknown>(in state.ptr->allocator);
                if (storage.ptr->entities.Contains(in state.ptr->allocator, entId) == false) return ref StaticTypes<T>.defaultValue;
                return ref *storage.ptr->data.AsPtr<T>(in state.ptr->allocator).ptr;
            } else {
                return ref StaticTypes<T>.defaultValue;
            }

        }

        [INLINE(256)]
        public static ref T GetShared<T>(safe_ptr<State> state, in Ent ent, uint hash, out bool isNew) where T : unmanaged, IComponentShared {

            state.ptr->components.lockSharedIndex.Lock();
            isNew = false;
            hash = GetSharedHash(default(T), state, in state.ptr->components, ent.id, hash);
            var sharedTypeId = StaticTypes<T>.sharedTypeId;
            var previousHash = GetStoredSharedHash(state, in state.ptr->components, ent.id, sharedTypeId);
            if (previousHash != hash) {
                RemoveSharedEntity(state, ref state.ptr->components, ent.id, sharedTypeId, previousHash);
            }
            
            // get shared storage for component by hash
            var key = GetSharedKey(sharedTypeId, hash);
            ref var ptr = ref state.ptr->components.sharedData.GetValue(ref state.ptr->allocator, key, out var exist);
            if (exist == false) ptr.Set(ref state.ptr->allocator, new SharedComponentStorageUnknown(state, default, TSize<T>.size));

            // get data from storage
            var storage = ptr.AsPtr<SharedComponentStorageUnknown>(in state.ptr->allocator);
            if (storage.ptr->entities.Add(ref state.ptr->allocator, ent.id) == true) {
                isNew = true;
            }
            SetSharedHash(state, ref state.ptr->components, ent.id, sharedTypeId, hash);

            Ents.UpVersion<T>(state, in ent);
            state.ptr->components.lockSharedIndex.Unlock();
            return ref *storage.ptr->data.AsPtr<T>(in state.ptr->allocator).ptr;
            
        }

        [INLINE(256)]
        public static bool HasShared<T>(safe_ptr<State> state, uint entId, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (entId * state.ptr->components.sharedTypesCount >= state.ptr->components.entityIdToHash.Length) return false;
            hash = GetSharedHash(default(T), state, in state.ptr->components, entId, hash);

            var key = GetSharedKey(StaticTypes<T>.sharedTypeId, hash);
            if (state.ptr->components.sharedData.TryGetValue(in state.ptr->allocator, key, out var ptr) == true) {
                
                var storage = ptr.AsPtr<SharedComponentStorageUnknown>(in state.ptr->allocator);
                return storage.ptr->entities.Contains(in state.ptr->allocator, entId);

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
