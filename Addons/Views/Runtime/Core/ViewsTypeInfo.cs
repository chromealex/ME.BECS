namespace ME.BECS.Views {

    /// <summary>
    /// Defines the supported type flags values.
    /// </summary>
    [System.Flags]
    public enum TypeFlags : byte {
        /// <summary>
        /// Initialize option for <c>TypeFlags</c>.
        /// </summary>
        Initialize         = 1 << 0,
        /// <summary>
        /// De initialize option for <c>TypeFlags</c>.
        /// </summary>
        DeInitialize       = 1 << 1,
        /// <summary>
        /// Enable from pool option for <c>TypeFlags</c>.
        /// </summary>
        EnableFromPool     = 1 << 2,
        /// <summary>
        /// Disable to pool option for <c>TypeFlags</c>.
        /// </summary>
        DisableToPool      = 1 << 3,
        /// <summary>
        /// Apply state option for <c>TypeFlags</c>.
        /// </summary>
        ApplyState         = 1 << 4,
        /// <summary>
        /// Update option for <c>TypeFlags</c>.
        /// </summary>
        Update             = 1 << 5,
        /// <summary>
        /// Apply state parallel option for <c>TypeFlags</c>.
        /// </summary>
        ApplyStateParallel = 1 << 6,
        /// <summary>
        /// Update parallel option for <c>TypeFlags</c>.
        /// </summary>
        UpdateParallel     = 1 << 7,
    }
    
    /// <summary>
    /// Stores view type info for the associated views API.
    /// </summary>
    [System.Serializable]
    public struct ViewTypeInfo {

        /// <summary>
        /// Bit flags controlling the associated behavior.
        /// </summary>
        public TypeFlags flags;
        /// <summary>
        /// Culling type used by <c>ViewTypeInfo</c>.
        /// </summary>
        public CullingType cullingType;
        /// <summary>
        /// Change tracker used to decide whether processing is required.
        /// </summary>
        public ViewsTracker.ViewInfo tracker;

        /// <summary>
        /// Indicates has initialize.
        /// </summary>
        public bool HasInitialize => (this.flags & TypeFlags.Initialize) != 0;
        /// <summary>
        /// Indicates has de initialize.
        /// </summary>
        public bool HasDeInitialize => (this.flags & TypeFlags.DeInitialize) != 0;
        /// <summary>
        /// Indicates has enable from pool.
        /// </summary>
        public bool HasEnableFromPool => (this.flags & TypeFlags.EnableFromPool) != 0;
        /// <summary>
        /// Indicates has disable to pool.
        /// </summary>
        public bool HasDisableToPool => (this.flags & TypeFlags.DisableToPool) != 0;
        /// <summary>
        /// Indicates has apply state.
        /// </summary>
        public bool HasApplyState => (this.flags & TypeFlags.ApplyState) != 0;
        /// <summary>
        /// Indicates has apply state parallel.
        /// </summary>
        public bool HasApplyStateParallel => (this.flags & TypeFlags.ApplyStateParallel) != 0;
        /// <summary>
        /// Indicates has update.
        /// </summary>
        public bool HasUpdate => (this.flags & TypeFlags.Update) != 0;
        /// <summary>
        /// Indicates has update parallel.
        /// </summary>
        public bool HasUpdateParallel => (this.flags & TypeFlags.UpdateParallel) != 0;

    }
    
    /// <summary>
    /// Stores views type info for the associated views API.
    /// </summary>
    public static class ViewsTypeInfo {

        /// <summary>
        /// Types used by <c>ViewsTypeInfo</c>.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<System.Type, ViewTypeInfo> types = new System.Collections.Generic.Dictionary<System.Type, ViewTypeInfo>();

        /// <summary>
        /// Registers type.
        /// </summary>
        public static void RegisterType<T>(ViewTypeInfo viewTypeInfo) {

            if (types.ContainsKey(typeof(T)) == false) {
                types.Add(typeof(T), viewTypeInfo);
            }

        }

    }

}