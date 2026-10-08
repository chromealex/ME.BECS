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
        /// Reports a violation of the mode invariant.
        /// </summary>
        public class ModeException : System.Exception {

            /// <summary>
            /// Initializes <c>ModeException</c> from the supplied message.
            /// </summary>
            public ModeException(string message) : base(message) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.ModeException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(WorldMode current, WorldMode required) {
                throw new ModeException($"Mode {current} must be {required} to use this method.");
            }

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is visual mode invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_VISUAL_MODE(WorldMode mode) {

            if (mode == WorldMode.Visual) return;
            ModeException.Throw(mode, WorldMode.Visual);

        }

        /// <summary>
        /// Checks the is logic mode invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_LOGIC_MODE(WorldMode mode) {

            if (mode == WorldMode.Logic) return;
            ModeException.Throw(mode, WorldMode.Logic);

        }

    }

}