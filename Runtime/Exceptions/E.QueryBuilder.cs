namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the query builder invariant.
        /// </summary>
        public class QueryBuilderException : System.Exception {

            /// <summary>
            /// Initializes <c>QueryBuilderException</c> from the supplied str.
            /// </summary>
            public QueryBuilderException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.QueryBuilderException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw(string str) {
                ThrowNotBurst(str);
                throw new QueryBuilderException("Internal collection exception");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(string str) => throw new QueryBuilderException($"{Exception.Format(str)}");

        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the query builder is unsafe invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_QUERY_BUILDER)]
        [HIDE_CALLSTACK]
        public static void QUERY_BUILDER_IS_UNSAFE(bool isUnsafe) {
            if (isUnsafe == true) QueryBuilderException.Throw("Query Builder can't use this method because it is in Unsafe mode");
        }

        /// <summary>
        /// Checks the query builder as job invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_QUERY_BUILDER)]
        [HIDE_CALLSTACK]
        public static void QUERY_BUILDER_AS_JOB(bool asJob) {
            if (asJob == true) QueryBuilderException.Throw("Query Builder can't use this method because it is in AsJob mode");
        }

        /// <summary>
        /// Checks the query builder parallel for invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_QUERY_BUILDER)]
        [HIDE_CALLSTACK]
        public static void QUERY_BUILDER_PARALLEL_FOR(uint parallelForBatch) {
            if (parallelForBatch > 0u) QueryBuilderException.Throw("Query Builder can't use this method because it is in ParallelFor mode");
        }

    }

}