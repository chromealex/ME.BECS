namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class CommandBufferException : System.Exception {

            public CommandBufferException(string str) : base(str) { }

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

    public static partial class E {

        [Conditional(COND.EXCEPTIONS_COMMAND_BUFFER)]
        [HIDE_CALLSTACK]
        public static void COMMAND_BUFFER_OPERATION_NOT_SUPPORTED(Unity.Collections.FixedString64Bytes method, uint entId) {
            CommandBufferException.Throw(method, entId);
        }

    }

}