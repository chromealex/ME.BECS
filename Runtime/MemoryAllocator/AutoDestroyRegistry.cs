
namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    [BURST(CompileSynchronously = true)]
    public static unsafe class AutoDestroyRegistryStatic<T> where T : unmanaged, IComponentDestroy {

        [BURST(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(AutoDestroyRegistry.DestroyDelegate))]
        public static void Destroy(byte* comp) {

            _ref((T*)comp).Destroy();
            
        }

    }
    
    public unsafe struct AutoDestroyRegistry {

        public delegate void DestroyDelegate(byte* comp);

        private MemArray<List<uint>> list;
        private MemArray<LockSpinner> readWriteSpinnerPerEntity;
        private ReadWriteSpinner readWriteSpinner;

        [INLINE(256)]
        public static AutoDestroyRegistry Create(safe_ptr<State> state, uint capacity) {

            return new AutoDestroyRegistry() {
                list = new MemArray<List<uint>>(ref state.ptr->allocator, capacity),
                readWriteSpinnerPerEntity = new MemArray<LockSpinner>(ref state.ptr->allocator, capacity),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };

        }

        [INLINE(256)]
        public static void OnEntityAdd(safe_ptr<State> state, uint entId) {
            
            if (entId >= state.ptr->autoDestroyRegistry.list.Length) {
                state.ptr->autoDestroyRegistry.readWriteSpinner.WriteBegin(state);
                state.ptr->autoDestroyRegistry.list.Resize(ref state.ptr->allocator, entId + 1u, 2);
                state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity.Resize(ref state.ptr->allocator, entId + 1u, 2);
                state.ptr->autoDestroyRegistry.readWriteSpinner.WriteEnd();
            }
            
        }

        [INLINE(256)]
        public static void Destroy(safe_ptr<State> state, in Ent ent) {

            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var list = ref state.ptr->autoDestroyRegistry.list[in state.ptr->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
                entitySpinner.Lock();
                if (list.IsCreated == true) {
                    for (uint i = 0; i < list.Count; ++i) {
                        var typeId = list[in state.ptr->allocator, i];
                        var comp = Components.ReadUnknownType(state, typeId, ent.id, ent.gen, out var exists);
                        if (exists == true) {
                            // component exists - call destroy method
                            var func = new Unity.Burst.FunctionPointer<DestroyDelegate>(StaticTypesDestroyRegistry.registry.Data.Get(typeId));
                            func.Invoke(comp);
                        }
                    }

                    list.Clear();
                }
                entitySpinner.Unlock();
            }
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            
        }
        
        [INLINE(256)]
        public static void Add(safe_ptr<State> state, in Ent ent, uint typeId) {
            
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref state.ptr->autoDestroyRegistry.list[in state.ptr->allocator, ent.id];
            if (list.IsCreated == false) list = new List<uint>(ref state.ptr->allocator, 1u);
            list.Add(ref state.ptr->allocator, typeId);
            entitySpinner.Unlock();
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            
        }

        [INLINE(256)]
        public static void Remove(safe_ptr<State> state, in Ent ent, uint typeId) {
            
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref state.ptr->autoDestroyRegistry.readWriteSpinnerPerEntity[in state.ptr->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref state.ptr->autoDestroyRegistry.list[in state.ptr->allocator, ent.id];
            if (list.IsCreated == true) list.Remove(ref state.ptr->allocator, typeId);
            entitySpinner.Unlock();
            state.ptr->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            
        }

    }
    
}