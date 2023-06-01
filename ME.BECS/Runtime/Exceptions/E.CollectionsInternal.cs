namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class CollectionInternalException : System.Exception {

            public CollectionInternalException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(string str) {
                ThrowNotBurst(str);
                throw new CollectionInternalException("Internal collection exception");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(string str) => throw new CollectionInternalException($"{Exception.Format(str)}");

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

    }

}