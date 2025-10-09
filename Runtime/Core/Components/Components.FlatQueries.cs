namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe partial struct Components {

        #if ENABLE_BECS_FLAT_QUIERIES
        [INLINE(256)]
        public static void CleanUpEntity(safe_ptr<State> state, in Ent ent) {

            var components = state.ptr->entities.entityToComponents[in state.ptr->allocator, ent.id];
            for (uint i = 0u; i < components.Count; ++i) {
                var typeId = components[in state.ptr->allocator, i];
                ref var ptr = ref state.ptr->components.items[state, typeId];
                ref var storage = ref ptr.As<DataDenseSet>(in state.ptr->allocator);
                storage.CleanUpEntity(state, ent.id);
            }

        }
        #endif

    }

}