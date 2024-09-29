namespace ME.BECS {
    
    public interface IComponent {}

    public interface IComponentShared : IComponent {

        /// <summary>
        /// Returns static hash of instance
        /// </summary>
        /// <returns></returns>
        uint GetHash() => throw new System.NotImplementedException();

    }

    public interface IComponentDestroy : IComponent {

        void Destroy();

    }

    // ReSharper disable once InconsistentNaming
    public readonly struct TNull : IComponent { }

}