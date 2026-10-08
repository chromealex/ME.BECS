namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the invalid type ID invariant.
        /// </summary>
        public class InvalidTypeIdException : System.Exception {

            /// <summary>
            /// Initializes <c>InvalidTypeIdException</c> from the supplied str.
            /// </summary>
            public InvalidTypeIdException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.InvalidTypeIdException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new OutOfRangeException("Type id is out of range. Be sure you have referenced it by your systems or jobs code or add it manually by [assembly: CodeGeneratorInclude(..)].");
            }

        }

        /// <summary>
        /// Reports a violation of the is tag invariant.
        /// </summary>
        public class IsTagException : System.Exception {

            /// <summary>
            /// Initializes <c>IsTagException</c> from the supplied str.
            /// </summary>
            public IsTagException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.IsTagException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(uint typeId) {
                ThrowNotBurst(typeId);
                throw new OutOfRangeException("Component is a tag which doesn't allow to access this method");
            }

            [BURST_DISCARD]
            private static void ThrowNotBurst(uint typeId) {
                var type = StaticTypesLoadedManaged.allLoadedTypes[typeId];
                throw new OutOfRangeException($"Component {type.FullName} is a tag which doesn't allow to access this method");
            }

        }

        /// <summary>
        /// Reports a violation of the is static invariant.
        /// </summary>
        public class IsStaticException : System.Exception {

            /// <summary>
            /// Initializes <c>IsStaticException</c> from the supplied str.
            /// </summary>
            public IsStaticException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.IsStaticException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(uint typeId) {
                ThrowNotBurst(typeId);
                throw new OutOfRangeException("Component is a static which doesn't allow to access this method");
            }

            [BURST_DISCARD]
            private static void ThrowNotBurst(uint typeId) {
                var type = StaticTypesLoadedManaged.allLoadedTypes[typeId];
                throw new OutOfRangeException($"Component {type.FullName} is a static which doesn't allow to access this method");
            }

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is valid type ID invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_VALID_TYPE_ID(uint typeId) {
            if (typeId > 0u && typeId <= StaticTypes.counter) return;
            InvalidTypeIdException.Throw();
        }

        /// <summary>
        /// Checks the is not tag invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_NOT_TAG(uint typeId) {
            if (StaticTypes.sizes.Get(typeId) != 0u) return;
            IsTagException.Throw(typeId);
        }

        /// <summary>
        /// Checks the is not static invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_NOT_STATIC(uint typeId) {
            if (typeId >= StaticTypes.staticTypeId.Length || StaticTypes.staticTypeId.Get(typeId) == 0u) return;
            IsStaticException.Throw(typeId);
        }

    }

}