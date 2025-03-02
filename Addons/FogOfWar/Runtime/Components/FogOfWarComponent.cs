#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.FogOfWar {
    
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
        public MemArrayAuto<tfloat> heights;
        public tfloat maxHeight;

    }

    [EditorComment("Tag indicates shadow copy creation")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyRequiredComponent : IConfigComponent, IConfigInitialize {

        public void OnInitialize(in Ent ent) {
            var playersSystem = ent.World.GetSystem<Players.PlayersSystem>();
            ent.Set(new FogOfWarShadowCopyRequiredRuntimeComponent() {
                shadowCopy = new MemArrayAuto<Ent>(in ent, playersSystem.GetTeams().Length),
            });
        }

    }

    public struct FogOfWarShadowCopyRequiredRuntimeComponent : IComponent {

        public MemArrayAuto<Ent> shadowCopy;

    }

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
        public tfloat height;
        
    }

    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarSectorRevealerComponent : IConfigComponent {

        public tfloat value;

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