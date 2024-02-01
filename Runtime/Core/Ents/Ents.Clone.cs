
namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static unsafe class EntCloneExt {

        [INLINE(256)]
        public static Ent Clone(this in Ent source) {
            return source.Clone(source.worldId);
        }

        [INLINE(256)]
        public static Ent Clone(this in Ent source, ushort worldId) {

            var ent = Ent.New(worldId);
            ent.CopyFrom(in source);
            return ent;

        }
        
        [INLINE(256)]
        public static void CopyFrom(this in Ent target, in Ent source) {

            var sourceState = source.World.state;
            var targetState = target.World.state;
            Batches.Apply(sourceState);
            sourceState->components.CopyFrom(sourceState, in source, targetState, in target);

        }

    }

}