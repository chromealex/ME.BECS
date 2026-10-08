
namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using static Cuts;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    /// <summary>
    /// Tracks entity-owned resources requiring destruction callbacks.
    /// </summary>
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct AutoDestroyRegistry {

        /// <summary>
        /// Defines the callback signature for destroy delegate.
        /// </summary>
        public delegate void DestroyDelegate(in Ent ent, byte* comp);


        /// <summary>
        /// Provides the <c>SerializeHeaders</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
        }

        /// <summary>
        /// Provides the <c>DeserializeHeaders</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
        }

        /// <summary>
        /// Creates <c>AutoDestroyRegistry</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static AutoDestroyRegistry Create(safe_ptr<State> state, uint capacity) {

            return default;

        }

        /// <summary>
        /// Provides the <c>OnEntityAdd</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public static void OnEntityAdd(safe_ptr<State> state, uint entId) {

            
        }

        /// <summary>
        /// Destroys the referenced instance and applies its registered destruction handling.
        /// </summary>
        [INLINE(256)]
        public static void Destroy(safe_ptr<State> state, in Ent ent) {

            for (uint typeId = 1u; typeId < StaticTypesAutoDestroy.registry.Data.Length; ++typeId) {
                if (StaticTypesAutoDestroy.Is(typeId) == false) continue;
                Invoke(state, in ent, typeId);
            }
            
        }
        
        /// <summary>
        /// Destroys the referenced instance and applies its registered destruction handling.
        /// </summary>
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

        /// <summary>
        /// Provides the <c>Add</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public static void Add(safe_ptr<State> state, in Ent ent, uint typeId) {

            
        }

        /// <summary>
        /// Provides the <c>Remove</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public static void Remove(safe_ptr<State> state, in Ent ent, uint typeId) {

            
        }

    }
    
}
