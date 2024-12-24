
namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe struct CollectionsRegistry {

        private MemArray<List<MemPtr>> list;
        private ReadWriteSpinner readWriteSpinner;
        private MemArray<LockSpinner> readWriteSpinnerPerEntity;

        [INLINE(256)]
        public static CollectionsRegistry Create(safe_ptr<State> state, uint capacity) {

            return new CollectionsRegistry() {
                list = new MemArray<List<MemPtr>>(ref state.ptr->allocator, capacity),
                readWriteSpinnerPerEntity = new MemArray<LockSpinner>(ref state.ptr->allocator, capacity),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };

        }

        [INLINE(256)]
        public static void OnEntityAdd(safe_ptr<State> state, uint entId) {
            
            if (entId >= state.ptr->collectionsRegistry.list.Length) {
                state.ptr->collectionsRegistry.readWriteSpinner.WriteBegin(state);
                if (entId >= state.ptr->collectionsRegistry.list.Length) {
                    state.ptr->collectionsRegistry.list.Resize(ref state.ptr->allocator, entId + 1u, 2);
                    state.ptr->collectionsRegistry.readWriteSpinnerPerEntity.Resize(ref state.ptr->allocator, entId + 1u, 2);
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
                    for (uint i = 0; i < list.Count; ++i) {
                        state.ptr->allocator.Free(in list[in state.ptr->allocator, i]);
                    }

                    list.Clear();
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
            if (list.IsCreated == false) list = new List<MemPtr>(ref state.ptr->allocator, 1u);
            list.Add(ref state.ptr->allocator, ptr);
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
                list.Remove(ref state.ptr->allocator, ptr);
                entitySpinner.Unlock();
            }
            state.ptr->collectionsRegistry.readWriteSpinner.ReadEnd(state);
            
        }

        public static uint GetReservedSizeInBytes(safe_ptr<State> state) {

            var size = TSize<CollectionsRegistry>.size;
            for (uint i = 0u; i < state.ptr->collectionsRegistry.list.Length; ++i) {
                var item = state.ptr->collectionsRegistry.list[state, i];
                size += item.GetReservedSizeInBytes();
                for (uint j = 0u; j < item.Count; ++j) {
                    var obj = item[state, j];
                    size += obj.GetSizeInBytes(state);
                }
            }
            return size;

        }

    }
    
}