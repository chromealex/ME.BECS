namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the command buffer invariant.
        /// </summary>
        public class CommandBufferException : System.Exception {

            /// <summary>
            /// Initializes <c>CommandBufferException</c> from the supplied str.
            /// </summary>
            public CommandBufferException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.CommandBufferException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(Unity.Collections.FixedString64Bytes method, uint entId) {
                ThrowNotBurst(method, entId);
                throw new CommandBufferException("CommandBuffer method not supported");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(Unity.Collections.FixedString64Bytes method, uint entId) => throw new CommandBufferException(Exception.Format($"Method {method} not supported on entity {entId}"));

        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the command buffer operation not supported invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COMMAND_BUFFER)]
        [HIDE_CALLSTACK]
        public static void COMMAND_BUFFER_OPERATION_NOT_SUPPORTED(Unity.Collections.FixedString64Bytes method, uint entId) {
            CommandBufferException.Throw(method, entId);
        }

    }

}