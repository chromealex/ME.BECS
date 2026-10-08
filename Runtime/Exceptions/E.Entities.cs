namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the entity not alive invariant.
        /// </summary>
        public class EntityNotAliveException : System.Exception {

            /// <summary>
            /// Initializes <c>EntityNotAliveException</c> from the supplied str.
            /// </summary>
            public EntityNotAliveException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.EntityNotAliveException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(Ent ent) {
                ThrowNotBurst(ent);
                throw new EntityNotAliveException("Entity is not alive");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(Ent ent) => throw new EntityNotAliveException(Exception.Format($"{ent} is not alive"));

        }

        /// <summary>
        /// Reports a violation of the entity is empty invariant.
        /// </summary>
        public class EntityIsEmptyException : System.Exception {

            /// <summary>
            /// Initializes <c>EntityIsEmptyException</c> from the supplied str.
            /// </summary>
            public EntityIsEmptyException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.EntityIsEmptyException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(Ent ent) {
                ThrowNotBurst(ent);
                throw new EntityIsEmptyException("Entity is empty");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(Ent ent) => throw new EntityIsEmptyException(Exception.Format($"{ent} is empty"));

        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is alive invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_ENTITIES)]
        [HIDE_CALLSTACK]
        public static void IS_ALIVE(in Ent ent) {
            if (ent == default) EntityIsEmptyException.Throw(ent);
            if (ent.IsAlive() == true) return;
            EntityNotAliveException.Throw(ent);
        }

    }

}