namespace ME.BECS {

    // AOT reachability only. These null-node calls must never be executed at runtime.
    /// <summary>
    /// Provides system AOT for BECS source-generator publication.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static unsafe class SourceGeneratorSystemAot {
        /// <summary>
        /// Invokes the Burst implementation of awake.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void BurstAwake<T>() where T : unmanaged, IAwake => BurstCompileOnAwake<T>.MakeMethod(null);
        /// <summary>
        /// Invokes the managed implementation of awake.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstAwake<T>() where T : unmanaged, IAwake => BurstCompileOnAwakeNoBurst<T>.MakeMethod(null);
        /// <summary>
        /// Builds the callback for awake.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void FactoryAwake<T>() where T : unmanaged, IAwake => BurstCompileMethod.MakeAwake<T>(default);
        /// <summary>
        /// Invokes the Burst implementation of start.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void BurstStart<T>() where T : unmanaged, IStart => BurstCompileOnStart<T>.MakeMethod(null);
        /// <summary>
        /// Invokes the managed implementation of start.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstStart<T>() where T : unmanaged, IStart => BurstCompileOnStartNoBurst<T>.MakeMethod(null);
        /// <summary>
        /// Builds the callback for start.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void FactoryStart<T>() where T : unmanaged, IStart => BurstCompileMethod.MakeStart<T>(default);
        /// <summary>
        /// Invokes the Burst implementation of update.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void BurstUpdate<T>() where T : unmanaged, IUpdate => BurstCompileOnUpdate<T>.MakeMethod(null);
        /// <summary>
        /// Invokes the managed implementation of update.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstUpdate<T>() where T : unmanaged, IUpdate => BurstCompileOnUpdateNoBurst<T>.MakeMethod(null);
        /// <summary>
        /// Builds the callback for update.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void FactoryUpdate<T>() where T : unmanaged, IUpdate => BurstCompileMethod.MakeUpdate<T>(default);
        /// <summary>
        /// Invokes the Burst implementation of destroy.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void BurstDestroy<T>() where T : unmanaged, IDestroy => BurstCompileOnDestroy<T>.MakeMethod(null);
        /// <summary>
        /// Invokes the managed implementation of destroy.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstDestroy<T>() where T : unmanaged, IDestroy => BurstCompileOnDestroyNoBurst<T>.MakeMethod(null);
        /// <summary>
        /// Builds the callback for destroy.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void FactoryDestroy<T>() where T : unmanaged, IDestroy => BurstCompileMethod.MakeDestroy<T>(default);
        /// <summary>
        /// Invokes the Burst implementation of draw gizmos.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void BurstDrawGizmos<T>() where T : unmanaged, IDrawGizmos => BurstCompileOnDrawGizmos<T>.MakeMethod(null);
        /// <summary>
        /// Invokes the managed implementation of draw gizmos.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstDrawGizmos<T>() where T : unmanaged, IDrawGizmos => BurstCompileOnDrawGizmosNoBurst<T>.MakeMethod(null);
        /// <summary>
        /// Builds the callback for draw gizmos.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public static void FactoryDrawGizmos<T>() where T : unmanaged, IDrawGizmos => BurstCompileMethod.MakeDrawGizmos<T>(default);
    }
}
