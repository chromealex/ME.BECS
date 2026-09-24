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

        [HIDE_CALLSTACK]
        public static void THROW_ENT_NEW() {
            
            throw new System.Exception("AsParallel cannot create entities in a loop without [EntitiesJobMaxCount(number)] on the job. Specify a positive maximum total number of Ent.New calls per Execute (including calls outside loops and all entity groups), and pass in jobInfo to Ent.New.");
            
        }

        // Unconditional: exceeding a reservation must never access another iteration's entities.
        [HIDE_CALLSTACK]
        public static void JOB_ENTITIES_MAX_COUNT() {
            throw new System.Exception("[ ME.BECS ] EntitiesJobMaxCount exceeded: total Ent.New calls in one Execute invocation exceed the declared maximum.");
        }

    }

}
