namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Defines required component exception data used by entity processing.
        /// </summary>
        public class RequiredComponentException : System.Exception {

            /// <summary>
            /// Initializes <c>RequiredComponentException</c> from the supplied str.
            /// </summary>
            public RequiredComponentException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.RequiredComponentException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw<T>(in Ent ent) where T : unmanaged, IComponentBase {
                ThrowNotBurst<T>(in ent);
                throw new RequiredComponentException("Entity has no component, but it is required");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst<T>(in Ent ent) => throw new RequiredComponentException(Exception.Format($"{ent.ToString()} has no component {typeof(T)}, but it is required"));

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the required invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void REQUIRED<T>(in Ent ent) where T : unmanaged, IComponent {
            
            if (ent.Has<T>(checkEnabled: false) == true) return;
            RequiredComponentException.Throw<T>(in ent);
            
        }

        /// <summary>
        /// Checks the throw required invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void THROW_REQUIRED<T>(in Ent ent) where T : unmanaged, IComponentBase {
            
            RequiredComponentException.Throw<T>(in ent);
            
        }

    }

}