namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using static Cuts;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the addr invariant.
        /// </summary>
        public class AddrException : System.Exception {

            /// <summary>
            /// Initializes <c>AddrException</c> from the supplied str.
            /// </summary>
            public AddrException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.AddrException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new AddrException("Addr of value must be % 4");
            }

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static unsafe partial class E {

        /// <summary>
        /// Asserts that the supplied value has a four-byte-aligned address when safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void ADDR_4<T>(ref T value) where T : unmanaged {

            fixed (void* f = &value) {
                if (((long)(System.IntPtr)f) % 4 == 0) return;
            }
            AddrException.Throw();

        }
        
        /// <summary>
        /// Checks the check field offset invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void CHECK_FIELD_OFFSET<T>(int offset, string fieldName) {

            var runtimeOffset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(typeof(T), fieldName);
            UnityEngine.Assertions.Assert.IsTrue(offset == runtimeOffset, $"Field {fieldName} in object {typeof(T).Name} has offset {offset} which does not match runtime offset {runtimeOffset}");

        }

    }

}