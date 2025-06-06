namespace ME.BECS {

    public struct EntityConfigComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.cyan;

    }
    
    /// <summary>
    /// Use this interface to assign to unmanaged type
    /// to show in EntityConfig static list
    /// </summary>
    public interface IConfigComponentStatic : IComponentBase, IConfigComponentBase { }

    /// <summary>
    /// Use this interface to initialize entity
    /// when you apply EntityConfig
    /// </summary>
    public interface IConfigInitialize : IComponent, IConfigComponentBase {

        void OnInitialize(in Ent ent);

    }

    /// <summary>
    /// Use this interface to assign to unmanaged type
    /// to show in EntityConfig list
    /// </summary>
    public interface IConfigComponent : IComponent, IConfigComponentBase { }

    /// <summary>
    /// Use this interface to assign to unmanaged type
    /// to show in EntityConfig list
    /// </summary>
    public interface IConfigComponentShared : IComponentShared, IConfigComponentBase { }

    [ComponentGroup(typeof(EntityConfigComponentGroup))]
    public struct EntityConfigComponent : IComponent {

        public uint id;
        public UnsafeEntityConfig EntityConfig {
            get {
                var config = EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(this.id);
                E.IS_CREATED(config);
                return config;
            }
        }

    }

}