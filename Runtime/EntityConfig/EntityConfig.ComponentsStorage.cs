namespace ME.BECS {

    using Extensions.SubclassSelector;

    internal interface IConfigComponentsStorage {

    }

    /// <summary>
    /// Defines components storage bit mask data used by entity processing.
    /// </summary>
    [System.Serializable]
    public struct ComponentsStorageBitMask {

        /// <summary>
        /// Mask used by <c>ComponentsStorageBitMask</c>.
        /// </summary>
        public bool[] mask;

    }

    /// <summary>
    /// Defines configuration-backed entity data for components storage.
    /// </summary>
    [System.Serializable]
    public struct ComponentsStorage<T> : IConfigComponentsStorage where T : class {

        /// <summary>
        /// Component storage or descriptors used by this operation.
        /// </summary>
        [SubclassSelector(unmanagedTypes: true, runtimeAssembliesOnly: true, showSelector: false)]
        [UnityEngine.SerializeReference]
        public T[] components;
        /// <summary>
        /// Masks used by <c>ComponentsStorage</c>.
        /// </summary>
        public ComponentsStorageBitMask[] masks;
        
    }

    /// <summary>
    /// Defines configuration-backed entity data for components storage link.
    /// </summary>
    [System.Serializable]
    public struct ComponentsStorageLink : IConfigComponentsStorage {

        /// <summary>
        /// Stores a item record used by <c>ComponentsStorageLink</c>.
        /// </summary>
        [System.Serializable]
        public struct Item {

            /// <summary>
            /// Type descriptor used by the associated operation.
            /// </summary>
            public byte type; // 0 - data, 1 - shared, 2 - static
            /// <summary>
            /// Index of this entry within its containing storage.
            /// </summary>
            public uint index;

        }
        
        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public Item[] items;

    }

}