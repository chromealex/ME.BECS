namespace ME.BECS.FogOfWar {
    
    using Unity.Mathematics;

    public enum RevealType : byte {

        Range = 0,
        Rect = 1,

    }
    
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

        public float2 mapPosition;
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
    public struct FogOfWarShadowCopyRequiredComponent : IConfigComponent {}

    [EditorComment("Stores links to original entity")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyComponent : IComponent {

        public Ent forTeam;
        public Ent original;

    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyPointsComponent : IComponent {

        public MemArrayAuto<float3> points;

    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerComponent : IConfigComponent {

        /// <summary>
        /// 0 - range, 1 - rect
        /// </summary>
        public byte type;
        /// <summary>
        /// Rect type: sizeX
        /// Range type: range
        /// </summary>
        public float range;
        /// <summary>
        /// Rect type: sizeY
        /// Range type: minRange
        /// </summary>
        public float rangeY;
        public float height;
        
    }
    
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyWasVisible : IComponent {}

}