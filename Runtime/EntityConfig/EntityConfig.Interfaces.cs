namespace ME.BECS {

    /// <summary>
    /// Groups entity config components for change tracking and queries.
    /// </summary>
    public struct EntityConfigComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
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

        /// <summary>
        /// Initializes i config initialize state from the supplied context.
        /// </summary>
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

    /// <summary>
    /// Stores per-entity state for entity config.
    /// </summary>
    [ComponentGroup(typeof(EntityConfigComponentGroup))]
    public struct EntityConfigComponent : IComponent {

        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        [EntityConfigId]
        public uint id;
        /// <summary>
        /// Entity config used by <c>EntityConfigComponent</c>.
        /// </summary>
        public UnsafeEntityConfig EntityConfig {
            get {
                var config = EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(this.id);
                E.IS_CREATED(config);
                return config;
            }
        }

    }

}