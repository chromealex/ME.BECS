
namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe struct CollectionsRegistry {

        private MemArray<List<MemPtr>> list;
        private ReadWriteSpinner readWriteSpinner;

        [INLINE(256)]
        public static CollectionsRegistry Create(State* state, uint capacity) {

            return new CollectionsRegistry() {
                list = new MemArray<List<MemPtr>>(ref state->allocator, capacity, growFactor: 2),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };

        }

        [INLINE(256)]
        public void OnEntityAdd(State* state, uint entId) {
            
            if (entId >= this.list.Length) {
                this.readWriteSpinner.WriteBegin(state);
                this.list.Resize(ref state->allocator, entId + 1u);
                this.readWriteSpinner.WriteEnd();
            }
            
        }

        [INLINE(256)]
        public void Destroy(State* state, in Ent ent) {

            this.readWriteSpinner.ReadBegin(state);
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.isCreated == true) {
                for (uint i = 0; i < list.Count; ++i) {
                    state->allocator.Free(in list[in state->allocator, i]);
                }

                list.Clear();
            }
            this.readWriteSpinner.ReadEnd(state);
            
        }
        
        [INLINE(256)]
        public void Add(State* state, in Ent ent, in MemPtr ptr) {
            
            this.readWriteSpinner.ReadBegin(state);
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.isCreated == false) list = new List<MemPtr>(ref state->allocator, 1u);
            list.Add(ref state->allocator, ptr);
            this.readWriteSpinner.ReadEnd(state);
            
        }

        [INLINE(256)]
        public void Remove(State* state, in Ent ent, in MemPtr ptr) {
            
            this.readWriteSpinner.ReadBegin(state);
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.isCreated == true) list.Remove(ref state->allocator, ptr);
            this.readWriteSpinner.ReadEnd(state);
            
        }

    }
    
}