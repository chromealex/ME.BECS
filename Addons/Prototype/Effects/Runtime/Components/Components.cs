namespace ME.BECS.Effects {
    
    public struct EffectComponentGroup { }
    
    [ComponentGroup(typeof(EffectComponentGroup))]
    public struct EffectComponent : IComponent {

    }

    [System.Serializable]
    public struct EffectConfig {

        public Config config;
        public ME.BECS.Views.View view;
        public float lifetime;

    }

}