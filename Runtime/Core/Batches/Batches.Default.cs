namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Unity.Jobs.LowLevel.Unsafe;
    
    public unsafe partial struct Batches {

        [INLINE(256)]
        public static T* GetPtr<T>(in Ent ent, safe_ptr<State> state) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            
            var result = Components.Get<T>(state, in ent, out var isNew);
            if (isNew == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Set_INTERNAL(typeId, in ent, state);
            }
            return result;

        }

        [INLINE(256)]
        public static ref T Get<T>(in Ent ent, safe_ptr<State> state) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            
            var result = Components.Get<T>(state, in ent, out var isNew);
            if (isNew == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Set_INTERNAL(typeId, in ent, state);
            }
            return ref *result;

        }

        [INLINE(256)]
        public static bool Set<T>(in Ent ent, in T data, safe_ptr<State> state) where T : unmanaged, IComponent {
            
            E.IS_IN_TICK(state);
            
            if (Components.Set(state, in ent, in data) == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Set_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Set(in Ent ent, uint typeId, void* data, safe_ptr<State> state) {
            
            E.IS_IN_TICK(state);
            
            var groupId = StaticTypes.groups.Get(typeId);
            if (Components.SetUnknownType(state, typeId, groupId, in ent, data) == true) {
                Batches.Set_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Remove<T>(in Ent ent, safe_ptr<State> state) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            
            if (Components.Remove<T>(state, in ent) == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Remove_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Remove(in Ent ent, uint typeId, safe_ptr<State> state) {

            E.IS_IN_TICK(state);
            
            var groupId = StaticTypes.groups.Get(typeId);
            if (Components.Remove(state, in ent, typeId, groupId) == true) {
                Batches.Remove_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Enable<T>(in Ent ent, safe_ptr<State> state) where T : unmanaged, IComponent {
            
            E.IS_IN_TICK(state);
            
            if (Components.Enable<T>(state, in ent) == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Set_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public static bool Disable<T>(in Ent ent, safe_ptr<State> state) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            
            if (Components.Disable<T>(state, in ent) == true) {
                var typeId = StaticTypes<T>.typeId;
                Batches.Remove_INTERNAL(typeId, in ent, state);
                return true;
            }
            
            return false;

        }

    }

}