namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Tracks the world associated with the current ECS execution context.
    /// </summary>
    public struct Context {
        
        private static readonly Unity.Burst.SharedStatic<World> worldBurst = Unity.Burst.SharedStatic<World>.GetOrCreate<Context>();
        /// <summary>
        /// World used by the containing operation.
        /// </summary>
        public static ref World world => ref worldBurst.Data;

        /// <summary>
        /// Makes the supplied world the current execution context.
        /// </summary>
        [INLINE(256)]
        public static void Switch(in World world) {

            Context.world = world;

        }

    }

}
