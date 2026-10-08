using System;

namespace ME.BECS {
    
    using System.Diagnostics;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is already initialized invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void IS_ALREADY_INITIALIZED(System.Array array) {
            if (array != null) throw new InvalidOperationException("This array is already initialized");
        }

    }

}