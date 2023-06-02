namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class SizeEqualsException : System.Exception {

            public SizeEqualsException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new OutOfRangeException("Size must be equals");
            }

        }

    }
    
    public static partial class E {

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void SIZE_EQUALS(uint sizeT, uint sizeStored) {
            
            if (sizeT == sizeStored) return;
            SizeEqualsException.Throw();
            
        }
        
    }

}