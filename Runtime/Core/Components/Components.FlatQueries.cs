namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    public unsafe partial struct Components {

        #if ENABLE_BECS_FLAT_QUERIES
        [INLINE(256)]
        public static void CleanUpEntity(safe_ptr<State> state, in Ent ent) {

            ref var spinner = ref state.ptr->entities.GetEntityComponentsLock(state, ent.id);
            spinner.Lock();
            var e = state.ptr->entities.GetEntityComponentsEnumerator(state, ent.id);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                var ptr = state.ptr->components.items.GetUnsafePtr(state, typeId);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                storage.ptr->CleanUpEntity(state, ent.id, typeId);
            }
            Cuts._memclear(state.ptr->entities.GetEntityComponentsWords(state, ent.id), state.ptr->entities.entityToComponentsWords * sizeof(ulong));
            spinner.Unlock();

        }
        #endif

    }

}
