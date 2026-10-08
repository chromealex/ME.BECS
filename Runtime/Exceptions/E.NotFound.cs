namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the not found invariant.
        /// </summary>
        public class NotFoundException : System.Exception {

            /// <summary>
            /// Initializes <c>NotFoundException</c> from the supplied str.
            /// </summary>
            public NotFoundException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.NotFoundException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static System.Exception Throw(string obj) {
                return new OutOfRangeException($"Object was not found {obj}");
            }

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the not found invariant when the corresponding safety checks are enabled.
        /// </summary>
        [HIDE_CALLSTACK]
        public static System.Exception NOT_FOUND(string obj) {
            
            return NotFoundException.Throw(obj);
            
        }

    }

}