
namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct CollectionsRegistry {

        private struct EntityCollectionPtrs {

            private MemPtr storageOrFirst;
            private uint count;
            private uint capacity;

            public bool IsCreated => this.count > 0u;
            public uint Count => this.count;

            [INLINE(256)]
            public void Add(ref MemoryAllocator allocator, in MemPtr ptr) {
                if (this.count == 0u) {
                    this.storageOrFirst = ptr;
                    this.count = 1u;
                    return;
                }
                if (this.count == 1u) {
                    var first = this.storageOrFirst;
                    this.capacity = 2u;
                    this.storageOrFirst = allocator.AllocArray<MemPtr>(this.capacity, out var items);
                    items[0u] = first;
                    items[1u] = ptr;
                    this.count = 2u;
                    return;
                }
                if (this.count == this.capacity) {
                    this.capacity *= 2u;
                    this.storageOrFirst = allocator.ReAllocArray<MemPtr>(this.storageOrFirst, this.capacity, out _);
                }
                var arr = (safe_ptr<MemPtr>)allocator.GetUnsafePtr(this.storageOrFirst);
                arr[this.count++] = ptr;
            }

            [INLINE(256)]
            public bool Remove(ref MemoryAllocator allocator, in MemPtr ptr) {
                if (this.count == 0u) return false;
                if (this.count == 1u) {
                    if (this.storageOrFirst != ptr) return false;
                    this = default;
                    return true;
                }
                var arr = (safe_ptr<MemPtr>)allocator.GetUnsafePtr(this.storageOrFirst);
                for (uint i = 0u; i < this.count; ++i) {
                    if (arr[i] != ptr) continue;
                    --this.count;
                    if (this.count == 1u) {
                        var remaining = arr[i == 0u ? 1u : 0u];
                        allocator.Free(this.storageOrFirst);
                        this.storageOrFirst = remaining;
                        this.capacity = 0u;
                    } else {
                        arr[i] = arr[this.count];
                    }
                    return true;
                }
                return false;
            }

            [INLINE(256)]
            public void Destroy(safe_ptr<State> state) {
                if (this.count == 0u) return;
                if (this.count == 1u) {
                    state.ptr->allocator.Free(this.storageOrFirst);
                    this = default;
                    return;
                }
                var arr = (safe_ptr<MemPtr>)state.ptr->allocator.GetUnsafePtr(this.storageOrFirst);
                for (uint i = 0u; i < this.count; ++i) state.ptr->allocator.Free(arr[i]);
                state.ptr->allocator.Free(this.storageOrFirst);
                this = default;
            }

            public uint GetReservedSizeInBytes(safe_ptr<State> state) {
                var size = this.count > 1u ? this.storageOrFirst.GetSizeInBytes(state) : 0u;
                if (this.count == 1u) return size + this.storageOrFirst.GetSizeInBytes(state);
                if (this.count > 1u) {
                    var arr = (safe_ptr<MemPtr>)state.ptr->allocator.GetUnsafePtr(this.storageOrFirst);
                    for (uint i = 0u; i < this.count; ++i) size += arr[i].GetSizeInBytes(state);
                }
                return size;
            }

        }

        private MemArray<EntityCollectionPtrs> list;
        private ReadWriteSpinner readWriteSpinner;
        private MemArray<LockSpinner> readWriteSpinnerPerEntity;

        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.list);
            writer.Write(this.readWriteSpinner);
            writer.Write(this.readWriteSpinnerPerEntity);
        }

        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.list);
            reader.Read(ref this.readWriteSpinner);
            reader.Read(ref this.readWriteSpinnerPerEntity);
        }

        [INLINE(256)]
        public static CollectionsRegistry Create(safe_ptr<State> state, uint capacity) {

            using (new AllocatorTag(ALLOC_TAGS.COLLECTIONS)) {
                return new CollectionsRegistry() {
                    list = new MemArray<EntityCollectionPtrs>(ref state.ptr->allocator, capacity),
                    readWriteSpinnerPerEntity = new MemArray<LockSpinner>(ref state.ptr->allocator, capacity),
                    readWriteSpinner = ReadWriteSpinner.Create(state),
                };
            }

        }

        [INLINE(256)]
        public static void OnEntityAdd(safe_ptr<State> state, uint entId) {
            
            if (entId >= state.ptr->collectionsRegistry.list.Length) {
                state.ptr->collectionsRegistry.readWriteSpinner.WriteBegin(state);
                if (entId >= state.ptr->collectionsRegistry.list.Length) {
                    using (new AllocatorTag(ALLOC_TAGS.COLLECTIONS)) {
                        state.ptr->collectionsRegistry.list.Resize(ref state.ptr->allocator, entId + 1u, 2);
                        state.ptr->collectionsRegistry.readWriteSpinnerPerEntity.Resize(ref state.ptr->allocator, entId + 1u, 2);
                    }
                }
                state.ptr->collectionsRegistry.readWriteSpinner.WriteEnd();
            }
            
        }

        [INLINE(256)]
        public static void Destroy(safe_ptr<State> state, in Ent ent) {

            state.ptr->collectionsRegistry.readWriteSpinner.ReadBegin(state);
            ref var list = ref state.ptr->collectionsRegistry.list[in state.ptr->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref state.ptr->collectionsRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
                entitySpinner.Lock();
                if (list.IsCreated == true) {
                    list.Destroy(state);
                }
                entitySpinner.Unlock();
            }
            state.ptr->collectionsRegistry.readWriteSpinner.ReadEnd(state);
            
        }
        
        [INLINE(256)]
        public static void Add(safe_ptr<State> state, in Ent ent, in MemPtr ptr) {
            
            state.ptr->collectionsRegistry.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref state.ptr->collectionsRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref state.ptr->collectionsRegistry.list[in state.ptr->allocator, ent.id];
            using (new AllocatorTag(ALLOC_TAGS.COLLECTIONS)) {
                list.Add(ref state.ptr->allocator, in ptr);
            }
            entitySpinner.Unlock();
            state.ptr->collectionsRegistry.readWriteSpinner.ReadEnd(state);
            
        }

        [INLINE(256)]
        public static void Remove(safe_ptr<State> state, in Ent ent, in MemPtr ptr) {
            
            state.ptr->collectionsRegistry.readWriteSpinner.ReadBegin(state);
            ref var list = ref state.ptr->collectionsRegistry.list[in state.ptr->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref state.ptr->collectionsRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
                entitySpinner.Lock();
                list.Remove(ref state.ptr->allocator, in ptr);
                entitySpinner.Unlock();
            }
            state.ptr->collectionsRegistry.readWriteSpinner.ReadEnd(state);
            
        }

        public static uint GetReservedSizeInBytes(safe_ptr<State> state) {

            var size = TSize<CollectionsRegistry>.size;
            for (uint i = 0u; i < state.ptr->collectionsRegistry.list.Length; ++i) {
                var item = state.ptr->collectionsRegistry.list[state, i];
                size += item.GetReservedSizeInBytes(state);
            }
            return size;

        }

    }
    
}
