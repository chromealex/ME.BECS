namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class CustomException : System.Exception {

            public CustomException(Unity.Collections.FixedString512Bytes str) : base(str.ToString()) { }

            [HIDE_CALLSTACK]
            public static void Throw(Unity.Collections.FixedString512Bytes str) {
                throw new CustomException(str);
            }

        }

    }
    
}