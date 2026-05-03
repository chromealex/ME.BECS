namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct Ents {

        #if ENABLE_BECS_FLAT_QUERIES
        public struct LockedEntityToComponent {

            public const uint DEFAULT_CAPACITY = 8u;

            public LockSpinner lockSpinner;
            public HashSet<uint> entities;
            
            public LockedEntityToComponent(ref MemoryAllocator allocator, uint capacity) {
                this.lockSpinner = default;
                this.entities = new HashSet<uint>(ref allocator, capacity);
            }

            [INLINE(256)]
            public void Clear(safe_ptr<State> state, uint capacity) {
                this.lockSpinner.Lock();
                if (this.entities.IsCreated == false) {
                    this.entities = new HashSet<uint>(ref state.ptr->allocator, capacity);
                }
                this.entities.Clear(ref state.ptr->allocator);
                this.lockSpinner.Unlock();
            }

        }
        public MemArray<LockedEntityToComponent> entityToComponents;
        
        [INLINE(256)]
        public void OnAddComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.lockSpinner.Lock();
            using (new AllocatorTag(ALLOC_TAGS.ENTITIES)) {
                list.entities.Add(ref state.ptr->allocator, typeId);
            }
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

        [INLINE(256)]
        public void SerializeHeadersFlatQueries(ref StreamBufferWriter writer) {
            #if ENABLE_BECS_FLAT_QUERIES
            writer.Write(this.entityToComponents);
            #endif
        }

        [INLINE(256)]
        public void DeserializeHeadersFlatQueries(ref StreamBufferReader reader) {
            #if ENABLE_BECS_FLAT_QUERIES
            reader.Read(ref this.entityToComponents);
            #endif
        }

    }
    
    public unsafe partial struct EntsOld {

        #if ENABLE_BECS_FLAT_QUERIES
        public struct LockedEntityToComponent {

            public LockSpinner lockSpinner;
            public HashSet<uint> entities;
            
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

        [INLINE(256)]
        public void SerializeHeadersFlatQueries(ref StreamBufferWriter writer) {
            #if ENABLE_BECS_FLAT_QUERIES
            writer.Write(this.entityToComponents);
            #endif
        }

        [INLINE(256)]
        public void DeserializeHeadersFlatQueries(ref StreamBufferReader reader) {
            #if ENABLE_BECS_FLAT_QUERIES
            reader.Read(ref this.entityToComponents);
            #endif
        }

    }

}