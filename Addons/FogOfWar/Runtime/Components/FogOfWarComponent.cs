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

        public MemArrayAuto<RectUInt> points;

    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerComponent : IConfigComponent {

        /// <summary>
        /// Rect type: sizeX
        /// Range type: range
        /// </summary>
        public uint range;
        /// <summary>
        /// Rect type: sizeY
        /// Range type: minRange
        /// </summary>
        public uint rangeY;
        public float height;
        
    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarSectorRevealerComponent : IConfigComponent {

        public float value;

    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerIsSectorTag : IComponent {}

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerIsRectTag : IComponent {}

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerIsRangeTag : IComponent {}

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerIsPartialTag : IComponent {}

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerPartialComponent : IComponent {

        public byte part;

    }
    
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyWasVisibleAnytimeTag : IComponent {}

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyWasVisibleTag : IComponent {}

}