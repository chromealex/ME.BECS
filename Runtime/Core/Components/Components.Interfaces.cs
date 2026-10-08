namespace ME.BECS {

    /// <summary>
    /// Defines the operations required by config component base.
    /// </summary>
    public interface IConfigComponentBase : IComponentBase {}
    
    /// <summary>
    /// Identifies a type supported by the ECS component infrastructure.
    /// </summary>
    public interface IComponentBase {}

    /// <summary>
    /// Marks unmanaged data stored on individual entities.
    /// </summary>
    public interface IComponent : IComponentBase {}

    /// <summary>
    /// Stores per-entity state for i component shared.
    /// </summary>
    public interface IComponentShared : IComponent {

        /// <summary>
        /// Returns static hash of instance
        /// </summary>
        /// <returns></returns>
        uint GetHash() => throw new System.NotImplementedException();

    }

    /// <summary>
    /// Stores per-entity state for i component destroy.
    /// </summary>
    public interface IComponentDestroy : IComponent {

        /// <summary>
        /// Releases resources owned by the component when its entity is destroyed.
        /// </summary>
        void Destroy(in Ent ent);

    }

    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// Stores per-entity state for t null.
    /// </summary>
    [ComponentGroup(typeof(CoreComponentGroup))]
    public readonly struct TNull : IComponent { }

}