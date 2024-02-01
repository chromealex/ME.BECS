namespace ME.BECS.Effects {
    
    using ME.BECS.Views;
    
    public struct EffectComponentGroup { }
    
    [ComponentGroup(typeof(EffectComponentGroup))]
    public struct EffectComponent : IComponent {

    }

    /// <summary>
    /// Use this in component with EntityConfig
    /// </summary>
    [System.Serializable]
    public struct EffectConfig {

        public Config config;
        public View view;
        public float lifetime;

    }

}