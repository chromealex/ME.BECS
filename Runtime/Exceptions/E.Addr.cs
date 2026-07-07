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
                throw new AddrException("Addr of value must be % 4");
            }

        }

    }
    
    public static unsafe partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void ADDR_4<T>(ref T value) where T : unmanaged {

            fixed (void* f = &value) {
                if (((long)(System.IntPtr)f) % 4 == 0) return;
            }
            AddrException.Throw();

        }
        
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void CHECK_FIELD_OFFSET<T>(int offset, string fieldName) {

            var runtimeOffset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(typeof(T), fieldName);
            UnityEngine.Assertions.Assert.IsTrue(offset == runtimeOffset, $"Field {fieldName} in object {typeof(T).Name} has offset {offset} which does not match runtime offset {runtimeOffset}");

        }

    }

}