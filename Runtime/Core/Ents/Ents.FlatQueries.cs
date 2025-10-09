namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct Ents {

        #if ENABLE_BECS_FLAT_QUIERIES
        public MemArray<List<uint>> entityToComponents;
        
        [INLINE(256)]
        public void OnAddComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.Add(ref state.ptr->allocator, typeId);
        }

        [INLINE(256)]
        public void OnRemoveComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.Remove(ref state.ptr->allocator, typeId);
        }
        #endif

    }

}