namespace ME.BECS {

    /// <summary>Typed lifecycle dispatch for generated graph code, including explicit implementations.</summary>
    public static class SourceGeneratorSystemCalls {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Awake<T>(ref T system, ref SystemContext context) where T : unmanaged, IAwake => system.OnAwake(ref context);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Start<T>(ref T system, ref SystemContext context) where T : unmanaged, IStart => system.OnStart(ref context);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Update<T>(ref T system, ref SystemContext context) where T : unmanaged, IUpdate => system.OnUpdate(ref context);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Destroy<T>(ref T system, ref SystemContext context) where T : unmanaged, IDestroy => system.OnDestroy(ref context);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void DrawGizmos<T>(ref T system, ref SystemContext context) where T : unmanaged, IDrawGizmos => system.OnDrawGizmos(ref context);
    }
}
