namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using static Cuts;

    public partial class E {

        public class AddrException : System.Exception {

            public AddrException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new System.InvalidOperationException("Addr of value must be % 4");
            }

        }

    }
    
    public static unsafe partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void ADDR_4<T>(ref T value) where T : struct {

            if (((long)(System.IntPtr)_address(ref value)) % 4 == 0) return;
            AddrException.Throw();

        }
        
    }

}