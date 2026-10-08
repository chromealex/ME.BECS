namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the custom invariant.
        /// </summary>
        public class CustomException : System.Exception {

            /// <summary>
            /// Initializes <c>CustomException</c> from the supplied str.
            /// </summary>
            public CustomException(Unity.Collections.FixedString512Bytes str) : base(str.ToString()) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.CustomException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(Unity.Collections.FixedString512Bytes str) {
                throw new CustomException(str);
            }

        }

    }
    
}