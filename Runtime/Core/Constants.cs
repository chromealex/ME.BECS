namespace ME.BECS {
    
    using Unity.Collections;

    public static class Constants {

        #if UNITY_2023_1_OR_NEWER
        public const Allocator ALLOCATOR_DOMAIN = Allocator.Domain;
        public const Allocator ALLOCATOR_DOMAIN_REAL = Allocator.Domain;
        #else
        public static Allocator ALLOCATOR_DOMAIN => WorldsDomainAllocator.allocatorDomain.Allocator.ToAllocator;
        public const Allocator ALLOCATOR_DOMAIN_REAL = Allocator.Persistent;
        #endif
        
        public const Allocator ALLOCATOR_PERSISTENT = Allocator.Persistent;
        public const Allocator ALLOCATOR_TEMP = Allocator.Temp;
        public const Allocator ALLOCATOR_TEMPJOB = Allocator.TempJob;
        
    }

    public static class ALLOC_TAGS {

        [AllocatorTagInfo] public static readonly AllocatorTagInfo COMPONENTS = new AllocatorTagInfo() { tag = 1000, name = "COMPONENTS", color = UnityEngine.Color.dodgerBlue };
        [AllocatorTagInfo] public static readonly AllocatorTagInfo COLLECTIONS = new AllocatorTagInfo() { tag = 1001, name = "COLLECTIONS", color = UnityEngine.Color.magenta };
        [AllocatorTagInfo] public static readonly AllocatorTagInfo AUTO_DESTROY = new AllocatorTagInfo() { tag = 1002, name = "AUTO DESTROY", color = UnityEngine.Color.yellow };
        [AllocatorTagInfo] public static readonly AllocatorTagInfo ENTITIES = new AllocatorTagInfo() { tag = 1003, name = "ENTITIES", color = UnityEngine.Color.paleGoldenRod };
        [AllocatorTagInfo] public static readonly AllocatorTagInfo ONE_SHOT = new AllocatorTagInfo() { tag = 1004, name = "ONE SHOT", color = UnityEngine.Color.indianRed };
        [AllocatorTagInfo] public static readonly AllocatorTagInfo SYSTEMS = new AllocatorTagInfo() { tag = 1005, name = "SYSTEMS", color = UnityEngine.Color.lightSkyBlue };

    }

}