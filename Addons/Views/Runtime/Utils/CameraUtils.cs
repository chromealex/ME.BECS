namespace ME.BECS.Views {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public static class CameraUtils {

        private const int PLANES_COUNT = 6;
        private const int CORNERS_COUNT = 4;

        private static readonly UnityEngine.Plane[] planes = new UnityEngine.Plane[6];
        private static readonly UnityEngine.Vector3[] corners = new UnityEngine.Vector3[4];
        
        [INLINE(256)]
        public static void UpdateCamera(in CameraAspect cameraAspect, UnityEngine.Camera camera) {
            
            var camTr = camera.transform;
            
            var tr = cameraAspect.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
            tr.position = camTr.position;
            tr.rotation = camTr.rotation;

            ref var comp = ref cameraAspect.component;
            comp.nearClipPlane = camera.nearClipPlane;
            comp.farClipPlane = camera.farClipPlane;
            comp.fieldOfViewVertical = math.radians(camera.fieldOfView);
            comp.aspect = camera.aspect;
            comp.bounds = CalculateLocalBounds(camera, tr.rotation);
            comp.orthographic = camera.orthographic;
            comp.orthographicSize = camera.orthographicSize;
            
            var radFov = 2f * math.atan(math.tan(math.radians(camera.fieldOfView) * 0.5f) * camera.aspect);
            comp.fieldOfViewHorizontal = radFov;
            
            UnityEngine.GeometryUtility.CalculateFrustumPlanes(camera, planes);
            for (uint i = 0; i < comp.localPlanes.Length; ++i) {
                ref var plane = ref comp.localPlanes[i];
                plane = planes[i];
            }
            
        }

        [INLINE(256)]
        public static UnityEngine.Bounds CalculateLocalBounds(UnityEngine.Camera camera, in quaternion rotation) {

            var bounds = new UnityEngine.Bounds();
            camera.CalculateFrustumCorners(new UnityEngine.Rect(0f, 0f, 1f, 1f), camera.nearClipPlane, UnityEngine.Camera.MonoOrStereoscopicEye.Mono, corners);
            bounds.SetMinMax(math.mul(rotation, corners[0]), math.mul(rotation, corners[1]));
            for (int i = 0; i < CORNERS_COUNT; ++i) {
                bounds.Encapsulate(math.mul(rotation, corners[i]));
            }
            
            camera.CalculateFrustumCorners(new UnityEngine.Rect(0f, 0f, 1f, 1f), camera.farClipPlane, UnityEngine.Camera.MonoOrStereoscopicEye.Mono, corners);
            for (int i = 0; i < CORNERS_COUNT; ++i) {
                bounds.Encapsulate(math.mul(rotation, corners[i]));
            }
            
            return bounds;

        }

        [INLINE(256)]
        public static CameraAspect CreateCamera(UnityEngine.Camera camera, in World world) {
            
            var ent = Ent.New(in world);
            ent.Set<ME.BECS.Transforms.TransformAspect>();
            var aspect = ent.GetOrCreateAspect<CameraAspect>();
            ref var comp = ref aspect.component;
            comp.localPlanes = new MemArrayAuto<UnityEngine.Plane>(in ent, 6);
            UpdateCamera(aspect, camera);
            return aspect;
            
        }
        
        [INLINE(256)]
        public static bool TestPlanesAABB(in MemArrayAuto<UnityEngine.Plane> planes, in UnityEngine.Bounds bounds) {
            
            for (uint i = 0u; i < PLANES_COUNT; ++i) {
                var plane = planes[i];
                var sign = math.sign(plane.normal);
                var testPoint = (float3)bounds.center + bounds.extents * sign;
                var dot = math.dot(testPoint, plane.normal);
                if (dot + plane.distance < 0f) return false;
            }
            
            return true;
            
        }

        [INLINE(256)]
        public static bool IsVisible(in CameraAspect camera, in UnityEngine.Bounds bounds) {
            // early exit on camera bounds intersection
            if (camera.WorldBounds.Intersects(bounds) == false) return false;
            return TestPlanesAABB(in camera.readComponent.localPlanes, in bounds);
        }

        [INLINE(256)]
        public static void DrawFrustum(in CameraAspect camera) {

            var tr = camera.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
            var data = camera.readComponent;

            var color = new UnityEngine.Color(1f, 1f, 0f, 1f);
            //var matrix = UnityEngine.Gizmos.matrix;
            //UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS(tr.position, tr.rotation, new float3(1f));
            color.a = 0.05f;
            UnityEngine.Gizmos.color = color;
            UnityEngine.Gizmos.DrawCube(tr.position + (float3)data.bounds.center, data.bounds.size);
            color.a = 1f;
            UnityEngine.Gizmos.color = color;
            UnityEngine.Gizmos.DrawWireCube(tr.position + (float3)data.bounds.center, data.bounds.size);
            //UnityEngine.Gizmos.matrix = matrix;
            
        }

    }

}