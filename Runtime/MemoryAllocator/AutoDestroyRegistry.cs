
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
        public void OnEntityAdd(State* state, uint entId) {
            
            if (entId >= this.list.Length) {
                this.readWriteSpinner.WriteBegin(state);
                this.list.Resize(ref state->allocator, entId + 1u, 2);
                this.readWriteSpinnerPerEntity.Resize(ref state->allocator, entId + 1u, 2);
                this.readWriteSpinner.WriteEnd();
            }
            
        }

        [INLINE(256)]
        public void Destroy(State* state, in Ent ent) {

            this.readWriteSpinner.ReadBegin(state);
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.IsCreated == true) {
                ref var entitySpinner = ref this.readWriteSpinnerPerEntity[in state->allocator, ent.id];
                entitySpinner.Lock();
                if (list.IsCreated == true) {
                    for (uint i = 0; i < list.Count; ++i) {
                        var typeId = list[in state->allocator, i];
                        var comp = state->components.ReadUnknownType(state, typeId, ent.id, ent.gen, out var exists);
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
            this.readWriteSpinner.ReadEnd(state);
            
        }
        
        [INLINE(256)]
        public void Add(State* state, in Ent ent, uint typeId) {
            
            this.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref this.readWriteSpinnerPerEntity[in state->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.IsCreated == false) list = new List<uint>(ref state->allocator, 1u);
            list.Add(ref state->allocator, typeId);
            entitySpinner.Unlock();
            this.readWriteSpinner.ReadEnd(state);
            
        }

        [INLINE(256)]
        public void Remove(State* state, in Ent ent, uint typeId) {
            
            this.readWriteSpinner.ReadBegin(state);
            ref var entitySpinner = ref this.readWriteSpinnerPerEntity[in state->allocator, ent.id];
            entitySpinner.Lock();
            ref var list = ref this.list[in state->allocator, ent.id];
            if (list.IsCreated == true) list.Remove(ref state->allocator, typeId);
            entitySpinner.Unlock();
            this.readWriteSpinner.ReadEnd(state);
            
        }

    }
    
}