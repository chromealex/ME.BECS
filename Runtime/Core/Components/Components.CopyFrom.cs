namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    /// <summary>
    /// Defines components data used by entity processing.
    /// </summary>
    public unsafe partial struct Components {

        /// <summary>
        /// Copies the supplied source state into this components instance.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void CopyFrom(safe_ptr<State> sourceState, in Ent ent, safe_ptr<State> targetState, in Ent targetEnt) {

            ref var listLock = ref sourceState.ptr->entities.GetEntityComponentsLock(sourceState, ent.id);
            listLock.Lock();
            var e = sourceState.ptr->entities.GetEntityComponentsEnumerator(sourceState, ent.id);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            listLock.Unlock();
            
        }

        /// <summary>
        /// Copies the supplied source state into this components instance.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void CopyFrom<TIgnore0>(safe_ptr<State> sourceState, in Ent ent, safe_ptr<State> targetState, in Ent targetEnt) where TIgnore0 : unmanaged, IComponent {

            var ignore0 = StaticTypes<TIgnore0>.typeId;
            ref var listLock = ref sourceState.ptr->entities.GetEntityComponentsLock(sourceState, ent.id);
            listLock.Lock();
            var e = sourceState.ptr->entities.GetEntityComponentsEnumerator(sourceState, ent.id);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                if (ignore0 == typeId) continue;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            listLock.Unlock();
            
        }

        /// <summary>
        /// Copies the supplied source state into this components instance.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void CopyFrom<TIgnore0, TIgnore1>(safe_ptr<State> sourceState, in Ent ent, safe_ptr<State> targetState, in Ent targetEnt) where TIgnore0 : unmanaged, IComponent where TIgnore1 : unmanaged, IComponent {

            var ignore0 = StaticTypes<TIgnore0>.typeId;
            var ignore1 = StaticTypes<TIgnore1>.typeId;
            ref var listLock = ref sourceState.ptr->entities.GetEntityComponentsLock(sourceState, ent.id);
            listLock.Lock();
            var e = sourceState.ptr->entities.GetEntityComponentsEnumerator(sourceState, ent.id);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                if (ignore0 == typeId || ignore1 == typeId) continue;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            listLock.Unlock();
            
        }

        [INLINE(256)]
        private static void CopyFrom_INTERNAL(safe_ptr<State> sourceState, in Ent ent, safe_ptr<State> targetState, in Ent targetEnt, uint typeId) {
            var groupId = StaticTypes.tracker.Get(typeId);
            var ptr = sourceState.ptr->components.items.GetUnsafePtr(in sourceState.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in sourceState.ptr->allocator);
            var data = storage.ptr->Read(sourceState, ent.id, ent.gen, out _);
            if (StaticTypesAutoDestroy.Is(typeId) == true) {
                AutoDestroyRegistry.Destroy(targetState, in targetEnt, typeId);
                AutoDestroyRegistry.Add(targetState, in targetEnt, typeId);
            }
            if (Components.SetUnknownType(targetState, typeId, groupId, in targetEnt, data) == true) {
                Batches.Set_INTERNAL(typeId, in targetEnt);
            }
            //WorldStaticCallbacks.RaiseCopyFromComponentCallback(typeId, data, in targetEnt);
        }

    }

}
