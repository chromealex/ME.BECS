#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Views {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public struct CameraComponent : IComponent {

        public MemArrayAuto<UnityEngine.Plane> localPlanes;
        public tfloat nearClipPlane;
        public tfloat farClipPlane;
        public tfloat fieldOfViewVertical;
        public tfloat fieldOfViewHorizontal;
        public tfloat aspect;
        public bool orthographic;
        public tfloat orthographicSize;
        public Bounds bounds;

    }

    [EditorComment("Give access to the camera methods")]
    public struct CameraAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<CameraComponent> cameraDataPtr;

        public readonly ref CameraComponent component => ref this.cameraDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly CameraComponent readComponent => ref this.cameraDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref readonly Bounds bounds => ref this.cameraDataPtr.Read(this.ent.id, this.ent.gen).bounds;

        public Bounds WorldBounds => new Bounds((float3)this.bounds.center + this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>().GetWorldMatrixPosition(), this.bounds.size);

        public float4x4 worldToCameraMatrix {
            [INLINE(256)]
            get {
                var tr = this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
                var matrix = math.inverse(float4x4.TRS(tr.GetWorldMatrixPosition(), tr.GetWorldMatrixRotation(), new float3(1f, 1f, -1f)));
                return matrix;
            }
        }
        
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

        [INLINE(256)]
        public float3 WorldToViewportPoint(float3 worldPosition) {

            var worldMatrix = this.worldToCameraMatrix;
            var viewPos = math.mul(worldMatrix, new float4(worldPosition.xyz, 1f));
            var projPos = math.mul(this.projectionMatrix, viewPos);
            var ndcPos = new float3(projPos.x / projPos.w, projPos.y / projPos.w, projPos.z / projPos.w);
            var viewportPos = new float3(ndcPos.x * 0.5f + 0.5f, ndcPos.y * 0.5f + 0.5f, -viewPos.z);
            return viewportPos;

        }

        [INLINE(256)]
        public float3 WorldToScreenPoint(float3 worldPosition) {

            var viewport = this.WorldToViewportPoint(worldPosition);
            viewport.x *= UnityEngine.Screen.width;
            viewport.y *= UnityEngine.Screen.height;
            return viewport;

        }

    }
    
}