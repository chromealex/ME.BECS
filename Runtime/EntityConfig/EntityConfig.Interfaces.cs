namespace ME.BECS {

    /// <summary>
    /// Use this interface to assign to unmanaged type
    /// to show in EntityConfig static list
    /// </summary>
    public interface IComponentStatic { }

    /// <summary>
    /// Use this interface to assign to unmanaged type
    /// to show in EntityConfig list
    /// </summary>
    public interface IConfigComponent : IComponent { }

    /// <summary>
    /// Use this interface to assign to unmanaged type
    /// to show in EntityConfig list
    /// </summary>
    public interface IConfigComponentShared : IComponentShared { }

    public struct EntityConfigComponent : IComponent {

        public uint id;
        public UnsafeEntityConfig EntityConfig => EntityConfigRegistry.GetById(this.id);

    }

}