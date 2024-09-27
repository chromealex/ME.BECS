namespace ME.BECS.FogOfWar {
    
    using Unity.Mathematics;

    public struct FogOfWarComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.gray;
        
    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarComponent : IComponent {

        public MemArrayAuto<byte> nodes;
        public MemArrayAuto<byte> explored;

    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarStaticComponent : IComponent {

        public float2 worldSize;
        public uint2 size;
        public MemArrayAuto<float> heights;
        public float maxHeight;

    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarHasShadowCopyComponent : IComponent {}

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyRequiredComponent : IComponent {}

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyComponent : IComponent {

        public Ent forTeam;
        public Ent original;

    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct UnitShadowCopyViewComponent : IComponentStatic, IConfigInitialize {

        public ME.BECS.Views.View view;

        public void OnInitialize(in Ent ent) {
            ent.Set(new FogOfWarShadowCopyRequiredComponent());
        }

    }

}