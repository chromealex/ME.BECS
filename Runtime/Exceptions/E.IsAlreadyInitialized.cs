using System;

namespace ME.BECS {
    
    using System.Diagnostics;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    
    public static partial class E {

        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void IS_ALREADY_INITIALIZED(System.Array array) {
            if (array != null) throw new InvalidOperationException("This array is already initialized");
        }

    }

}