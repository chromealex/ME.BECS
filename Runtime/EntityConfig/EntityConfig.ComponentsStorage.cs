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

}