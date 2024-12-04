namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class QueryBuilderException : System.Exception {

            public QueryBuilderException(string str) : base(str) { }

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

    public static partial class E {

        [Conditional(COND.EXCEPTIONS_QUERY_BUILDER)]
        [HIDE_CALLSTACK]
        public static void QUERY_BUILDER_IS_UNSAFE(bool isUnsafe) {
            if (isUnsafe == true) QueryBuilderException.Throw("Query Builder can't use this method because it is in Unsafe mode");
        }

        [Conditional(COND.EXCEPTIONS_QUERY_BUILDER)]
        [HIDE_CALLSTACK]
        public static void QUERY_BUILDER_AS_JOB(bool asJob) {
            if (asJob == true) QueryBuilderException.Throw("Query Builder can't use this method because it is in AsJob mode");
        }

        [Conditional(COND.EXCEPTIONS_QUERY_BUILDER)]
        [HIDE_CALLSTACK]
        public static void QUERY_BUILDER_PARALLEL_FOR(uint parallelForBatch) {
            if (parallelForBatch > 0u) QueryBuilderException.Throw("Query Builder can't use this method because it is in ParallelFor mode");
        }

    }

}