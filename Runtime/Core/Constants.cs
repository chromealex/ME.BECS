namespace ME.BECS {
    
    using Unity.Collections;

    /// <summary>
    /// Defines constants state and operations.
    /// </summary>
    public static class Constants {

        #if UNITY_2023_1_OR_NEWER
        /// <summary>
        /// Allocator domain constant used by <c>Constants</c>.
        /// </summary>
        public const Allocator ALLOCATOR_DOMAIN = Allocator.Domain;
        /// <summary>
        /// Allocator domain real constant used by <c>Constants</c>.
        /// </summary>
        public const Allocator ALLOCATOR_DOMAIN_REAL = Allocator.Domain;
        #else
        /// <summary>
        /// Gets allocator domain; this implementation returns <c>WorldsDomainAllocator.allocatorDomain.Allocator.ToAllocator</c>.
        /// </summary>
        public static Allocator ALLOCATOR_DOMAIN => WorldsDomainAllocator.allocatorDomain.Allocator.ToAllocator;
        /// <summary>
        /// Allocator domain real constant used by <c>Constants</c>.
        /// </summary>
        public const Allocator ALLOCATOR_DOMAIN_REAL = Allocator.Persistent;
        #endif
        
        /// <summary>
        /// Allocator persistent constant used by <c>Constants</c>.
        /// </summary>
        public const Allocator ALLOCATOR_PERSISTENT = Allocator.Persistent;
        /// <summary>
        /// Allocator temp constant used by <c>Constants</c>.
        /// </summary>
        public const Allocator ALLOCATOR_TEMP = Allocator.Temp;
        /// <summary>
        /// Allocator tempjob constant used by <c>Constants</c>.
        /// </summary>
        public const Allocator ALLOCATOR_TEMPJOB = Allocator.TempJob;
        
    }

    /// <summary>
    /// Defines alloc tags state and operations.
    /// </summary>
    public static class ALLOC_TAGS {

        /// <summary>
        /// Component storage or descriptors used by this operation.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo COMPONENTS = new AllocatorTagInfo() { tag = 1000, name = "COMPONENTS", color = UnityEngine.Color.dodgerBlue };
        /// <summary>
        /// Collections constant used by <c>ALLOC_TAGS</c>.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo COLLECTIONS = new AllocatorTagInfo() { tag = 1001, name = "COLLECTIONS", color = UnityEngine.Color.magenta };
        /// <summary>
        /// Auto destroy constant used by <c>ALLOC_TAGS</c>.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo AUTO_DESTROY = new AllocatorTagInfo() { tag = 1002, name = "AUTO DESTROY", color = UnityEngine.Color.yellow };
        /// <summary>
        /// Entity handles processed or stored by this operation.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo ENTITIES = new AllocatorTagInfo() { tag = 1003, name = "ENTITIES", color = UnityEngine.Color.paleGoldenRod };
        /// <summary>
        /// One shot constant used by <c>ALLOC_TAGS</c>.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo ONE_SHOT = new AllocatorTagInfo() { tag = 1004, name = "ONE SHOT", color = UnityEngine.Color.indianRed };
        /// <summary>
        /// Systems constant used by <c>ALLOC_TAGS</c>.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo SYSTEMS = new AllocatorTagInfo() { tag = 1005, name = "SYSTEMS", color = UnityEngine.Color.lightSkyBlue };

        /// <summary>
        /// Components data constant used by <c>ALLOC_TAGS</c>.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo COMPONENTS_DATA = new AllocatorTagInfo() { tag = 1006, name = "COMPONENTS DATA", color = UnityEngine.Color.deepSkyBlue };
        /// <summary>
        /// Components bits constant used by <c>ALLOC_TAGS</c>.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo COMPONENTS_BITS = new AllocatorTagInfo() { tag = 1007, name = "COMPONENTS BITS", color = UnityEngine.Color.cornflowerBlue };
        /// <summary>
        /// Components pages constant used by <c>ALLOC_TAGS</c>.
        /// </summary>
        [AllocatorTagInfo] public static readonly AllocatorTagInfo COMPONENTS_PAGES = new AllocatorTagInfo() { tag = 1008, name = "COMPONENTS PAGES", color = UnityEngine.Color.cadetBlue };

    }

}