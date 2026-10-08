namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the size equals invariant.
        /// </summary>
        public class SizeEqualsException : System.Exception {

            /// <summary>
            /// Initializes <c>SizeEqualsException</c> from the supplied str.
            /// </summary>
            public SizeEqualsException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.SizeEqualsException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new OutOfRangeException("Size must be equals");
            }

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the size equals invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void SIZE_EQUALS(uint sizeT, uint sizeStored) {
            
            if (sizeT == sizeStored) return;
            SizeEqualsException.Throw();
            
        }
        
    }

}