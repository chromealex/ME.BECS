#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Views {

    public struct ViewsComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.magenta;

    }
    
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct ViewComponent : IComponent {

        public ViewSource source;

    }

    [EditorComment("Provides custom id for prefab pooling")]
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct ViewCustomIdComponent : IComponent {

        public uint uniqueId;

    }

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct AssignViewComponent : IComponent {

        public ViewSource source;
        public Ent sourceEnt;

    }

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct IsViewRequested : IComponent {}

    [EditorComment("Instantiate view on scene automatically from view source")]
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct InstantiateViewComponent : IConfigComponentStatic, IConfigInitialize {

        public View view;
        
        public void OnInitialize(in Ent ent) {

            ent.InstantiateView(this.view);

        }

    }

    [EditorComment("Instantiate view on scene automatically from view source")]
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct InstantiateViewRandomComponent : IConfigComponentStatic, IConfigInitialize {

        [System.Serializable]
        public struct Item {

            public View view;

        }
        
        public MemArrayAuto<Item> views;
        
        public void OnInitialize(in Ent ent) {

            if (this.views.Length == 0u) return;
            ent.InstantiateView(this.views[ent.GetRandomValue(0u, this.views.Length)].view);

        }

    }

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct InstantiateAvatarViewComponent : IConfigComponentStatic, IConfigInitialize {

        [System.Serializable]
        public struct AnimationData {

            [System.Serializable]
            public struct FirePoint {

                public uint id;
                public float3 position;
                public quaternion rotation;

            }

            public uint animationId;
            public FirePoint firePoint;
            public uint fireFrame;

        }

        [System.Serializable]
        public struct AnimatorData {

            public View view;
            public MemArrayAuto<AnimationData> points;

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

        public AnimatorData animatorData;

        public void OnInitialize(in Ent ent) {

            ent.InstantiateView(this.animatorData.view);

        }

    }

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct MeshFilterComponent : IComponent {

        public RuntimeObjectReference<UnityEngine.Mesh> mesh;

    }

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct MeshRendererComponent : IComponent {

        public RuntimeObjectReference<UnityEngine.Material> material;
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;
        public int receiveShadows;
        public int layer;
        public uint renderingLayerMask;
        public int rendererPriority;
        public int instanceID;

    }

}