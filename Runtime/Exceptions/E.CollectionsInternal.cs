namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class CollectionInternalException : System.Exception {

            public CollectionInternalException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(Unity.Collections.FixedString64Bytes str) {
                ThrowNotBurst(str);
                throw new CollectionInternalException("Internal collection exception. Turn off burst mode to get more info.");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(Unity.Collections.FixedString64Bytes str) => throw new CollectionInternalException($"{Exception.Format(str.ToString())}");

        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void ADDING_DUPLICATE() {
            CollectionInternalException.Throw("Duplicate adding");
        }

        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void VERSION_CHANGED() {
            CollectionInternalException.Throw("Version changed while enumeration");
        }

        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void OP_NOT_STARTED() {
            CollectionInternalException.Throw("Operation not started");
        }

        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void OP_ENDED() {
            CollectionInternalException.Throw("Operation not ended");
        }

        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void IS_EMPTY(uint size) {
            if (size == 0u) CollectionInternalException.Throw("Collection is empty");
        }

        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void IS_NULL(System.IntPtr ptr) {
            if (ptr == System.IntPtr.Zero) CollectionInternalException.Throw("Ptr is null");
        }

        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void IS_NULL(System.IntPtr ptr, Unity.Collections.FixedString64Bytes err) {
            if (ptr == System.IntPtr.Zero) CollectionInternalException.Throw(err);
        }

    }

}