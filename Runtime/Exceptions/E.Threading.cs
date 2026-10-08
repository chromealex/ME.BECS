namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the not thread safe invariant.
        /// </summary>
        public class NotThreadSafeException : System.Exception {

            /// <summary>
            /// Initializes <c>NotThreadSafeException</c> from the supplied str.
            /// </summary>
            public NotThreadSafeException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.NotThreadSafeException</c>.
            /// </summary>
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

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the thread check invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_THREAD_SAFE)]
        [HIDE_CALLSTACK]
        public static void THREAD_CHECK(Unity.Collections.FixedString64Bytes methodName) {
            
            if (JobUtils.IsInParallelJob() == true) {
                NotThreadSafeException.Throw(methodName);
            }

        }

        /// <summary>
        /// Checks the throw ent new invariant when the corresponding safety checks are enabled.
        /// </summary>
        [HIDE_CALLSTACK]
        public static void THROW_ENT_NEW() {
            
            throw new System.Exception("AsParallel cannot create entities in a loop, recursion or a potentially repeating exception filter without [EntitiesJobMaxCount(number)] on the job. Specify a positive maximum total number of Ent.New calls per Execute (including calls outside repeating regions and all entity groups), and pass in jobInfo to Ent.New.");
            
        }

        // Unconditional: exceeding a reservation must never access another iteration's entities.
        /// <summary>
        /// Checks the job entities max count invariant when the corresponding safety checks are enabled.
        /// </summary>
        [HIDE_CALLSTACK]
        public static void JOB_ENTITIES_MAX_COUNT() {
            throw new System.Exception("[ ME.BECS ] EntitiesJobMaxCount exceeded: total Ent.New calls in one Execute invocation exceed the declared maximum.");
        }

    }

}
