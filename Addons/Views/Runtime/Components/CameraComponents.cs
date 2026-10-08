#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Plane = ME.BECS.FixedPoint.FPlane;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Plane = UnityEngine.Plane;
#endif

namespace ME.BECS.Views {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// Groups camera components for change tracking and queries.
    /// </summary>
    public struct CameraComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = new UnityEngine.Color(0.36f, 0.65f, 0.5f);

    }

    /// <summary>
    /// Stores per-entity state for camera.
    /// </summary>
    [ComponentGroup(typeof(CameraComponentGroup))]
    public struct CameraComponent : IComponent {

        /// <summary>
        /// Local planes used by <c>CameraComponent</c>.
        /// </summary>
        public MemArrayAuto<Plane> localPlanes;
        /// <summary>
        /// Near clip plane used by <c>CameraComponent</c>.
        /// </summary>
        public tfloat nearClipPlane;
        /// <summary>
        /// Far clip plane used by <c>CameraComponent</c>.
        /// </summary>
        public tfloat farClipPlane;
        /// <summary>
        /// Field of view vertical used by <c>CameraComponent</c>.
        /// </summary>
        public tfloat fieldOfViewVertical;
        /// <summary>
        /// Field of view horizontal used by <c>CameraComponent</c>.
        /// </summary>
        public tfloat fieldOfViewHorizontal;
        /// <summary>
        /// Aspect used by <c>CameraComponent</c>.
        /// </summary>
        public tfloat aspect;
        /// <summary>
        /// Orthographic size used by <c>CameraComponent</c>.
        /// </summary>
        public tfloat orthographicSize;
        /// <summary>
        /// Spatial bounds used by this entry or query.
        /// </summary>
        public Bounds bounds;
        /// <summary>
        /// Whether orthographic behavior or state is selected.
        /// </summary>
        public bbool orthographic;

    }

    /// <summary>
    /// Provides typed access to the entity components used for camera.
    /// </summary>
    [EditorComment("Give access to the camera methods")]
    public partial struct CameraAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for camera.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<CameraComponent> cameraDataPtr;

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public readonly ref CameraComponent component => ref this.cameraDataPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component.
        /// </summary>
        public readonly ref readonly CameraComponent readComponent => ref this.cameraDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Spatial bounds used by this entry or query.
        /// </summary>
        public readonly ref readonly Bounds bounds => ref this.cameraDataPtr.Read(this.ent.id, this.ent.gen).bounds;

        /// <summary>
        /// World bounds used by <c>CameraAspect</c>.
        /// </summary>
        public Bounds WorldBounds => new Bounds((float3)this.bounds.center + this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>().GetWorldMatrixPosition(), this.bounds.size);

        /// <summary>
        /// World to camera matrix used to transform between the associated coordinate spaces.
        /// </summary>
        public float4x4 worldToCameraMatrix {
            [INLINE(256)]
            get {
                var tr = this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
                var matrix = math.inverse(float4x4.TRS(tr.GetWorldMatrixPosition(), tr.GetWorldMatrixRotation(), new float3(1f, 1f, -1f)));
                return matrix;
            }
        }
        
        /// <summary>
        /// Projection matrix used to transform between the associated coordinate spaces.
        /// </summary>
        public float4x4 projectionMatrix {
            [INLINE(256)]
            get {
                float4x4 projection;
                if (this.readComponent.orthographic == true) {
                    projection = float4x4.Ortho(this.readComponent.orthographicSize * this.readComponent.aspect, this.readComponent.orthographicSize, this.readComponent.nearClipPlane, this.readComponent.farClipPlane);
                } else {
                    projection = float4x4.PerspectiveFov(this.readComponent.fieldOfViewVertical, this.readComponent.aspect, this.readComponent.nearClipPlane,
                                                         this.readComponent.farClipPlane);
                }

                return projection;
            }
        }

        /// <summary>
        /// Converts a world position to viewport point.
        /// </summary>
        [INLINE(256)]
        public float3 WorldToViewportPoint(float3 worldPosition) {

            var worldMatrix = this.worldToCameraMatrix;
            var viewPos = math.mul(worldMatrix, new float4(worldPosition.xyz, 1f));
            var projPos = math.mul(this.projectionMatrix, viewPos);
            var ndcPos = new float3(projPos.x / projPos.w, projPos.y / projPos.w, projPos.z / projPos.w);
            var viewportPos = new float3(ndcPos.x * 0.5f + 0.5f, ndcPos.y * 0.5f + 0.5f, -viewPos.z);
            return viewportPos;

        }

        /// <summary>
        /// Converts a world position to screen point.
        /// </summary>
        [INLINE(256)]
        public float3 WorldToScreenPoint(float3 worldPosition) {

            var viewport = this.WorldToViewportPoint(worldPosition);
            viewport.x *= UnityEngine.Screen.width;
            viewport.y *= UnityEngine.Screen.height;
            return viewport;

        }

    }
    
}