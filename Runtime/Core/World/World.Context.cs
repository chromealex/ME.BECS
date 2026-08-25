namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    public struct Context {
        
        private static readonly Unity.Burst.SharedStatic<World> worldBurst = Unity.Burst.SharedStatic<World>.GetOrCreate<Context>();
        public static ref World world => ref worldBurst.Data;

        [INLINE(256)]
        public static void Switch(in World world) {

            Context.world = world;

        }

    }

}
