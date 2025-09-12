namespace ME.BECS {

    using Extensions.SubclassSelector;

    internal interface IConfigComponentsStorage {

    }
    
    [System.Serializable]
    public struct ComponentsStorage<T> : IConfigComponentsStorage where T : class {

        [SubclassSelector(unmanagedTypes: true, runtimeAssembliesOnly: true, showSelector: false)]
        [UnityEngine.SerializeReference]
        public T[] components;
        
    }

    [System.Serializable]
    public struct ComponentsStorageLink : IConfigComponentsStorage {

        [System.Serializable]
        public struct Item {

            public byte type; // 0 - data, 1 - shared, 2 - static
            public uint index;

        }
        
        public Item[] items;

    }

}