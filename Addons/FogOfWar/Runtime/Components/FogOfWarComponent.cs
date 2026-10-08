#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.FogOfWar {
    
    /// <summary>
    /// Groups fog of war components for change tracking and queries.
    /// </summary>
    public struct FogOfWarComponentGroup {
        
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.gray;
        
    }

    /// <summary>
    /// Stores per-entity state for quad tree query fog of war filter.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct QuadTreeQueryFogOfWarFilter : IComponent {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public FogOfWarSubFilter data;

    }

    /// <summary>
    /// Stores per-entity state for fog of war.
    /// </summary>
    [EditorComment("Main runtime component to store nodes")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarComponent : IComponent {

        /// <summary>
        /// Nodes composing the associated graph.
        /// </summary>
        public MemArrayAuto<byte> nodes;
        /// <summary>
        /// Explored used by <c>FogOfWarComponent</c>.
        /// </summary>
        public MemArrayAuto<byte> explored;

    }

    /// <summary>
    /// Stores per-entity state for fog of war.
    /// </summary>
    [EditorComment("World settings")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarStaticComponent : IComponent {

        /// <summary>
        /// Map position used by the associated spatial operation.
        /// </summary>
        public float2 mapPosition;
        /// <summary>
        /// Size of the represented value in the units used by this API.
        /// </summary>
        public uint2 size;
        /// <summary>
        /// Height samples used by the geometry or graph.
        /// </summary>
        public MemArrayAuto<tfloat> heights;
        /// <summary>
        /// Maximum height.
        /// </summary>
        public tfloat maxHeight;
        /// <summary>
        /// Node size used by <c>FogOfWarStaticComponent</c>.
        /// </summary>
        public tfloat nodeSize;

    }

    /// <summary>
    /// Defines configuration-backed entity data for fog of war shadow copy required.
    /// </summary>
    [EditorComment("Tag indicates shadow copy creation")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyRequiredComponent : IConfigComponent, IConfigInitialize {

        /// <summary>
        /// Initializes fog of war shadow copy required component state from the supplied context.
        /// </summary>
        public void OnInitialize(in Ent ent) {
            var playersSystem = ent.World.GetSystem<Players.PlayersSystem>();
            ent.Set(new FogOfWarShadowCopyRequiredRuntimeComponent() {
                shadowCopy = new MemArrayAuto<Ent>(in ent, playersSystem.GetTeams().Length),
            });
        }

    }

    /// <summary>
    /// Stores per-entity state for fog of war shadow copy required runtime.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyRequiredRuntimeComponent : IComponent {

        /// <summary>
        /// Shadow copy used by <c>FogOfWarShadowCopyRequiredRuntimeComponent</c>.
        /// </summary>
        public MemArrayAuto<Ent> shadowCopy;

    }

    /// <summary>
    /// Stores per-entity state for fog of war shadow copy.
    /// </summary>
    [EditorComment("Stores links to original entity")]
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyComponent : IComponent {

        /// <summary>
        /// For team used by <c>FogOfWarShadowCopyComponent</c>.
        /// </summary>
        public Ent forTeam;
        /// <summary>
        /// Original used by <c>FogOfWarShadowCopyComponent</c>.
        /// </summary>
        public Ent original;

    }

    /// <summary>
    /// Stores per-entity state for fog of war shadow copy points.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyPointsComponent : IComponent {

        /// <summary>
        /// Points used by the associated geometry or query.
        /// </summary>
        public MemArrayAuto<RectUInt> points;

    }

    /// <summary>
    /// Defines configuration-backed entity data for fog of war revealer.
    /// </summary>
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
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public tfloat height;
        
    }

    /// <summary>
    /// Defines configuration-backed entity data for fog of war sector revealer.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarSectorRevealerComponent : IConfigComponent {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public tfloat value;

    }

    /// <summary>
    /// Stores per-entity state for fog of war revealer is sector tag.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerIsSectorTag : IComponent {}

    /// <summary>
    /// Stores per-entity state for fog of war revealer is rect tag.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerIsRectTag : IComponent {}

    /// <summary>
    /// Stores per-entity state for fog of war revealer is range tag.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerIsRangeTag : IComponent {}

    /// <summary>
    /// Stores per-entity state for fog of war revealer is partial tag.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerIsPartialTag : IComponent {}

    /// <summary>
    /// Stores per-entity state for fog of war revealer partial.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarRevealerPartialComponent : IComponent {

        /// <summary>
        /// Part used by <c>FogOfWarRevealerPartialComponent</c>.
        /// </summary>
        public byte part;

    }
    
    /// <summary>
    /// Stores per-entity state for fog of war shadow copy was visible anytime tag.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyWasVisibleAnytimeTag : IComponent {}

    /// <summary>
    /// Stores per-entity state for fog of war shadow copy was visible tag.
    /// </summary>
    [ComponentGroup(typeof(FogOfWarComponentGroup))]
    public struct FogOfWarShadowCopyWasVisibleTag : IComponent {}

}