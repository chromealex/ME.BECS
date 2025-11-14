namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe partial struct Components {

        #if ENABLE_BECS_FLAT_QUERIES
        [INLINE(256)]
        public static void CleanUpEntity(safe_ptr<State> state, in Ent ent) {

            ref var components = ref state.ptr->entities.entityToComponents[in state.ptr->allocator, ent.id];
            components.lockSpinner.Lock();
            var e = components.entities.GetEnumerator(state);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                var ptr = state.ptr->components.items.GetUnsafePtr(state, typeId);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                storage.ptr->CleanUpEntity(state, ent.id, typeId);
            }
            components.entities.Dispose(ref state.ptr->allocator);
            components.lockSpinner.Unlock();

        }
        #endif

    }

}