namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    
    public unsafe partial struct Batches {

        [INLINE(256)]
        public static ref T GetShared<T>(in Ent ent, safe_ptr<State> state, uint hash = 0u) where T : unmanaged, IComponentShared {

            var result = _addressT(ref Components.GetShared<T>(state, in ent, hash, out var isNew));
            if (isNew == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Set_INTERNAL(typeId, in ent, state);
            }
            return ref _ref(result.ptr);

        }

        [INLINE(256)]
        public static ref readonly T ReadShared<T>(in Ent ent, safe_ptr<State> state, uint hash = 0u) where T : unmanaged, IComponentShared {

            return ref Components.ReadShared<T>(state, ent.id, hash);

        }

        [INLINE(256)]
        public static bool SetShared<T>(in Ent ent, in T data, safe_ptr<State> state, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (Components.SetShared(state, in ent, in data, hash) == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Set_INTERNAL(typeId, in ent, state);
                return true;
            }

            return false;

        }

        [INLINE(256)]
        public static bool SetShared(in Ent ent, uint groupId, void* data, uint dataSize, uint typeId, uint sharedTypeId, safe_ptr<State> state, uint hash) {

            if (Components.SetShared(state, in ent, groupId, data, dataSize, sharedTypeId, hash) == true) {
                Batches.Set_INTERNAL(typeId, in ent, state);
                return true;
            }

            return false;

        }

        [INLINE(256)]
        public static bool RemoveShared<T>(in Ent ent, safe_ptr<State> state, uint hash = 0u) where T : unmanaged, IComponentShared {

            if (Components.RemoveShared<T>(state, in ent, hash) == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Remove_INTERNAL(typeId, in ent, state);
                return true;
            }

            return false;

        }

        [INLINE(256)]
        public static bool HasShared<T>(in Ent ent, safe_ptr<State> state, uint hash = 0u) where T : unmanaged, IComponentShared {

            return Components.HasShared<T>(state, ent.id, hash);

        }

    }

}