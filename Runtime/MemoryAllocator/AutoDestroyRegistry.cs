
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


        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
        }

        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
        }

        [INLINE(256)]
        public static AutoDestroyRegistry Create(safe_ptr<State> state, uint capacity) {

            return default;

        }

        [INLINE(256)]
        public static void OnEntityAdd(safe_ptr<State> state, uint entId) {

            
        }

        [INLINE(256)]
        public static void Destroy(safe_ptr<State> state, in Ent ent) {

            for (uint typeId = 1u; typeId < StaticTypesAutoDestroy.registry.Data.Length; ++typeId) {
                if (StaticTypesAutoDestroy.Is(typeId) == false) continue;
                Invoke(state, in ent, typeId);
            }
            
        }
        
        [INLINE(256)]
        public static void Destroy(safe_ptr<State> state, in Ent ent, uint typeId) {

            if (Invoke(state, in ent, typeId) == true) {
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

            
        }

        [INLINE(256)]
        public static void Remove(safe_ptr<State> state, in Ent ent, uint typeId) {

            
        }

    }
    
}
