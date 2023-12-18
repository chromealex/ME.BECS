namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Unity.Jobs.LowLevel.Unsafe;
    
    public static unsafe partial class BatchesExt {

        [INLINE(256)]
        public static T* GetPtr<T>(this ref Batches batches, uint entId, ushort gen, State* state) where T : unmanaged {

            E.IS_IN_TICK(state);
            
            var result = state->components.Get<T>(state, entId, gen, out var isNew);
            if (isNew == true) {
                *result = StaticTypes<T>.defaultValue;
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, entId, state);
            }
            return result;

        }

        [INLINE(256)]
        public static ref T Get<T>(this ref Batches batches, uint entId, ushort gen, State* state) where T : unmanaged {

            E.IS_IN_TICK(state);
            
            var result = state->components.Get<T>(state, entId, gen, out var isNew);
            if (isNew == true) {
                *result = StaticTypes<T>.defaultValue;
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, entId, state);
            }
            return ref *result;

        }

        [INLINE(256)]
        public static bool Set<T>(this ref Batches batches, uint entId, ushort gen, in T data, State* state) where T : unmanaged {
            
            E.IS_IN_TICK(state);
            
            if (state->components.Set(state, entId, gen, in data) == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, entId, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Set(this ref Batches batches, uint entId, ushort gen, uint typeId, void* data, State* state) {
            
            E.IS_IN_TICK(state);

            var groupId = StaticTypes.groups.Get(typeId);
            if (state->components.SetUnknownType(state, typeId, groupId, entId, gen, data) == true) {
                batches.Set_INTERNAL(typeId, entId, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Remove<T>(this ref Batches batches, uint entId, ushort gen, State* state) where T : unmanaged {

            E.IS_IN_TICK(state);
            
            if (state->components.Remove<T>(state, entId, gen) == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Remove_INTERNAL(typeId, entId, state);
                return true;
            }
            
            return false;

        }

    }

}