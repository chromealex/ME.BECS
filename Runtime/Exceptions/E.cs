namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public static class COND {

        public const string EXCEPTIONS = "EXCEPTIONS";
        public const string EXCEPTIONS_INTERNAL = "EXCEPTIONS_INTERNAL";
        public const string EXCEPTIONS_COMMAND_BUFFER = "EXCEPTIONS_COMMAND_BUFFER";
        public const string EXCEPTIONS_ENTITIES = "EXCEPTIONS_ENTITIES";
        public const string EXCEPTIONS_COLLECTIONS = "EXCEPTIONS_COLLECTIONS";
        public const string EXCEPTIONS_QUERY_BUILDER = "EXCEPTIONS_QUERY_BUILDER";
        public const string EXCEPTIONS_ALLOCATOR = "EXCEPTIONS_ALLOCATOR";
        public const string ALLOCATOR_VALIDATION = "ALLOCATOR_VALIDATION";
        public const string SPARSESET_VALIDATION = "SPARSESET_VALIDATION";
        public const string EXCEPTIONS_THREAD_SAFE = "EXCEPTIONS_THREAD_SAFE";
        public const string EXCEPTIONS_ASPECTS = "EXCEPTIONS_ASPECTS";
        
        public const string ARCHETYPES_INTERNAL_CHECKS = "ARCHETYPES_INTERNAL_CHECKS";
        public const string LEAK_DETECTION = "LEAK_DETECTION";
        public const string MEMORY_ALLOCATOR_BOUNDS_CHECK = "MEMORY_ALLOCATOR_BOUNDS_CHECK";

    }

    public static class Exception {

        public static string Format(string str) {
            return $"[ ME.BECS ] {str}";
        }

    }

}