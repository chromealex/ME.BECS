namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class NotFoundException : System.Exception {

            public NotFoundException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static System.Exception Throw(string obj) {
                return new OutOfRangeException($"Object was not found {obj}");
            }

        }

    }
    
    public static partial class E {

        [HIDE_CALLSTACK]
        public static System.Exception NOT_FOUND(string obj) {
            
            return NotFoundException.Throw(obj);
            
        }

    }

}