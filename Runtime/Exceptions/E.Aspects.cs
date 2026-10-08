namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is valid aspect type ID invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_VALID_ASPECT_TYPE_ID(uint typeId) {
            if (typeId > 0u && typeId <= AspectTypeInfo.counter) return;
            InvalidTypeIdException.Throw();
        }

        /// <summary>
        /// Checks the is valid for aspect invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_ASPECTS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_VALID_FOR_ASPECT<T>(in Ent ent) where T : unmanaged, IAspect {
            
            var world = ent.World;
            for (uint i = 0u; i < AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId).Length; ++i) {

                var typeId = AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId).Get(i);
                var has = Components.HasUnknownType(world.state, typeId, ent.id, ent.gen, checkEnabled: false);
                if (has == false) {
                    IS_VALID_FOR_ASPECT_BurstDiscard(in ent, typeId);
                    throw new RequiredComponentException("Entity has no component, but it is required");
                }

            }
            
        }

        [BURST_DISCARD][IgnoreProfiler]
        private static void IS_VALID_FOR_ASPECT_BurstDiscard(in Ent ent, uint typeId) {
            throw new RequiredComponentException($"Entity {ent.ToString()} has no component {typeId} ({StaticTypesLoadedManaged.loadedTypes[typeId].Name}), but it is required");
        }

    }

}