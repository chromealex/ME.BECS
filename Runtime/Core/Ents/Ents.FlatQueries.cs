namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct Ents {

        #if ENABLE_BECS_FLAT_QUIERIES
        public struct LockedEntityToComponent {

            public HashSet<uint> entities;
            public LockSpinner lockSpinner;
            
            public LockedEntityToComponent(ref MemoryAllocator allocator, uint capacity) {
                this.lockSpinner = default;
                this.entities = new HashSet<uint>(ref allocator, capacity);
            }

        }
        public MemArray<LockedEntityToComponent> entityToComponents;
        
        [INLINE(256)]
        public void OnAddComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.lockSpinner.Lock();
            list.entities.Add(ref state.ptr->allocator, typeId);
            list.lockSpinner.Unlock();
        }

        [INLINE(256)]
        public void OnRemoveComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.lockSpinner.Lock();
            list.entities.Remove(ref state.ptr->allocator, typeId);
            list.lockSpinner.Unlock();
        }
        #endif

    }

}