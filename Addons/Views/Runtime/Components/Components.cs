#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Views {

    /// <summary>
    /// Groups views components for change tracking and queries.
    /// </summary>
    public struct ViewsComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.magenta;

    }
    
    /// <summary>
    /// Stores per-entity state for view.
    /// </summary>
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct ViewComponent : IComponent {

        /// <summary>
        /// Source data or instance used by this operation.
        /// </summary>
        public ViewSource source;

    }

    /// <summary>
    /// Stores per-entity state for view custom ID.
    /// </summary>
    [EditorComment("Provides custom id for prefab pooling")]
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct ViewCustomIdComponent : IComponent {

        /// <summary>
        /// Identifier used to distinguish this entry from other entries.
        /// </summary>
        public uint uniqueId;

    }

    /// <summary>
    /// Stores per-entity state for assign view.
    /// </summary>
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct AssignViewComponent : IComponent {

        /// <summary>
        /// Source data or instance used by this operation.
        /// </summary>
        public ViewSource source;
        /// <summary>
        /// Source ent used by <c>AssignViewComponent</c>.
        /// </summary>
        public Ent sourceEnt;
        /// <summary>
        /// Indicates is used.
        /// </summary>
        public bbool isUsed;

    }

    /// <summary>
    /// Stores per-entity state for is view requested.
    /// </summary>
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct IsViewRequested : IComponent {}

    /// <summary>
    /// Defines immutable configuration data for instantiate view.
    /// </summary>
    [EditorComment("Instantiate view on scene automatically from view source")]
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct InstantiateViewComponent : IConfigComponentStatic, IConfigInitialize {

        /// <summary>
        /// Presentation instance associated with the logic entity.
        /// </summary>
        public View view;
        
        /// <summary>
        /// Initializes instantiate view component state from the supplied context.
        /// </summary>
        public void OnInitialize(in Ent ent) {

            ent.InstantiateView(this.view);

        }

    }

    /// <summary>
    /// Defines immutable configuration data for instantiate view random.
    /// </summary>
    [EditorComment("Instantiate view on scene automatically from view source")]
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct InstantiateViewRandomComponent : IConfigComponentStatic, IConfigInitialize {

        /// <summary>
        /// Stores a item record used by <c>InstantiateViewRandomComponent</c>.
        /// </summary>
        [System.Serializable]
        public struct Item {

            /// <summary>
            /// Presentation instance associated with the logic entity.
            /// </summary>
            public View view;

        }
        
        /// <summary>
        /// Views used by <c>InstantiateViewRandomComponent</c>.
        /// </summary>
        public MemArrayAuto<Item> views;
        
        /// <summary>
        /// Initializes instantiate view random component state from the supplied context.
        /// </summary>
        public void OnInitialize(in Ent ent) {

            if (this.views.Length == 0u) return;
            ent.InstantiateView(this.views[ent.GetRandomValue(0u, this.views.Length)].view);

        }

    }

    /// <summary>
    /// Defines immutable configuration data for instantiate avatar view.
    /// </summary>
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct InstantiateAvatarViewComponent : IConfigComponentStatic, IConfigInitialize {

        /// <summary>
        /// Stores animation state used by presentation updates.
        /// </summary>
        [System.Serializable]
        public struct AnimationData {

            /// <summary>
            /// Defines fire point state and operations for <c>InstantiateAvatarViewComponent.AnimationData</c>.
            /// </summary>
            [System.Serializable]
            public struct FirePoint {

                /// <summary>
                /// Identifier used to address this entry within its containing registry.
                /// </summary>
                public uint id;
                /// <summary>
                /// Position in the coordinate space used by the containing API.
                /// </summary>
                public float3 position;
                /// <summary>
                /// Orientation in the coordinate space used by the containing API.
                /// </summary>
                public quaternion rotation;

            }

            /// <summary>
            /// Animation id used to locate the associated entry.
            /// </summary>
            public uint animationId;
            /// <summary>
            /// Fire point used by <c>InstantiateAvatarViewComponent.AnimationData</c>.
            /// </summary>
            public FirePoint firePoint;
            /// <summary>
            /// Fire frame used by <c>InstantiateAvatarViewComponent.AnimationData</c>.
            /// </summary>
            public uint fireFrame;

        }

        /// <summary>
        /// Stores animator parameters associated with an entity view.
        /// </summary>
        [System.Serializable]
        public struct AnimatorData {

            /// <summary>
            /// Presentation instance associated with the logic entity.
            /// </summary>
            public View view;
            /// <summary>
            /// Points used by the associated geometry or query.
            /// </summary>
            public MemArrayAuto<AnimationData> points;

            /// <summary>
            /// Returns animation data.
            /// </summary>
            public bool GetAnimationData(uint animationId, out AnimationData animationData) {
                for (uint i = 0; i < this.points.Length; ++i) {
                    var point = this.points[i];
                    if (point.animationId == animationId) {
                        animationData = point;
                        return true;
                    }
                }
                animationData = default;
                return false;
            }

        }

        /// <summary>
        /// Animator data used by <c>InstantiateAvatarViewComponent</c>.
        /// </summary>
        public AnimatorData animatorData;

        /// <summary>
        /// Initializes instantiate avatar view component state from the supplied context.
        /// </summary>
        public void OnInitialize(in Ent ent) {

            ent.InstantiateView(this.animatorData.view);

        }

    }

    /// <summary>
    /// Stores per-entity state for mesh filter.
    /// </summary>
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct MeshFilterComponent : IComponent {

        /// <summary>
        /// Mesh used by <c>MeshFilterComponent</c>.
        /// </summary>
        public RuntimeObjectReference<UnityEngine.Mesh> mesh;

    }

    /// <summary>
    /// Stores per-entity state for mesh renderer.
    /// </summary>
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct MeshRendererComponent : IComponent {

        /// <summary>
        /// Material used by the associated renderer.
        /// </summary>
        public RuntimeObjectReference<UnityEngine.Material> material;
        /// <summary>
        /// Materials used by <c>MeshRendererComponent</c>.
        /// </summary>
        public MemArrayAuto<RuntimeObjectReference<UnityEngine.Material>> materials;
        /// <summary>
        /// Shadow casting mode used by <c>MeshRendererComponent</c>.
        /// </summary>
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;
        /// <summary>
        /// Receive shadows used by <c>MeshRendererComponent</c>.
        /// </summary>
        public int receiveShadows;
        /// <summary>
        /// Layer used by <c>MeshRendererComponent</c>.
        /// </summary>
        public int layer;
        /// <summary>
        /// Rendering layer mask used to select the applicable bits or entries.
        /// </summary>
        public uint renderingLayerMask;
        /// <summary>
        /// Renderer priority used by <c>MeshRendererComponent</c>.
        /// </summary>
        public int rendererPriority;
        /// <summary>
        /// Instance id used to locate the associated entry.
        /// </summary>
        public int instanceID;

    }

}