namespace ME.BECS {

    /// <summary>
    /// Use this interface to assign to unmanaged type
    /// to show in EntityConfig static list
    /// </summary>
    public interface IStaticComponent { }

    /// <summary>
    /// Use this interface to assign to unmanaged type
    /// to show in EntityConfig list
    /// </summary>
    public interface IConfigComponent : IComponent { }

    public struct EntityConfigComponent : IComponent {

        public uint id;
        public EntityConfig EntityConfig => EntityConfigRegistry.GetId(this.id);

    }

}