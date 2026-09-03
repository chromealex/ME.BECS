
namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using static Cuts;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct AutoDestroyRegistry {

        public delegate void DestroyDelegate(in Ent ent, byte* comp);

        #if !ENABLE_BECS_FLAT_QUERIES
        private MemArray<List<uint>> list;
        private ReadWriteSpinner readWriteSpinner;
        private MemArray<LockSpinner> readWriteSpinnerPerEntity;
        #endif

        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            #if !ENABLE_BECS_FLAT_QUERIES
            writer.Write(this.list);
            writer.Write(this.readWriteSpinner);
            writer.Write(this.readWriteSpinnerPerEntity);
            #endif
        }

        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            #if !ENABLE_BECS_FLAT_QUERIES
            reader.Read(ref this.list);
            reader.Read(ref this.readWriteSpinner);
            reader.Read(ref this.readWriteSpinnerPerEntity);
            #endif
        }

        [INLINE(256)]
        public static AutoDestroyRegistry Create(safe_ptr<State> state, uint capacity) {

            #if ENABLE_BECS_FLAT_QUERIES
            return default;
            #else
            using (new AllocatorTag(ALLOC_TAGS.AUTO_DESTROY)) {
                return new AutoDestroyRegistry() {
                    list = new MemArray<List<uint>>(ref state.ptr->allocator, capacity),
                    readWriteSpinnerPerEntity = new MemArray<LockSpinner>(ref state.ptr->allocator, capacity),
                    readWriteSpinner = ReadWriteSpinner.Create(state),
                };
            }
            #endif

        }

        [INLINE(256)]
        public static void OnEntityAdd(safe_ptr<State> state, uint entId) {

            #if !ENABLE_BECS_FLAT_QUERIES
            if (entId >= state.ptr->autoDestroyRegistry.list.Length) {
                state.ptr->autoDestroyRegistry.readWriteSpinner.WriteBegin(state);
                using (new AllocatorTag(ALLOC_TAGS.AUTO_DESTROY)) {
                    state.ptr->autoDestroyRegistry.list.Resize(ref state.ptr->allocator, entId + 1u, 2);
                    state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity.Resize(ref state.ptr->allocator, entId + 1u, 2);
                }
                state.ptr->autoDestroyRegistry.readWriteSpinner.WriteEnd();
            }
            #endif
            
        }

        [INLINE(256)]
        public static void Destroy(safe_ptr<State> state, in Ent ent) {

            #if ENABLE_BECS_FLAT_QUERIES
            for (uint typeId = 1u; typeId < StaticTypesAutoDestroy.registry.Data.Length; ++typeId) {
                if (StaticTypesAutoDestroy.Is(typeId) == false) continue;
                Invoke(state, in ent, typeId);
            }
            #else
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var list = ref state.ptr->autoDestroyRegistry.list[in state.ptr->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
                entitySpinner.Lock();
                if (list.IsCreated == true) {
                    for (uint i = 0; i < list.Count; ++i) {
                        var typeId = list[in state.ptr->allocator, i];
                        byte* comp = null;
                        var exists = true;
                        if (StaticTypes.sizes.Get(typeId) > 0) {
                            comp = Components.ReadUnknownType(state, typeId, ent.id, ent.gen, out exists);
                        } else {
                            exists = Components.HasUnknownType(state, typeId, ent.id, ent.gen, false);
                        }
                        if (exists == true) {
                            // component exists - call destroy method
                            var func = StaticTypesDestroyRegistry.registry.Data.Get(typeId);
                            func.Invoke(ent, comp);
                        }
                    }

                    list.Clear();
                }
                entitySpinner.Unlock();
            }
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            #endif
            
        }
        
        [INLINE(256)]
        public static void Destroy(safe_ptr<State> state, in Ent ent, uint typeId) {

            if (Invoke(state, in ent, typeId) == true) {
                #if !ENABLE_BECS_FLAT_QUERIES
                // clean up list
                state.ptr->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
                ref var list = ref state.ptr->autoDestroyRegistry.list[in state.ptr->allocator, ent.id];
                if (list.IsCreated == true) {
                    ref var entitySpinner = ref state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
                    entitySpinner.Lock();
                    if (list.IsCreated == true) {
                        list.Remove(ref state.ptr->allocator, typeId);
                    }
                    entitySpinner.Unlock();
                }
                state.ptr->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
                #endif
            }

        }

        [INLINE(256)]
        private static bool Invoke(safe_ptr<State> state, in Ent ent, uint typeId) {

            byte* comp = null;
            var exists = true;
            if (StaticTypes.sizes.Get(typeId) > 0) {
                comp = Components.ReadUnknownType(state, typeId, ent.id, ent.gen, out exists);
            } else {
                exists = Components.HasUnknownType(state, typeId, ent.id, ent.gen, false);
            }
            if (exists == true) {
                // component exists - call destroy method
                var func = StaticTypesDestroyRegistry.registry.Data.Get(typeId);
                func.Invoke(ent, comp);
            }

            return exists;

        }

        [INLINE(256)]
        public static void Add(safe_ptr<State> state, in Ent ent, uint typeId) {

            #if !ENABLE_BECS_FLAT_QUERIES
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
            entitySpinner.Lock();
            using (new AllocatorTag(ALLOC_TAGS.AUTO_DESTROY)) {
                ref var list = ref state.ptr->autoDestroyRegistry.list[in state.ptr->allocator, ent.id];
                if (list.IsCreated == false) list = new List<uint>(ref state.ptr->allocator, 1u);
                list.Add(ref state.ptr->allocator, typeId);
            }
            entitySpinner.Unlock();
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            #endif
            
        }

        [INLINE(256)]
        public static void Remove(safe_ptr<State> state, in Ent ent, uint typeId) {

            #if !ENABLE_BECS_FLAT_QUERIES
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref state.ptr->autoDestroyRegistry.list[in state.ptr->allocator, ent.id];
            if (list.IsCreated == true) list.Remove(ref state.ptr->allocator, typeId);
            entitySpinner.Unlock();
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            #endif
            
        }

    }
    
}
