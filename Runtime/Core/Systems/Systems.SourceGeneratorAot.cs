namespace ME.BECS {

    // AOT reachability only. These null-node calls must never be executed at runtime.
    [UnityEngine.Scripting.Preserve]
    public static unsafe class SourceGeneratorSystemAot {
        [UnityEngine.Scripting.Preserve]
        public static void BurstAwake<T>() where T : unmanaged, IAwake => BurstCompileOnAwake<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstAwake<T>() where T : unmanaged, IAwake => BurstCompileOnAwakeNoBurst<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void FactoryAwake<T>() where T : unmanaged, IAwake => BurstCompileMethod.MakeAwake<T>(default);
        [UnityEngine.Scripting.Preserve]
        public static void BurstStart<T>() where T : unmanaged, IStart => BurstCompileOnStart<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstStart<T>() where T : unmanaged, IStart => BurstCompileOnStartNoBurst<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void FactoryStart<T>() where T : unmanaged, IStart => BurstCompileMethod.MakeStart<T>(default);
        [UnityEngine.Scripting.Preserve]
        public static void BurstUpdate<T>() where T : unmanaged, IUpdate => BurstCompileOnUpdate<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstUpdate<T>() where T : unmanaged, IUpdate => BurstCompileOnUpdateNoBurst<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void FactoryUpdate<T>() where T : unmanaged, IUpdate => BurstCompileMethod.MakeUpdate<T>(default);
        [UnityEngine.Scripting.Preserve]
        public static void BurstDestroy<T>() where T : unmanaged, IDestroy => BurstCompileOnDestroy<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstDestroy<T>() where T : unmanaged, IDestroy => BurstCompileOnDestroyNoBurst<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void FactoryDestroy<T>() where T : unmanaged, IDestroy => BurstCompileMethod.MakeDestroy<T>(default);
        [UnityEngine.Scripting.Preserve]
        public static void BurstDrawGizmos<T>() where T : unmanaged, IDrawGizmos => BurstCompileOnDrawGizmos<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void NoBurstDrawGizmos<T>() where T : unmanaged, IDrawGizmos => BurstCompileOnDrawGizmosNoBurst<T>.MakeMethod(null);
        [UnityEngine.Scripting.Preserve]
        public static void FactoryDrawGizmos<T>() where T : unmanaged, IDrawGizmos => BurstCompileMethod.MakeDrawGizmos<T>(default);
    }
}
