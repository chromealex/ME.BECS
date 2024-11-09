
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
        public static AutoDestroyRegistry Create(State* state, uint capacity) {

            return new AutoDestroyRegistry() {
                list = new MemArray<List<uint>>(ref state->allocator, capacity),
                readWriteSpinnerPerEntity = new MemArray<LockSpinner>(ref state->allocator, capacity),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };

        }

        [INLINE(256)]
        public static void OnEntityAdd(State* state, uint entId) {
            
            if (entId >= state->autoDestroyRegistry.list.Length) {
                state->autoDestroyRegistry.readWriteSpinner.WriteBegin(state);
                state->autoDestroyRegistry.list.Resize(ref state->allocator, entId + 1u, 2);
                state->autoDestroyRegistry.readWriteSpinnerPerEntity.Resize(ref state->allocator, entId + 1u, 2);
                state->autoDestroyRegistry.readWriteSpinner.WriteEnd();
            }
            
        }

        [INLINE(256)]
        public static void Destroy(State* state, in Ent ent) {

            state->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var list = ref state->autoDestroyRegistry.list[in state->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref state->autoDestroyRegistry.readWriteSpinnerPerEntity[in state->allocator, ent.id];
                entitySpinner.Lock();
                if (list.IsCreated == true) {
                    for (uint i = 0; i < list.Count; ++i) {
                        var typeId = list[in state->allocator, i];
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
            state->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            
        }
        
        [INLINE(256)]
        public static void Add(State* state, in Ent ent, uint typeId) {
            
            state->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref state->autoDestroyRegistry.readWriteSpinnerPerEntity[in state->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref state->autoDestroyRegistry.list[in state->allocator, ent.id];
            if (list.IsCreated == false) list = new List<uint>(ref state->allocator, 1u);
            list.Add(ref state->allocator, typeId);
            entitySpinner.Unlock();
            state->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            
        }

        [INLINE(256)]
        public static void Remove(State* state, in Ent ent, uint typeId) {
            
            state->autoDestroyRegistry.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref state->autoDestroyRegistry.readWriteSpinnerPerEntity[in state->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref state->autoDestroyRegistry.list[in state->allocator, ent.id];
            if (list.IsCreated == true) list.Remove(ref state->allocator, typeId);
            entitySpinner.Unlock();
            state->autoDestroyRegistry.readWriteSpinner.ReadEnd(state);
            
        }

    }
    
}