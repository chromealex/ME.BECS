namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class ZeroException : System.Exception {

            public ZeroException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new OutOfRangeException("Value must be more than 0");
            }

        }

    }
    
    public static partial class E {

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void NOT_ZERO(in uint index) {
            
            if (index != 0u) return;
            ZeroException.Throw();
            
        }
        
    }

}