namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Reports a violation of the cond invariant.
    /// </summary>
    public static class COND {

        /// <summary>
        /// Exceptions constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS = "EXCEPTIONS";
        /// <summary>
        /// Exceptions internal constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS_INTERNAL = "EXCEPTIONS_INTERNAL";
        /// <summary>
        /// Exceptions command buffer constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS_COMMAND_BUFFER = "EXCEPTIONS_COMMAND_BUFFER";
        /// <summary>
        /// Exceptions entities constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS_ENTITIES = "EXCEPTIONS_ENTITIES";
        /// <summary>
        /// Exceptions collections constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS_COLLECTIONS = "EXCEPTIONS_COLLECTIONS";
        /// <summary>
        /// Exceptions query builder constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS_QUERY_BUILDER = "EXCEPTIONS_QUERY_BUILDER";
        /// <summary>
        /// Exceptions allocator constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS_ALLOCATOR = "EXCEPTIONS_ALLOCATOR";
        /// <summary>
        /// Allocator validation constant used by <c>COND</c>.
        /// </summary>
        public const string ALLOCATOR_VALIDATION = "ALLOCATOR_VALIDATION";
        /// <summary>
        /// Sparseset validation constant used by <c>COND</c>.
        /// </summary>
        public const string SPARSESET_VALIDATION = "SPARSESET_VALIDATION";
        /// <summary>
        /// Exceptions thread safe constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS_THREAD_SAFE = "EXCEPTIONS_THREAD_SAFE";
        /// <summary>
        /// Exceptions aspects constant used by <c>COND</c>.
        /// </summary>
        public const string EXCEPTIONS_ASPECTS = "EXCEPTIONS_ASPECTS";
        
        /// <summary>
        /// Leak detection constant used by <c>COND</c>.
        /// </summary>
        public const string LEAK_DETECTION = "LEAK_DETECTION";
        /// <summary>
        /// Leak detection allocator constant used by <c>COND</c>.
        /// </summary>
        public const string LEAK_DETECTION_ALLOCATOR = "LEAK_DETECTION_ALLOCATOR";
        /// <summary>
        /// Leak detection counter constant used by <c>COND</c>.
        /// </summary>
        public const string LEAK_DETECTION_COUNTER = "LEAK_DETECTION_COUNTER";
        /// <summary>
        /// Memory allocator bounds check constant used by <c>COND</c>.
        /// </summary>
        public const string MEMORY_ALLOCATOR_BOUNDS_CHECK = "MEMORY_ALLOCATOR_BOUNDS_CHECK";
        /// <summary>
        /// Editor constant used by <c>COND</c>.
        /// </summary>
        public const string EDITOR = "UNITY_EDITOR";

    }

    /// <summary>
    /// Reports a violation of the  invariant.
    /// </summary>
    [IgnoreProfiler]
    public static class Exception {

        /// <summary>
        /// Formats the supplied value using the requested presentation settings.
        /// </summary>
        public static string Format(string str) {
            return $"[ ME.BECS ] {str}";
        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    [IgnoreProfiler]
    public static partial class E {
    }

}