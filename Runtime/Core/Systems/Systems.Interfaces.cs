namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Defines the operations required by generic without.
    /// </summary>
    public interface IGenericWithout {}
    /// <summary>
    /// Defines the operations required by generic without.
    /// </summary>
    public interface IGenericWithout<T> : IGenericWithout { }

    /// <summary>
    /// Coordinates i during the ECS system lifecycle.
    /// </summary>
    public interface ISystem {

    }

    /// <summary>
    /// Defines the system callback invoked during world initialization.
    /// </summary>
    public interface IAwake : ISystem {

        /// <summary>
        /// Initializes i awake state from the supplied context.
        /// </summary>
        void OnAwake(ref SystemContext context);

    }

    /// <summary>
    /// Defines the system callback invoked when the world starts.
    /// </summary>
    public interface IStart : ISystem {

        /// <summary>
        /// Starts i start processing for the supplied context.
        /// </summary>
        void OnStart(ref SystemContext context);

    }

    /// <summary>
    /// Defines the system callback used to release world-owned system resources.
    /// </summary>
    public interface IDestroy : ISystem {

        /// <summary>
        /// Releases i destroy state at the end of its owning lifecycle.
        /// </summary>
        void OnDestroy(ref SystemContext context);

    }

    /// <summary>
    /// Defines the system callback used to advance a world update.
    /// </summary>
    public interface IUpdate : ISystem {

        /// <summary>
        /// Updates i update using the current inputs and execution context.
        /// </summary>
        void OnUpdate(ref SystemContext context);

    }

    /// <summary>
    /// Defines the operations required by draw gizmos.
    /// </summary>
    public interface IDrawGizmos : ISystem {

        /// <summary>
        /// Draws diagnostic geometry for the associated state.
        /// </summary>
        void OnDrawGizmos(ref SystemContext context);

    }

}