namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the collection internal invariant.
        /// </summary>
        public class CollectionInternalException : System.Exception {

            /// <summary>
            /// Initializes <c>CollectionInternalException</c> from the supplied str.
            /// </summary>
            public CollectionInternalException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.CollectionInternalException</c>.
            /// </summary>
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

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the adding duplicate invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void ADDING_DUPLICATE() {
            CollectionInternalException.Throw("Duplicate adding");
        }

        /// <summary>
        /// Checks the version changed invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void VERSION_CHANGED() {
            CollectionInternalException.Throw("Version changed while enumeration");
        }

        /// <summary>
        /// Checks the op not started invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void OP_NOT_STARTED() {
            CollectionInternalException.Throw("Operation not started");
        }

        /// <summary>
        /// Checks the op ended invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void OP_ENDED() {
            CollectionInternalException.Throw("Operation not ended");
        }

        /// <summary>
        /// Checks the is empty invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void IS_EMPTY(uint size) {
            if (size == 0u) CollectionInternalException.Throw("Collection is empty");
        }

        /// <summary>
        /// Checks the is null invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void IS_NULL(System.IntPtr ptr) {
            if (ptr == System.IntPtr.Zero) CollectionInternalException.Throw("Ptr is null");
        }

        /// <summary>
        /// Checks the is null invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_INTERNAL)]
        [HIDE_CALLSTACK]
        public static void IS_NULL(System.IntPtr ptr, Unity.Collections.FixedString64Bytes err) {
            if (ptr == System.IntPtr.Zero) CollectionInternalException.Throw(err);
        }

    }

}