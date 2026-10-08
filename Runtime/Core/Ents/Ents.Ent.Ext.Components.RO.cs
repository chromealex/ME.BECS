namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides component, lifecycle and identity operations on entity handles.
    /// </summary>
    public static partial class EntExt {

        /// <summary>
        /// Tests whether the specified component is enabled on the entity.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool IsEnabled<T>(in this EntRO ent) where T : unmanaged, IComponent => ent.ent.IsEnabled<T>();

        /// <summary>
        /// Tests component presence, optionally requiring the component to be enabled.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool Has<T>(in this EntRO ent, bool checkEnabled = true) where T : unmanaged, IComponent => ent.ent.Has<T>(checkEnabled);

        /// <summary>
        /// Returns read-only access to the component, or its default value when the component is absent.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static ref readonly T Read<T>(in this EntRO ent) where T : unmanaged, IComponent => ref ent.ent.Read<T>();

        /// <summary>
        /// Reads component data and reports whether the component is present.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static ref readonly T TryRead<T>(in this EntRO ent, out bool exists) where T : unmanaged, IComponent => ref ent.ent.TryRead<T>(out exists);

        /// <summary>
        /// Reads component data and reports whether the component is present.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool TryRead<T>(in this EntRO ent, out T component) where T : unmanaged, IComponent => ent.ent.TryRead(out component);

        /// <summary>
        /// Compares the tag's presence with the requested boolean value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool HasTag<T>(in this EntRO ent, bool value) where T : unmanaged, IComponent => ent.ent.HasTag<T>(value);

        /// <summary>
        /// Reads immutable component data from the entity's configuration.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static T ReadStatic<T>(in this EntRO ent) where T : unmanaged, IConfigComponentStatic => ent.ent.ReadStatic<T>();

        /// <summary>
        /// Tests whether the entity configuration supplies the specified static component.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool HasStatic<T>(in this EntRO ent) where T : unmanaged, IConfigComponentStatic => ent.ent.HasStatic<T>();

        /// <summary>
        /// Attempts to read immutable configuration data and reports whether it exists.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        [SafetyCheck(RefOp.ReadOnly)] public static bool TryReadStatic<T>(in this EntRO ent, out T component) where T : unmanaged, IConfigComponentStatic => ent.ent.TryReadStatic(out component);

    }

}