namespace ME.BECS.Effects {
    
    public struct EffectComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }
    
    [ComponentGroup(typeof(EffectComponentGroup))]
    public struct EffectComponent : IComponent {

    }

    /// <summary>
    /// Use this in component with EntityConfig
    /// </summary>
    [System.Serializable]
    public struct EffectConfig {

        public Config config;
        public float lifetime;

    }

}