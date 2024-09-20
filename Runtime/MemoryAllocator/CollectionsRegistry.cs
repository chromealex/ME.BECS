
namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe struct CollectionsRegistry {

        private MemArray<List<MemPtr>> list;
        private ReadWriteSpinner readWriteSpinner;
        private MemArray<LockSpinner> readWriteSpinnerPerEntity;

        [INLINE(256)]
        public static CollectionsRegistry Create(State* state, uint capacity) {

            return new CollectionsRegistry() {
                list = new MemArray<List<MemPtr>>(ref state->allocator, capacity, growFactor: 2),
                readWriteSpinnerPerEntity = new MemArray<LockSpinner>(ref state->allocator, capacity, growFactor: 2),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };

        }

        [INLINE(256)]
        public void OnEntityAdd(State* state, uint entId) {
            
            if (entId >= this.list.Length) {
                this.readWriteSpinner.WriteBegin(state);
                this.list.Resize(ref state->allocator, entId + 1u);
                this.readWriteSpinnerPerEntity.Resize(ref state->allocator, entId + 1u);
                this.readWriteSpinner.WriteEnd();
            }
            
        }

        [INLINE(256)]
        public void Destroy(State* state, in Ent ent) {

            this.readWriteSpinner.ReadBegin(state);
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref this.readWriteSpinnerPerEntity[in state->allocator, ent.id];
                entitySpinner.Lock();
                if (list.IsCreated == true) {
                    for (uint i = 0; i < list.Count; ++i) {
                        state->allocator.Free(in list[in state->allocator, i]);
                    }

                    list.Clear();
                }
                entitySpinner.Unlock();
            }
            this.readWriteSpinner.ReadEnd(state);
            
        }
        
        [INLINE(256)]
        public void Add(State* state, in Ent ent, in MemPtr ptr) {
            
            this.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref this.readWriteSpinnerPerEntity[in state->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.IsCreated == false) list = new List<MemPtr>(ref state->allocator, 1u);
            list.Add(ref state->allocator, ptr);
            entitySpinner.Unlock();
            this.readWriteSpinner.ReadEnd(state);
            
        }

        [INLINE(256)]
        public void Remove(State* state, in Ent ent, in MemPtr ptr) {
            
            this.readWriteSpinner.ReadBegin(state);
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref this.readWriteSpinnerPerEntity[in state->allocator, ent.id];
                entitySpinner.Lock();
                list.Remove(ref state->allocator, ptr);
                entitySpinner.Unlock();
            }
            this.readWriteSpinner.ReadEnd(state);
            
        }

        public uint GetReservedSizeInBytes(State* state) {

            var size = 0u;
            for (uint i = 0u; i < this.list.Length; ++i) {
                var item = this.list[state, i];
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