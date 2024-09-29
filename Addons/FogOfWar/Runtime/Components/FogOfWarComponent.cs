namespace ME.BECS.FogOfWar {
    
    using Unity.Mathematics;

    public struct FogOfWarComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.gray;
        
    }

    [EditorComment("Main runtime component to store nodes")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarComponent : IComponent {

        public MemArrayAuto<byte> nodes;
        public MemArrayAuto<byte> explored;

    }

    [EditorComment("World settings")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarStaticComponent : IComponent {

        public float2 worldSize;
        public uint2 size;
        public MemArrayAuto<float> heights;
        public float maxHeight;

    }

    [EditorComment("Tag indicates shadow copy for the entity")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarHasShadowCopyComponent : IComponent {}

    [EditorComment("Tag indicates shadow copy creation")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyRequiredComponent : IComponent {}

    [EditorComment("Stores links to original entity")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyComponent : IComponent {

        public Ent forTeam;
        public Ent original;

    }

    [EditorComment("View which represents shadow copy view")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct UnitShadowCopyViewComponent : IConfigComponentStatic, IConfigInitialize {

        public ME.BECS.Views.View view;

        public void OnInitialize(in Ent ent) {
            ent.Set(new FogOfWarShadowCopyRequiredComponent());
        }

    }

}