namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe partial struct Components {

        #if ENABLE_BECS_FLAT_QUIERIES
        [INLINE(256)]
        public static void CleanUpEntity(safe_ptr<State> state, in Ent ent) {

            ref var components = ref state.ptr->entities.entityToComponents[in state.ptr->allocator, ent.id];
            var e = components.GetEnumerator(state);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                ref var ptr = ref state.ptr->components.items[state, typeId];
                ref var storage = ref ptr.As<DataDenseSet>(in state.ptr->allocator);
                storage.CleanUpEntity(state, ent.id, typeId);
            }
            components.Dispose(ref state.ptr->allocator);

        }
        #endif

    }

}