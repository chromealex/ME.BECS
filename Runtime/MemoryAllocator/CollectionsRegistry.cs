
namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe struct CollectionsRegistry {

        private MemArray<List<MemPtr>> list;
        private ReadWriteSpinner readWriteSpinner;
        private MemArray<LockSpinner> readWriteSpinnerPerEntity;

        [INLINE(256)]
        public static CollectionsRegistry Create(State* state, uint capacity) {

            return new CollectionsRegistry() {
                list = new MemArray<List<MemPtr>>(ref state->allocator, capacity),
                readWriteSpinnerPerEntity = new MemArray<LockSpinner>(ref state->allocator, capacity),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };

        }

        [INLINE(256)]
        public static void OnEntityAdd(State* state, uint entId) {
            
            if (entId >= state->collectionsRegistry.list.Length) {
                state->collectionsRegistry.readWriteSpinner.WriteBegin(state);
                if (entId >= state->collectionsRegistry.list.Length) {
                    state->collectionsRegistry.list.Resize(ref state->allocator, entId + 1u, 2);
                    state->collectionsRegistry.readWriteSpinnerPerEntity.Resize(ref state->allocator, entId + 1u, 2);
                }
                state->collectionsRegistry.readWriteSpinner.WriteEnd();
            }
            
        }

        [INLINE(256)]
        public static void Destroy(State* state, in Ent ent) {

            state->collectionsRegistry.readWriteSpinner.ReadBegin(state);
            ref var list = ref state->collectionsRegistry.list[in state->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref state->collectionsRegistry.readWriteSpinnerPerEntity[in state->allocator, ent.id];
                entitySpinner.Lock();
                if (list.IsCreated == true) {
                    for (uint i = 0; i < list.Count; ++i) {
                        state->allocator.Free(in list[in state->allocator, i]);
                    }

                    list.Clear();
                }
                entitySpinner.Unlock();
            }
            state->collectionsRegistry.readWriteSpinner.ReadEnd(state);
            
        }
        
        [INLINE(256)]
        public static void Add(State* state, in Ent ent, in MemPtr ptr) {
            
            state->collectionsRegistry.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref state->collectionsRegistry.readWriteSpinnerPerEntity[in state->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref state->collectionsRegistry.list[in state->allocator, ent.id];
            if (list.IsCreated == false) list = new List<MemPtr>(ref state->allocator, 1u);
            list.Add(ref state->allocator, ptr);
            entitySpinner.Unlock();
            state->collectionsRegistry.readWriteSpinner.ReadEnd(state);
            
        }

        [INLINE(256)]
        public static void Remove(State* state, in Ent ent, in MemPtr ptr) {
            
            state->collectionsRegistry.readWriteSpinner.ReadBegin(state);
            ref var list = ref state->collectionsRegistry.list[in state->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref state->collectionsRegistry.readWriteSpinnerPerEntity[in state->allocator, ent.id];
                entitySpinner.Lock();
                list.Remove(ref state->allocator, ptr);
                entitySpinner.Unlock();
            }
            state->collectionsRegistry.readWriteSpinner.ReadEnd(state);
            
        }

        public static uint GetReservedSizeInBytes(State* state) {

            var size = TSize<CollectionsRegistry>.size;
            for (uint i = 0u; i < state->collectionsRegistry.list.Length; ++i) {
                var item = state->collectionsRegistry.list[state, i];
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