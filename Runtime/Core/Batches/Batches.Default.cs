namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Unity.Jobs.LowLevel.Unsafe;
    
    public static unsafe partial class BatchesExt {

        [INLINE(256)]
        public static T* GetPtr<T>(this ref Batches batches, in Ent ent, State* state) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            
            var result = state->components.Get<T>(state, in ent, out var isNew);
            if (isNew == true) {
                *result = StaticTypes<T>.defaultValue;
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, in ent, state);
            }
            return result;

        }

        [INLINE(256)]
        public static ref T Get<T>(this ref Batches batches, in Ent ent, State* state) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            
            var result = state->components.Get<T>(state, in ent, out var isNew);
            if (isNew == true) {
                *result = StaticTypes<T>.defaultValue;
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, in ent, state);
            }
            return ref *result;

        }

        [INLINE(256)]
        public static bool Set<T>(this ref Batches batches, in Ent ent, in T data, State* state) where T : unmanaged, IComponent {
            
            E.IS_IN_TICK(state);
            
            if (state->components.Set(state, in ent, in data) == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Set(this ref Batches batches, in Ent ent, uint typeId, void* data, State* state) {
            
            E.IS_IN_TICK(state);
            
            var groupId = StaticTypes.groups.Get(typeId);
            if (state->components.SetUnknownType(state, typeId, groupId, in ent, data) == true) {
                batches.Set_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Remove<T>(this ref Batches batches, in Ent ent, State* state) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            
            if (state->components.Remove<T>(state, in ent) == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Remove_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Remove(this ref Batches batches, in Ent ent, uint typeId, uint groupId, State* state) {

            E.IS_IN_TICK(state);
            
            if (state->components.Remove(state, in ent, typeId, groupId) == true) {
                batches.Remove_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Enable<T>(this ref Batches batches, in Ent ent, State* state) where T : unmanaged, IComponent {
            
            E.IS_IN_TICK(state);
            
            if (state->components.Enable<T>(state, in ent) == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Disable<T>(this ref Batches batches, in Ent ent, State* state) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            
            if (state->components.Disable<T>(state, in ent) == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Remove_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

    }

}