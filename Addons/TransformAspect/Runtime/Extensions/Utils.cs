#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Transforms {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    public static class MatrixUtils {

        [INLINE(256)]
        public static quaternion FromToRotation(float3 from, float3 to) {
            return quaternion.AxisAngle(angle: math.acos(math.clamp(math.dot(from, math.normalizesafe(to)), -1f, 1f)), axis: math.normalizesafe(math.cross(from, to)));
        }

        [INLINE(256)]
        public static quaternion FromToRotationSafe(float3 from, float3 to) {
            return quaternion.AxisAngle(angle: math.acos(math.clamp(math.dot(math.normalizesafe(from), math.normalizesafe(to)), -1f, 1f)), axis: math.normalizesafe(math.cross(from, to)));
        }

        [INLINE(256)]
        public static float3 GetPosition(in float4x4 matrix) {
            return matrix.c3.xyz;
        }
 
        [INLINE(256)]
        public static quaternion GetRotation(in float4x4 matrix) {
            float3 forward = matrix.c2.xyz;
            float3 upwards = matrix.c1.xyz;
            //if (forward.x * forward.y * forward.z == 0f && upwards.x * upwards.y * upwards.z == 0f) return quaternion.identity;
            return quaternion.LookRotation(forward, upwards);
        }
        
        [INLINE(256)]
        public static float3 GetScale(in float4x4 matrix) {
            float3 scale;
            scale.x = math.length(matrix.c0);
            scale.y = math.length(matrix.c1);
            scale.z = math.length(matrix.c2);
            return scale;
        }

    }

}