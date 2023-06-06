namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    
    public static unsafe partial class BatchesExt {

        [INLINE(256)]
        public static ref T GetShared<T>(this ref Batches batches, uint entId, State* state, uint hash = 0u) where T : unmanaged, IComponentShared {

            var result = _address(ref state->components.GetShared<T>(state, entId, hash, out var isNew));
            if (isNew == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, entId, state);
            }
            return ref _ref(result);

        }

        [INLINE(256)]
        public static ref readonly T ReadShared<T>(this ref Batches batches, uint entId, State* state, uint hash = 0u) where T : unmanaged, IComponentShared {

            return ref state->components.ReadShared<T>(state, entId, hash);

        }

        [INLINE(256)]
        public static bool SetShared<T>(this ref Batches batches, uint entId, in T data, State* state, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (state->components.SetShared(state, entId, in data, hash) == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Set_INTERNAL(typeId, entId, state);
                return true;
            }

            return false;

        }

        [INLINE(256)]
        public static bool SetShared(this ref Batches batches, uint entId, uint groupId, void* data, uint dataSize, uint typeId, uint sharedTypeId, State* state, uint hash) {

            if (state->components.SetShared(state, entId, groupId, data, dataSize, sharedTypeId, hash) == true) {
                batches.Set_INTERNAL(typeId, entId, state);
                return true;
            }

            return false;

        }

        [INLINE(256)]
        public static bool RemoveShared<T>(this ref Batches batches, uint entId, State* state, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (state->components.RemoveShared<T>(state, entId, hash) == true) {
                var typeId = StaticTypes<T>.typeId;
                batches.Remove_INTERNAL(typeId, entId, state);
                return true;
            }

            return false;

        }

        [INLINE(256)]
        public static bool HasShared<T>(this in Batches batches, uint entId, State* state, uint hash = 0u) where T : unmanaged, IComponentShared {

            return state->components.HasShared<T>(state, entId, hash);

        }

    }

}