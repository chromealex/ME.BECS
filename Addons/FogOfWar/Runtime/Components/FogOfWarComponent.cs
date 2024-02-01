namespace ME.BECS.FogOfWar {
    
    using Unity.Mathematics;

    public struct FogOfWarComponentGroup { }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarComponent : IComponent {

        public MemArrayAuto<int> nodes;

    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarStaticComponent : IComponent {

        public float2 worldSize;
        public uint2 size;
        public MemArrayAuto<int> heights;

    }

}