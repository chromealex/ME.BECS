
namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides helper operations for ent clone.
    /// </summary>
    public static class EntCloneExt {

        /// <summary>
        /// Creates a copy of the supplied state using the requested allocation context.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static Ent Clone(this in Ent source) {
            return source.Clone(source.worldId);
        }

        /// <summary>
        /// Creates a copy of the supplied state using the requested allocation context.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static Ent Clone(this in Ent source, ushort worldId) {

            // TODO: Somehow we need to call generic method with group id to clone to the same group
            var ent = Ent.New(worldId);
            ent.EditorName = source.EditorName;
            ent.CopyFrom(in source);
            return ent;

        }
        
        /// <summary>
        /// Copies the supplied source state into this ent clone ext instance.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static void CopyFrom(this in Ent target, in Ent source) {

            Components.CopyFrom(source.World.state, in source, target.World.state, in target);

        }

        /// <summary>
        /// Copies the supplied source state into this ent clone ext instance.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static void CopyFrom<TIgnore0>(this in Ent target, in Ent source) where TIgnore0 : unmanaged, IComponent {

            Components.CopyFrom<TIgnore0>(source.World.state, in source, target.World.state, in target);

        }

        /// <summary>
        /// Copies the supplied source state into this ent clone ext instance.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [NotThreadSafe]
        public static void CopyFrom<TIgnore0, TIgnore1>(this in Ent target, in Ent source) where TIgnore0 : unmanaged, IComponent where TIgnore1 : unmanaged, IComponent {

            Components.CopyFrom<TIgnore0, TIgnore1>(source.World.state, in source, target.World.state, in target);

        }

    }

}