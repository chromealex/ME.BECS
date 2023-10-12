namespace ME.BECS {
    
    using static Cuts;
    using MemPtr = System.Int64;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;

    public interface IComponentShared {

        /// <summary>
        /// Returns static hash of instance
        /// </summary>
        /// <returns></returns>
        uint GetHash() => throw new System.NotImplementedException();

    }

    public unsafe partial struct Components {

        public const uint COMPONENT_SHARED_DEFAULT_HASH = 0u;

        [StructLayout(LayoutKind.Sequential)]
        public struct SharedComponentStorageUnknown {

            public UIntHashSet entities;
            public MemAllocatorPtr data;
            
            public SharedComponentStorageUnknown(State* state, void* data, uint dataSize) {
                this = default;
                this.data.Set(ref state->allocator, data, dataSize);
                this.entities = new UIntHashSet(ref state->allocator, 1u);
            }

            public void Dispose(ref MemoryAllocator allocator) {
                this.entities.Dispose(ref allocator);
                this = default;
            }

        }

        // hash => SharedComponentStorage<T>
        internal UIntDictionary<MemAllocatorPtr> sharedData;
        // entityId => [typeId => hash]
        internal MemArray<MemArray<uint>> entityIdToHash;
        public int lockSharedIndex;

        [INLINE(256)]
        private Components InitializeSharedComponents(State* state, in StateProperties stateProperties) {

            this.sharedData = new UIntDictionary<MemAllocatorPtr>(ref state->allocator, stateProperties.sharedComponentsCapacity);
            this.entityIdToHash = new MemArray<MemArray<uint>>(ref state->allocator, stateProperties.entitiesCapacity);

            return this;

        }

        [INLINE(256)]
        private static uint GetStoredSharedHash<T>(State* state, in Components components, uint entId) where T : unmanaged, IComponentShared {

            if (entId >= components.entityIdToHash.Length) return Components.COMPONENT_SHARED_DEFAULT_HASH;
            var typeId = StaticTypes<T>.sharedTypeId; 
            ref var arr = ref components.entityIdToHash[state, entId];
            if (typeId >= arr.Length) return Components.COMPONENT_SHARED_DEFAULT_HASH;
            return arr[state, typeId];

        }

        [INLINE(256)]
        private static uint GetSharedHash<T>(in T data, State* state, in Components components, uint entId, uint hash) where T : unmanaged, IComponentShared {

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
        private static void SetSharedHash(State* state, ref Components components, uint entId, uint typeId, uint hash) {
            
            if (entId >= components.entityIdToHash.Length) components.entityIdToHash.Resize(ref state->allocator, entId + 1u);
            ref var typeIdToHash = ref components.entityIdToHash[state, entId];
            if (typeId >= typeIdToHash.Length) typeIdToHash.Resize(ref state->allocator, typeId + 1u);
            typeIdToHash[state, typeId] = hash;

        }

        [INLINE(256)]
        public bool SetShared<T>(State* state, uint entId, in T data, uint hash = 0u) where T : unmanaged, IComponentShared {

            var typeId = StaticTypes<T>.typeId;
            // No custom hash provided - use data hash
            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) {
                hash = GetDataSharedHash(in data);
            }

            var rData = data;
            return this.SetShared(state, entId, StaticTypes<T>.groupId, _address(ref rData), TSize<T>.size, StaticTypes<T>.sharedTypeId, hash);
            
        }

        [INLINE(256)]
        public bool SetShared(State* state, uint entId, uint groupId, void* data, uint dataSize, uint sharedTypeId, uint hash) {

            // No custom hash provided - use data hash
            if (hash == Components.COMPONENT_SHARED_DEFAULT_HASH) {
                // use typeId as hash
                hash = sharedTypeId;
            }
            
            // get shared storage for component by hash
            ref var ptr = ref this.sharedData.GetValue(ref state->allocator, hash, out var exist);
            if (exist == false) ptr.Set(ref state->allocator, new SharedComponentStorageUnknown(state, data, dataSize));

            // update data in storage
            ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state->allocator);
            var dataMemPtr = storage.data.ptr;
            var dataPtr = MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in dataMemPtr);
            if (dataSize > 0u) _memcpy(data, dataPtr, dataSize);
            var added = storage.entities.Add(ref state->allocator, entId);
            
            // update indexer
            SetSharedHash(state, ref this, entId, sharedTypeId, hash);
            
            if (added == true) state->entities.UpVersion(state, entId, groupId);
            return added;

        }

        [INLINE(256)]
        public void ClearShared(State* state, uint entId) {
            
            if (entId >= this.entityIdToHash.Length) return;
            
            ref var typeIdToHash = ref this.entityIdToHash[state, entId];
            for (uint i = 0; i < typeIdToHash.Length; ++i) {
                
                var hash = typeIdToHash[state, i];
                if (this.sharedData.TryGetValue(in state->allocator, hash, out var ptr) == false) {
                    continue;
                }
            
                // get data from storage
                ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state->allocator);
                var exist = storage.entities.Remove(ref state->allocator, entId);
                if (exist == true && storage.entities.Count == 0u) {
                    // remove data from storage
                    storage.Dispose(ref state->allocator);
                    ptr.Dispose(ref state->allocator);
                    this.sharedData.Remove(in state->allocator, hash);
                }

            }
            
        }

        [INLINE(256)]
        public bool RemoveShared<T>(State* state, uint entId, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (entId >= this.entityIdToHash.Length) return false;
            
            hash = GetSharedHash(default(T), state, in this, entId, hash);
            
            // get shared storage for component by hash
            if (this.sharedData.TryGetValue(in state->allocator, hash, out var ptr) == false) {
                return false;
            }
            
            // get data from storage
            ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state->allocator);
            var exist = storage.entities.Remove(ref state->allocator, entId);
            if (exist == true && storage.entities.Count == 0u) {
                // remove data from storage
                storage.Dispose(ref state->allocator);
                ptr.Dispose(ref state->allocator);
                this.sharedData.Remove(in state->allocator, hash);
            }
            
            if (exist == true) state->entities.UpVersion<T>(state, entId);
            return exist;

        }

        [INLINE(256)]
        public readonly ref readonly T ReadShared<T>(State* state, uint entId, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (entId >= this.entityIdToHash.Length) return ref StaticTypes<T>.defaultValue;
            hash = GetSharedHash(default(T), state, in this, entId, hash);

            if (this.sharedData.TryGetValue(in state->allocator, hash, out var ptr) == true) {
                ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state->allocator);
                if (storage.entities.Contains(in state->allocator, entId) == false) return ref StaticTypes<T>.defaultValue;
                return ref *storage.data.AsPtr<T>(in state->allocator);
            } else {
                return ref StaticTypes<T>.defaultValue;
            }

        }

        [INLINE(256)]
        public ref T GetShared<T>(State* state, uint entId, uint hash, out bool isNew) where T : unmanaged, IComponentShared {

            isNew = false;
            if (entId >= this.entityIdToHash.Length) this.entityIdToHash.Resize(ref state->allocator, entId + 1u);
            hash = GetSharedHash(default(T), state, in this, entId, hash);
            
            // get shared storage for component by hash
            ref var ptr = ref this.sharedData.GetValue(ref state->allocator, hash, out var exist);
            if (exist == false) ptr.Set(ref state->allocator, new SharedComponentStorageUnknown(state, null, TSize<T>.size));

            // get data from storage
            ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state->allocator);
            if (storage.entities.Add(ref state->allocator, entId) == true) {
                isNew = true;
            }
            
            state->entities.UpVersion<T>(state, entId);
            return ref *storage.data.AsPtr<T>(in state->allocator);
            
        }

        [INLINE(256)]
        public readonly bool HasShared<T>(State* state, uint entId, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (entId >= this.entityIdToHash.Length) return false;
            hash = GetSharedHash(default(T), state, in this, entId, hash);

            if (this.sharedData.TryGetValue(in state->allocator, hash, out var ptr) == true) {
                
                ref var storage = ref ptr.As<SharedComponentStorageUnknown>(in state->allocator);
                return storage.entities.Contains(in state->allocator, entId);

            }

            return false;

        }

        public bool HasSharedDirect<T>(Ent ent) where T : unmanaged, IComponentShared {

            return this.HasShared<T>(ent.World.state, ent.id);

        }

        public T ReadSharedDirect<T>(Ent ent) where T : unmanaged, IComponentShared {

            return this.ReadShared<T>(ent.World.state, ent.id);

        }

        public void SetSharedDirect<T>(Ent ent, T data) where T : unmanaged, IComponentShared {

            this.SetShared(ent.World.state, ent.id, in data);

        }

    }

}