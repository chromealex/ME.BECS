namespace ME.BECS {

    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the type not found invariant.
        /// </summary>
        public class TypeNotFoundException : System.Exception {

            /// <summary>
            /// Initializes <c>TypeNotFoundException</c> from the supplied str.
            /// </summary>
            public TypeNotFoundException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.TypeNotFoundException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(string str) {
                ThrowNotBurst(str);
                throw new TypeNotFoundException("Type not found in types list. Select `ME.BECS/Regenerate Assemblies`.");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(string str) => throw new TypeNotFoundException(Exception.Format(str));

        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {
        
        /// <summary>
        /// Checks the type not found in managed types invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void TYPE_NOT_FOUND_IN_MANAGED_TYPES() {
            E.TypeNotFoundException.Throw("Type not found in types list. Select `ME.BECS/Regenerate Assemblies`.");
        }

    }
    
}