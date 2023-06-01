namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class NotThreadSafeException : System.Exception {

            public NotThreadSafeException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(Unity.Collections.FixedString64Bytes method) {
                ThrowNotBurst(method);
                throw new CommandBufferException("Method is not thread-safe");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(Unity.Collections.FixedString64Bytes method) => throw new CommandBufferException(Exception.Format($"Method {method} is not thread-safe"));

        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS_THREAD_SAFE)]
        [HIDE_CALLSTACK]
        public static void THREAD_CHECK(Unity.Collections.FixedString64Bytes methodName) {
            
            if (JobUtils.IsInParallelJob() == true) {
                NotThreadSafeException.Throw(methodName);
            }

        }

    }

}