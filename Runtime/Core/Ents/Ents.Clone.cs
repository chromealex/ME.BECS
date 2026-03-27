
namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    public static class EntCloneExt {

        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static Ent Clone(this in Ent source) {
            return source.Clone(source.worldId);
        }

        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static Ent Clone(this in Ent source, ushort worldId) {

            // TODO: Somehow we need to call generic method with group id to clone to the same group
            var ent = Ent.New(worldId);
            ent.EditorName = source.EditorName;
            ent.CopyFrom(in source);
            return ent;

        }
        
        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static void CopyFrom(this in Ent target, in Ent source) {

            Components.CopyFrom(source.World.state, in source, target.World.state, in target);

        }

        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static void CopyFrom<TIgnore0>(this in Ent target, in Ent source) where TIgnore0 : unmanaged, IComponent {

            Components.CopyFrom<TIgnore0>(source.World.state, in source, target.World.state, in target);

        }

        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static void CopyFrom<TIgnore0, TIgnore1>(this in Ent target, in Ent source) where TIgnore0 : unmanaged, IComponent where TIgnore1 : unmanaged, IComponent {

            Components.CopyFrom<TIgnore0, TIgnore1>(source.World.state, in source, target.World.state, in target);

        }

    }

}