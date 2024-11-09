
namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static unsafe class EntCloneExt {

        [INLINE(256)]
        [NotThreadSafe]
        public static Ent Clone(this in Ent source) {
            return source.Clone(source.worldId);
        }

        [INLINE(256)]
        [NotThreadSafe]
        public static Ent Clone(this in Ent source, ushort worldId) {

            var ent = Ent.New(worldId);
            ent.CopyFrom(in source);
            return ent;

        }
        
        [INLINE(256)]
        [NotThreadSafe]
        public static void CopyFrom(this in Ent target, in Ent source) {

            Components.CopyFrom(source.World.state, in source, target.World.state, in target);

        }

        [INLINE(256)]
        [NotThreadSafe]
        public static void CopyFrom<TIgnore0>(this in Ent target, in Ent source) where TIgnore0 : unmanaged, IComponent {

            Components.CopyFrom<TIgnore0>(source.World.state, in source, target.World.state, in target);

        }

        [INLINE(256)]
        [NotThreadSafe]
        public static void CopyFrom<TIgnore0, TIgnore1>(this in Ent target, in Ent source) where TIgnore0 : unmanaged, IComponent where TIgnore1 : unmanaged, IComponent {

            Components.CopyFrom<TIgnore0, TIgnore1>(source.World.state, in source, target.World.state, in target);

        }

    }

}