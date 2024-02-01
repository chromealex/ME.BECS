namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public static partial class E {

        [Conditional(COND.EXCEPTIONS_ASPECTS)]
        [HIDE_CALLSTACK]
        public static unsafe void IS_VALID_FOR_ASPECT<T>(in Ent ent) where T : unmanaged, IAspect {
            
            var world = ent.World;
            for (uint i = 0u; i < AspectTypeInfo<T>.with.Length; ++i) {

                var typeId = AspectTypeInfo<T>.with.Get(i);
                var has = world.state->components.HasUnknownType(world.state, typeId, ent.id, ent.gen, checkEnabled: false);
                if (has == false) {
                    throw new RequiredComponentException($"Entity has no component {typeId}, but it is required");
                }

            }
            
        }

    }

}