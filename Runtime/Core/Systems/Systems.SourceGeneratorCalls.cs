namespace ME.BECS {

    /// <summary>Typed lifecycle dispatch for generated graph code, including explicit implementations.</summary>
    public static class SourceGeneratorSystemCalls {
        /// <summary>
        /// Runs initialization for the associated lifecycle.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Awake<T>(ref T system, ref SystemContext context) where T : unmanaged, IAwake => system.OnAwake(ref context);
        /// <summary>
        /// Starts source generator system calls processing for the supplied context.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Start<T>(ref T system, ref SystemContext context) where T : unmanaged, IStart => system.OnStart(ref context);
        /// <summary>
        /// Updates source generator system calls using the current inputs and execution context.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Update<T>(ref T system, ref SystemContext context) where T : unmanaged, IUpdate => system.OnUpdate(ref context);
        /// <summary>
        /// Destroys the referenced instance and applies its registered destruction handling.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Destroy<T>(ref T system, ref SystemContext context) where T : unmanaged, IDestroy => system.OnDestroy(ref context);
        /// <summary>
        /// Draws diagnostic geometry for the supplied data.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void DrawGizmos<T>(ref T system, ref SystemContext context) where T : unmanaged, IDrawGizmos => system.OnDrawGizmos(ref context);
    }
}
