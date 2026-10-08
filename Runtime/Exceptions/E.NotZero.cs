namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the zero invariant.
        /// </summary>
        public class ZeroException : System.Exception {

            /// <summary>
            /// Initializes <c>ZeroException</c> from the supplied str.
            /// </summary>
            public ZeroException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.ZeroException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new OutOfRangeException("Value must be more than 0");
            }

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the not zero invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void NOT_ZERO(in uint index) {
            
            if (index != 0u) return;
            ZeroException.Throw();
            
        }
        
    }

}