namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class InvalidTypeIdException : System.Exception {

            public InvalidTypeIdException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new OutOfRangeException("Type id is out of range");
            }

        }

        public class IsTagException : System.Exception {

            public IsTagException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new OutOfRangeException("Component is a tag which doesn't allow to access this method");
            }

        }

    }
    
    public static partial class E {

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_VALID_TYPE_ID(uint typeId) {
            if (typeId > 0u && typeId <= StaticTypes.counter) return;
            InvalidTypeIdException.Throw();
        }

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_NOT_TAG(uint typeId) {
            if (StaticTypes.sizes.Get(typeId) != 0u) return;
            IsTagException.Throw();
        }

    }

}