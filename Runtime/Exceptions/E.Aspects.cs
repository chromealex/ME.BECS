namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public static partial class E {

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_VALID_ASPECT_TYPE_ID(uint typeId) {
            if (typeId > 0u && typeId <= AspectTypeInfo.counter) return;
            InvalidTypeIdException.Throw();
        }

        [Conditional(COND.EXCEPTIONS_ASPECTS)]
        [HIDE_CALLSTACK]
        public static unsafe void IS_VALID_FOR_ASPECT<T>(in Ent ent) where T : unmanaged, IAspect {
            
            var world = ent.World;
            for (uint i = 0u; i < AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId).Length; ++i) {

                var typeId = AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId).Get(i);
                var has = Components.HasUnknownType(world.state, typeId, ent.id, ent.gen, checkEnabled: false);
                if (has == false) {
                    IS_VALID_FOR_ASPECT_BurstDiscard(in ent, typeId);
                    throw new RequiredComponentException($"Entity {ent.ToString()} has no component {typeId}, but it is required");
                }

            }
            
        }

        [BURST_DISCARD]
        private static void IS_VALID_FOR_ASPECT_BurstDiscard(in Ent ent, uint typeId) {
            throw new RequiredComponentException($"Entity {ent.ToString()} has no component {typeId} ({StaticTypesLoadedManaged.loadedTypes[typeId].Name}), but it is required");
        }

    }

}