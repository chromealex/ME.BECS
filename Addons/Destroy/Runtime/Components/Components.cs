namespace ME.BECS {

    public struct DestroyComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }

    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithLifetime : IConfigComponent {

        public float lifetime;

    }
    
    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithTicks : IConfigComponent {

        public int ticks;

    }

}