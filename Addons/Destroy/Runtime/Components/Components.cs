namespace ME.BECS {

    public struct DestroyComponentGroup { }

    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithLifetime : IConfigComponent {

        public float lifetime;

    }

}