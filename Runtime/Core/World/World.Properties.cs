namespace ME.BECS {
    
    using Unity.Mathematics;

    /// <summary>
    /// Configures state behavior and storage.
    /// </summary>
    [System.Serializable]
    public struct StateProperties {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static StateProperties Default => new StateProperties() {
            entitiesCapacity = 100u,
            storageCapacity = 1u,
            sharedComponentsCapacity = 10u,
            oneShotTasksCapacity = 1u,
            mode = WorldMode.Logic,
        };

        /// <summary>
        /// Minimum .
        /// </summary>
        public static StateProperties Min => new StateProperties() {
            entitiesCapacity = 1u,
            storageCapacity = 1u,
            oneShotTasksCapacity = 1u,
            sharedComponentsCapacity = 1u,
            mode = WorldMode.Logic,
            allowStaticStorage = false,
        };

        /// <summary>
        /// Resize internal storage and fill pools with entities by default. Set up this value to fit max entities count of your world.
        /// </summary>
        [UnityEngine.MinAttribute(1)]
        [UnityEngine.Tooltip("Resize internal storage and fill pools with entities by default. Set up this value to fit max entities count of your world.")]
        public uint entitiesCapacity;
        /// <summary>
        /// Resize components storage per component type.
        /// </summary>
        [UnityEngine.MinAttribute(1)]
        [UnityEngine.Tooltip("Resize components storage per component type.")]
        public uint storageCapacity;
        /// <summary>
        /// Resize shared components storage. Set up this value to fit max shared components count.
        /// </summary>
        [UnityEngine.MinAttribute(0)]
        [UnityEngine.Tooltip("Resize shared components storage. Set up this value to fit max shared components count.")]
        public uint sharedComponentsCapacity;
        /// <summary>
        /// Resize one-shot tasks storage by this value.
        /// </summary>
        [UnityEngine.Tooltip("Resize one-shot tasks storage by this value.")]
        public uint oneShotTasksCapacity;
        /// <summary>
        /// Use Logic for logic worlds, Visual for client-only local worlds.
        /// </summary>
        [UnityEngine.MinAttribute(0)]
        [UnityEngine.Tooltip("Use Logic for logic worlds, Visual for client-only local worlds.")]
        public WorldMode mode;

        /// <summary>
        /// Whether allow static storage behavior or state is selected.
        /// </summary>
        public bool allowStaticStorage;

        /// <summary>
        /// Entities capacity for the associated storage.
        /// </summary>
        public uint EntitiesCapacity => math.max(Bitwise.AlignUp(this.entitiesCapacity, Ents.ENTITIES_PER_PAGE), Ents.ENTITIES_PER_PAGE);

    }

    /// <summary>
    /// Configures allocator behavior and storage.
    /// </summary>
    [System.Serializable]
    public struct AllocatorProperties {

        /// <summary>
        /// Memory Allocator default size, but it will be resized on demand. Min size is &lt;i&gt;{MIN_ZONE_SIZE_IN_KB} KB&lt;/i&gt;. This size is used when new allocator zone created, so be sure you have took right size (Default value for this field 1MB).
        /// </summary>
        [UnityEngine.MinAttribute((float)MemoryAllocator.MIN_ZONE_SIZE)]
        [UnityEngine.Tooltip("Memory Allocator default size, but it will be resized on demand. Min size is <i>{MIN_ZONE_SIZE_IN_KB} KB</i>. This size is used when new allocator zone created, so be sure you have took right size (Default value for this field 1MB).")]
        public uint sizeInBytesCapacity;

    }

    /// <summary>
    /// Configures world creation, including state capacity and allocator settings.
    /// </summary>
    [System.Serializable]
    public struct WorldProperties {
        
        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static WorldProperties Default => new WorldProperties() {
            stateProperties = StateProperties.Default,
            allocatorProperties = new AllocatorProperties() {
                sizeInBytesCapacity = 1024 * 1024, // 1MB
            },
        };

        /// <summary>
        /// World's name.
        /// </summary>
        [UnityEngine.Tooltip("World's name.")]
        public Unity.Collections.FixedString64Bytes name;
        /// <summary>
        /// State properties used by <c>WorldProperties</c>.
        /// </summary>
        public StateProperties stateProperties;
        /// <summary>
        /// Allocator properties used by <c>WorldProperties</c>.
        /// </summary>
        public AllocatorProperties allocatorProperties;

    }

}